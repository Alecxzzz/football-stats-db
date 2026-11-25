using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace FootballStats.API;

/// <summary>
/// Radar del partido: el "Live Match Tracker" de Sportradar que FotMob pinta
/// en su web (campo, balón, ataques y acciones minuto a minuto).
///
/// FotMob firma el acceso con el token de su cuenta de widgets. Aquí se pide
/// ese token, se descifra y se reutiliza hasta que caduca, y los datos se
/// piden al feed desde el servidor: el navegador solo habla con este equipo.
///
///   GET /api/radar/71762370
/// </summary>
[ApiController]
public class RadarController : ControllerBase
{
    private const string CustomerKey = "44ea55a6918bd96cab0f7c854ad171ab";
    private const string Fotmob = "https://www.fotmob.com";
    private const string Feed = "https://lmt.fn.sportradar.com/common/en/Etc:UTC/gismo";
    private const int TtlSeconds = 10;

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    private static readonly HttpClient Http = CreateClient();
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static readonly ConcurrentDictionary<int, Entry> Cache = new();
    private static Token? _token;

    private sealed record Token(string Value, string Origin, DateTime Expires);

    private sealed record Entry(DateTime Expires, string Body);

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "es-ES,es;q=0.9,en;q=0.8");
        return client;
    }

    [HttpGet("api/radar/{srId:int}")]
    public async Task<IActionResult> Radar(int srId, CancellationToken ct)
    {
        if (Cache.TryGetValue(srId, out var hit) && hit.Expires > DateTime.UtcNow)
            return Json(hit.Body);

        var token = await GetTokenAsync(ct);
        if (token is null)
            return StatusCode(StatusCodes.Status502BadGateway,
                new { status = "error", message = "No se pudo firmar el acceso al radar de Sportradar." });

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Feed}/match_timeline/{srId}?T={token.Value}");
        request.Headers.TryAddWithoutValidation("Origin", token.Origin);
        request.Headers.TryAddWithoutValidation("Referer", token.Origin + "/");

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { status = "error", message = "No se pudo contactar con el radar: " + ex.Message });
        }

        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode,
                    new { status = "error", message = $"El radar respondió {(int)response.StatusCode} para este partido." });

            string body;
            try
            {
                body = Simplificar(raw);
            }
            catch (JsonException)
            {
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { status = "error", message = "El radar devolvió una respuesta que no se pudo leer." });
            }

            Cache[srId] = new Entry(DateTime.UtcNow.AddSeconds(TtlSeconds), body);
            return Json(body);
        }
    }

    private IActionResult Json(string body) => new ContentResult
    {
        StatusCode = StatusCodes.Status200OK,
        Content = body,
        ContentType = "application/json",
    };

    /// <summary>Token de la cuenta de widgets de FotMob (caduca a las ~24 h).</summary>
    private static async Task<Token?> GetTokenAsync(CancellationToken ct)
    {
        if (_token is { } cached && cached.Expires > DateTime.UtcNow.AddMinutes(5)) return cached;

        await TokenLock.WaitAsync(ct);
        try
        {
            if (_token is { } cached2 && cached2.Expires > DateTime.UtcNow.AddMinutes(5)) return cached2;

            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://widgets.sir.sportradar.com/{CustomerKey}/licensing");
            request.Headers.TryAddWithoutValidation("Origin", Fotmob);
            request.Headers.TryAddWithoutValidation("Referer", Fotmob + "/");

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var outer = JsonDocument.Parse(json);
            if (!outer.RootElement.TryGetProperty("text", out var textEl)) return null;
            var text = textEl.GetString();
            if (string.IsNullOrWhiteSpace(text)) return null;

            var plain = Descifrar(text);
            using var inner = JsonDocument.Parse(plain);
            if (!inner.RootElement.TryGetProperty("fishnetToken", out var fish)) return null;

            var value = Str(fish, "token");
            var origin = Str(fish, "origin");
            var expires = Int64(fish, "expirationTs");
            if (value.Length == 0 || expires is null) return null;

            _token = new Token(
                value,
                origin.Length > 0 ? origin : Fotmob,
                DateTimeOffset.FromUnixTimeMilliseconds(expires.Value).UtcDateTime);
            return _token;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or CryptographicException or FormatException)
        {
            return null;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    /// <summary>Formato OpenSSL "Salted__" + AES-256-CBC con clave = clave de cliente.</summary>
    private static string Descifrar(string base64)
    {
        var raw = Convert.FromBase64String(base64);
        if (raw.Length < 32) throw new FormatException("Token cifrado demasiado corto.");

        var salt = raw.Skip(8).Take(8).ToArray();
        var cipher = raw.Skip(16).ToArray();

        var material = DerivarClave(salt, 48);
        using var aes = Aes.Create();
        aes.Key = material.Take(32).ToArray();
        aes.IV = material.Skip(32).Take(16).ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>EVP_BytesToKey (MD5) como CryptoJS: la clave es la contraseña.</summary>
    private static byte[] DerivarClave(byte[] salt, int length)
    {
        var pass = Encoding.UTF8.GetBytes(CustomerKey);
        var result = new List<byte>(length + 16);
        var prev = Array.Empty<byte>();

        while (result.Count < length)
        {
            using var md5 = MD5.Create();
            var input = new byte[prev.Length + pass.Length + salt.Length];
            Buffer.BlockCopy(prev, 0, input, 0, prev.Length);
            Buffer.BlockCopy(pass, 0, input, prev.Length, pass.Length);
            Buffer.BlockCopy(salt, 0, input, prev.Length + pass.Length, salt.Length);
            prev = md5.ComputeHash(input);
            result.AddRange(prev);
        }

        return result.Take(length).ToArray();
    }

    /// <summary>Deja solo lo que pinta el radar: estado, balón, acciones y cuentas.</summary>
    private static string Simplificar(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("doc", out var docs) ||
            docs.ValueKind != JsonValueKind.Array || docs.GetArrayLength() == 0)
            return "{\"ok\":false}";

        if (!docs[0].TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return "{\"ok\":false}";

        var match = data.TryGetProperty("match", out var m) && m.ValueKind == JsonValueKind.Object
            ? m
            : default;

        var homeName = "";
        var awayName = "";
        if (match.ValueKind == JsonValueKind.Object && match.TryGetProperty("teams", out var teams))
        {
            if (teams.TryGetProperty("home", out var h)) homeName = Str(h, "name");
            if (teams.TryGetProperty("away", out var a)) awayName = Str(a, "name");
        }

        var homeScore = (int?)null;
        var awayScore = (int?)null;
        if (match.ValueKind == JsonValueKind.Object && match.TryGetProperty("result", out var result))
        {
            homeScore = Int(result, "home");
            awayScore = Int(result, "away");
        }

        var statusName = "";
        var statusShort = "";
        if (match.ValueKind == JsonValueKind.Object && match.TryGetProperty("status", out var status))
        {
            statusName = Str(status, "name");
            statusShort = Str(status, "shortName");
        }

        var live = match.ValueKind == JsonValueKind.Object && Str(match, "matchstatus") == "live";

        int? minute = null;
        if (match.ValueKind == JsonValueKind.Object && match.TryGetProperty("timeinfo", out var time))
        {
            var played = Int(time, "played");
            if (played is > 0) minute = played / 60;
        }

        var tiros = new int[2];
        var aPuerta = new int[2];
        var corners = new int[2];
        var tarjetas = new int[2];
        object? ball = null;
        var eventos = new List<object>();

        if (data.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in events.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;

                var type = Str(e, "type");
                var team = Str(e, "team");
                var index = team == "home" ? 0 : 1;

                switch (type)
                {
                    case "goal":
                        tiros[index]++;
                        aPuerta[index]++;
                        break;
                    case "shotontarget":
                        tiros[index]++;
                        aPuerta[index]++;
                        break;
                    case "shotofftarget":
                    case "shotblocked":
                        tiros[index]++;
                        break;
                    case "corner":
                        corners[index]++;
                        break;
                    case "card":
                        tarjetas[index]++;
                        break;
                }

                if (type == "ballcoordinates")
                {
                    if (e.TryGetProperty("coordinates", out var coords) &&
                        coords.ValueKind == JsonValueKind.Array && coords.GetArrayLength() > 0)
                    {
                        var last = coords[coords.GetArrayLength() - 1];
                        ball = new { equipo = Str(last, "team"), x = Int(last, "X"), y = Int(last, "Y") };
                    }
                    continue;
                }

                if (type is not ("goal" or "shotontarget" or "shotofftarget" or "shotblocked" or "corner" or "card"))
                    continue;

                var x = Int(e, "X");
                var y = Int(e, "Y");
                if (x is null || y is null) continue;

                var player = e.TryGetProperty("player", out var p) ? Str(p, "name") : "";
                eventos.Add(new
                {
                    tipo = type,
                    equipo = team,
                    x,
                    y,
                    min = Int(e, "time"),
                    jugador = player,
                });
            }
        }

        var payload = new
        {
            ok = true,
            enVivo = live,
            estado = statusShort.Length > 0 ? statusShort : statusName,
            detalle = statusName,
            minuto = minute,
            local = new { nombre = homeName, marcador = homeScore },
            visitante = new { nombre = awayName, marcador = awayScore },
            balon = ball,
            eventos,
            cuenta = new { tiros, aPuerta, corners, tarjetas },
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string Str(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var value)) return "";
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.ToString(),
            _ => "",
        };
    }

    private static int? Int(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        return int.TryParse(text, out var parsed) ? parsed : null;
    }

    private static long? Int64(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        return long.TryParse(text, out var parsed) ? parsed : null;
    }
}

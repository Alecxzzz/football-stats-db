using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace FootballStats.API;

/// <summary>
/// Puente hacia las APIs públicas (no oficiales) de las que salen las
/// estadísticas: FotMob para partidos, alineaciones, comparativas, jugadores
/// y clasificaciones, y ESPN como fuente alternativa.
///
/// El navegador nunca llama a esos dominios directamente: pide aquí, en el
/// mismo origen, y este controlador añade las cabeceras que los servicios
/// esperan, guarda la respuesta unos segundos y la archiva en MongoDB para
/// poder reutilizarla después (ver AlmacenFutbol y /api/datos).
///
///   GET /api/fm/matches?date=20261120
///   GET /api/fm/matchDetails?matchId=5868132
///   GET /api/fm/leagues?id=87
///   GET /api/espn/soccer/eng.1/scoreboard?dates=20261120
/// </summary>
[ApiController]
public class FuentesController : ControllerBase
{
    private const int TtlSeconds = 60;

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    private static readonly HttpClient Http = CreateClient();

    // FotMob añade esto a todos los requests del frontend. Sin él devuelve
    // {"action":{"notFound":true}} o 404. Solo se añade cuando falta.
    private static readonly string FotmobOrigin = "Origin",
        FotmobReferer = "Referer",
        FotmobAuthPattern = "Cookie";

    private static readonly ConcurrentDictionary<string, Entry> Cache = new();

    private readonly AlmacenFutbol _almacen;
    private readonly ILogger<FuentesController> _log;

    public FuentesController(AlmacenFutbol almacen, ILogger<FuentesController> log)
    {
        _almacen = almacen;
        _log = log;
    }

    private sealed record Entry(DateTime Expires, string Body);

    private sealed record Respuesta(int Status, string Body, bool DesdeCache);

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "es-ES,es;q=0.9,en;q=0.8");
        return client;
    }

    /// <summary>FotMob (API interna del sitio, sin credenciales).</summary>
    [HttpGet("api/fm/{**path}")]
    public async Task<IActionResult> Fotmob(string? path, CancellationToken ct)
    {
        var query = Request.QueryString.Value ?? string.Empty;
        var url = $"https://www.fotmob.com/api/data/{path}{query}";

        var reloj = Stopwatch.StartNew();
        var respuesta = await FetchAsync(url, "https://www.fotmob.com/", ct);
        reloj.Stop();

        if (respuesta.Status == 200 && !respuesta.DesdeCache)
            await _almacen.GuardarAsync(path, query, respuesta.Body);

        await _almacen.RegistrarConsultaAsync($"/api/fm/{path}", query, respuesta.Status, reloj.ElapsedMilliseconds);

        return Resultado(respuesta);
    }

    /// <summary>ESPN (API pública de su sitio), fuente alternativa.</summary>
    [HttpGet("api/espn/{**path}")]
    public async Task<IActionResult> Espn(string? path, CancellationToken ct)
    {
        var query = Request.QueryString.Value ?? string.Empty;
        var url = $"https://site.api.espn.com/apis/site/v2/sports/{path}{query}";

        var reloj = Stopwatch.StartNew();
        var respuesta = await FetchAsync(url, "https://www.espn.com/", ct);
        reloj.Stop();

        await _almacen.RegistrarConsultaAsync($"/api/espn/{path}", query, respuesta.Status, reloj.ElapsedMilliseconds);

        return Resultado(respuesta);
    }

    private async Task<Respuesta> FetchAsync(string url, string referer, CancellationToken ct)
    {
        if (Cache.TryGetValue(url, out var hit) && hit.Expires > DateTime.UtcNow)
            return new Respuesta(200, hit.Body, true);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Referer", referer);

        // FotMob exige esta cabecera; obliga Referer/Origin a su dominio.
        if (url.StartsWith("https://www.fotmob.com/api/data/"))
        {
            request.Headers.TryAddWithoutValidation(FotmobOrigin, "https://www.fotmob.com/");
            request.Headers.TryAddWithoutValidation(FotmobReferer, "https://www.fotmob.com/");
        }

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Error(StatusCodes.Status502BadGateway, "No se pudo contactar con la fuente de datos: " + ex.Message);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return Error(
                    (int)response.StatusCode,
                    $"La fuente de datos respondió {(int)response.StatusCode}. " +
                    (string.IsNullOrWhiteSpace(body) ? "Sin detalle." : "Puede que el partido o la competición ya no existan."));
            }

            Cache[url] = new Entry(DateTime.UtcNow.AddSeconds(TtlSeconds), body);

            return new Respuesta(200, body, false);
        }
    }

    private static Respuesta Error(int status, string message) => new(
        status,
        JsonSerializer.Serialize(new { status = "error", code = status, message }),
        false);

    private IActionResult Resultado(Respuesta respuesta) => new ContentResult
    {
        StatusCode = respuesta.Status,
        Content = respuesta.Body,
        ContentType = "application/json",
    };
}

using System.Text.Json;
using FootballStats.Domain;
using FootballStats.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace FootballStats.API;

/// <summary>
/// Lectura de lo que hay guardado en MongoDB. Son los datos que el visor ya
/// trajo de FotMob, así que sirven de respaldo cuando la fuente falla.
///
///   GET /api/datos/resumen
///   GET /api/datos/partidos?fecha=20261125
///   GET /api/datos/ligas
///   GET /api/datos/detalle/{matchId}
///   GET /api/datos/alineacion/{matchId}
///   GET /api/datos/estadisticas/{matchId}
///   GET /api/datos/eventos/{matchId}
///   GET /api/datos/jugadores/{matchId}
///   GET /api/datos/consultas?limite=50
/// </summary>
[ApiController]
[Route("api/datos")]
public class DatosController : ControllerBase
{
    private readonly FootballStatsContext _db;

    public DatosController(FootballStatsContext db)
    {
        _db = db;
    }

    /// <summary>Qué colecciones existen, con cuántos documentos y para qué sirven.</summary>
    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen()
    {
        var lista = new List<object>
        {
            await FichaAsync("PartidosFm", "Partidos del día que llegaron de FotMob", _db.PartidosFm),
            await FichaAsync("LigasFm", "Competiciones vistas y su id de FotMob", _db.LigasFm),
            await FichaAsync("Alineaciones", "Onces publicados, guardados para cuando la fuente deje de servirlos", _db.Alineaciones),
            await FichaAsync("Estadisticas", "Posesión, tiros, córners y demás cifras del partido", _db.Estadisticas),
            await FichaAsync("Eventos", "Goles, tarjetas y cambios del partido", _db.Eventos),
            await FichaAsync("Jugadores", "Estadísticas individuales por jugador y partido", _db.Jugadores),
            await FichaAsync("ConsultasApi", "Registro de peticiones al puente (se borra solo a los 7 días)", _db.ConsultasApi),
            await FichaAsync("LigasSoportadas", "Todas las competiciones que soporta el visor", _db.LigasSoportadas),
            await FichaAsync("TopLigas", "Las cinco ligas oficiales de referencia", _db.TopLigas),
            await FichaAsync("Paises", "Países con sus ligas, partidos y equipos", _db.Paises),
            await FichaAsync("Equipos", "Equipos vistos en los partidos guardados", _db.Equipos),
            await FichaAsync("ResumenDiario", "Una fila por día: partidos, goles, en vivo y ligas", _db.ResumenDiario),
        };

        return Ok(new { base_datos = "threesixtybets_db", colecciones = lista });
    }

    private static async Task<object> FichaAsync<T>(string nombre, string uso, IMongoCollection<T> coleccion)
    {
        var total = await coleccion.CountDocumentsAsync(Builders<T>.Filter.Empty);
        return new { coleccion = nombre, uso, documentos = total };
    }

    /// <summary>Partidos guardados de un día (formato yyyymmdd) o de un rango ISO.</summary>
    [HttpGet("partidos")]
    public async Task<IActionResult> Partidos(
        [FromQuery] string? fecha,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int limite = 400)
    {
        var filtro = Builders<PartidoGuardado>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(fecha))
        {
            filtro = Builders<PartidoGuardado>.Filter.Eq(p => p.Dia, fecha.Trim());
        }
        else if (desde != null || hasta != null)
        {
            var desdeFecha = desde ?? hasta!.Value.AddDays(-1);
            var hastaFecha = hasta ?? desde!.Value.AddDays(1);
            filtro = Builders<PartidoGuardado>.Filter.And(
                Builders<PartidoGuardado>.Filter.Gte(p => p.Fecha, desdeFecha.Date),
                Builders<PartidoGuardado>.Filter.Lt(p => p.Fecha, hastaFecha.Date.AddDays(1)));
        }

        var partidos = await _db.PartidosFm
            .Find(filtro)
            .SortBy(p => p.Fecha)
            .Limit(Math.Clamp(limite, 1, 1000))
            .ToListAsync();

        return Ok(new
        {
            total = partidos.Count,
            partidos = partidos.Select(p => new
            {
                matchId = p.MatchId,
                dia = p.Dia,
                ligaId = p.LigaId,
                liga = p.Liga,
                pais = p.Pais,
                local = p.Local,
                visitante = p.Visitante,
                localId = p.LocalId,
                visitanteId = p.VisitanteId,
                golesLocal = p.GolesLocal,
                golesVisitante = p.GolesVisitante,
                fecha = p.Fecha,
                estado = p.Estado,
                jugando = p.Jugando,
                terminado = p.Terminado,
                actualizado = p.Actualizado,
            }),
        });
    }

    [HttpGet("ligas")]
    public async Task<IActionResult> Ligas()
    {
        var ligas = await _db.LigasFm
            .Find(Builders<LigaGuardada>.Filter.Empty)
            .SortBy(l => l.Nombre)
            .ToListAsync();

        return Ok(new
        {
            total = ligas.Count,
            ligas = ligas.Select(l => new
            {
                ligaId = l.LigaId,
                nombre = l.Nombre,
                codigo = l.Codigo,
                pais = l.Pais,
                temporada = l.Temporada,
                actualizado = l.Actualizado,
            }),
        });
    }

    /// <summary>
    /// Todo lo guardado de un partido, con la forma que espera el frontend:
    /// general + header + content (lineup, stats, matchFacts.events, playerStats).
    /// </summary>
    [HttpGet("detalle/{matchId:int}")]
    public async Task<IActionResult> Detalle(int matchId)
    {
        var partido = await _db.PartidosFm.Find(p => p.MatchId == matchId).FirstOrDefaultAsync();
        var alineacion = await _db.Alineaciones.Find(a => a.MatchId == matchId).FirstOrDefaultAsync();
        var estadistica = await _db.Estadisticas.Find(e => e.MatchId == matchId).FirstOrDefaultAsync();
        var evento = await _db.Eventos.Find(e => e.MatchId == matchId).FirstOrDefaultAsync();
        var jugadores = await _db.Jugadores.Find(j => j.MatchId == matchId).ToListAsync();

        if (partido == null && alineacion == null && estadistica == null && evento == null && jugadores.Count == 0)
        {
            return NotFound(new { status = "error", message = $"No hay nada guardado del partido {matchId}." });
        }

        var content = new Dictionary<string, object?>();
        if (alineacion != null) content["lineup"] = Json(alineacion.Json);
        if (estadistica != null) content["stats"] = Json(estadistica.Json);
        if (evento != null) content["matchFacts"] = new Dictionary<string, object?> { ["events"] = Json(evento.Json) };
        if (jugadores.Count > 0)
        {
            content["playerStats"] = jugadores
                .Where(j => !string.IsNullOrWhiteSpace(j.Json))
                .ToDictionary(j => j.JugadorId.ToString(), j => (object?)Json(j.Json));
        }

        var iniciado = partido != null && (partido.Jugando || partido.Terminado);

        return Ok(new
        {
            matchId,
            liga = partido?.Liga,
            general = new
            {
                matchId,
                leagueId = partido?.LigaId,
                leagueName = partido?.Liga,
                homeTeam = new { id = partido?.LocalId, name = partido?.Local ?? alineacion?.Local },
                awayTeam = new { id = partido?.VisitanteId, name = partido?.Visitante ?? alineacion?.Visitante },
                matchTimeUTC = partido?.Fecha,
                started = iniciado,
            },
            header = new
            {
                status = new
                {
                    utcTime = partido?.Fecha,
                    started = iniciado,
                    finished = partido?.Terminado ?? false,
                    scoreStr = partido is { GolesLocal: not null, GolesVisitante: not null }
                        ? $"{partido.GolesLocal} - {partido.GolesVisitante}"
                        : null,
                },
                teams = new object[]
                {
                    new { name = partido?.Local ?? alineacion?.Local, score = partido?.GolesLocal },
                    new { name = partido?.Visitante ?? alineacion?.Visitante, score = partido?.GolesVisitante },
                },
            },
            content,
            guardado = new[] { alineacion?.Guardado, estadistica?.Guardado, evento?.Guardado }
                .Where(f => f != null)
                .DefaultIfEmpty()
                .Max(),
            guardadoAlineacion = alineacion?.Guardado,
            alineacionCompleta = alineacion != null,
        });
    }

    [HttpGet("alineacion/{matchId:int}")]
    public async Task<IActionResult> Alineacion(int matchId)
    {
        var doc = await _db.Alineaciones.Find(a => a.MatchId == matchId).FirstOrDefaultAsync();
        if (doc == null) return NotFound(new { status = "error", message = "Este partido no tiene alineación guardada." });

        return Ok(new
        {
            matchId,
            local = doc.Local,
            visitante = doc.Visitante,
            formacionLocal = doc.FormacionLocal,
            formacionVisita = doc.FormacionVisita,
            titulares = doc.Titulares,
            suplentes = doc.Suplentes,
            guardado = doc.Guardado,
            json = Json(doc.Json),
        });
    }

    [HttpGet("estadisticas/{matchId:int}")]
    public async Task<IActionResult> Estadisticas(int matchId)
    {
        var doc = await _db.Estadisticas.Find(e => e.MatchId == matchId).FirstOrDefaultAsync();
        if (doc == null) return NotFound(new { status = "error", message = "Este partido no tiene estadísticas guardadas." });

        return Ok(new { matchId, liga = doc.Liga, guardado = doc.Guardado, json = Json(doc.Json) });
    }

    [HttpGet("eventos/{matchId:int}")]
    public async Task<IActionResult> Eventos(int matchId)
    {
        var doc = await _db.Eventos.Find(e => e.MatchId == matchId).FirstOrDefaultAsync();
        if (doc == null) return NotFound(new { status = "error", message = "Este partido no tiene eventos guardados." });

        return Ok(new { matchId, total = doc.Total, goles = doc.Goles, guardado = doc.Guardado, json = Json(doc.Json) });
    }

    [HttpGet("jugadores/{matchId:int}")]
    public async Task<IActionResult> Jugadores(int matchId)
    {
        var docs = await _db.Jugadores.Find(j => j.MatchId == matchId).ToListAsync();
        if (docs.Count == 0) return NotFound(new { status = "error", message = "Este partido no tiene jugadores guardados." });

        return Ok(new
        {
            matchId,
            total = docs.Count,
            jugadores = docs.Select(j => new
            {
                jugadorId = j.JugadorId,
                nombre = j.Nombre,
                equipo = j.Equipo,
                dorsal = j.Dorsal,
                nota = j.Nota,
                actualizado = j.Actualizado,
            }),
        });
    }

    [HttpGet("consultas")]
    public async Task<IActionResult> Consultas([FromQuery] int limite = 50)
    {
        var docs = await _db.ConsultasApi
            .Find(Builders<ConsultaApi>.Filter.Empty)
            .SortByDescending(c => c.Fecha)
            .Limit(Math.Clamp(limite, 1, 500))
            .ToListAsync();

        return Ok(new
        {
            total = docs.Count,
            consultas = docs.Select(c => new { c.Ruta, c.Consulta, c.Estado, c.Ms, c.Fecha }),
        });
    }

    /// <summary>Catálogo completo de competiciones que soporta el visor.</summary>
    [HttpGet("ligas-soportadas")]
    public async Task<IActionResult> LigasSoportadas(
        [FromQuery] string? pais,
        [FromQuery] string? tipo,
        [FromQuery] string? q,
        [FromQuery] int limite = 500)
    {
        var filtro = Builders<LigaSoportada>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(pais))
            filtro &= Builders<LigaSoportada>.Filter.Eq(l => l.Pais, pais.Trim().ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(tipo))
            filtro &= Builders<LigaSoportada>.Filter.Eq(l => l.Tipo, tipo.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(q))
        {
            var texto = System.Text.RegularExpressions.Regex.Escape(q.Trim());
            filtro &= Builders<LigaSoportada>.Filter.Regex(l => l.Nombre, new MongoDB.Bson.BsonRegularExpression(texto, "i"));
        }

        var total = await _db.LigasSoportadas.CountDocumentsAsync(filtro);

        var ligas = await _db.LigasSoportadas
            .Find(filtro)
            .SortBy(l => l.Pais).ThenBy(l => l.Nombre)
            .Limit(Math.Clamp(limite, 1, 1000))
            .ToListAsync();

        return Ok(new
        {
            total,
            devueltas = ligas.Count,
            ligas = ligas.Select(l => new
            {
                ligaId = l.LigaId,
                nombre = l.Nombre,
                pais = l.Pais,
                paisNombre = l.PaisNombre,
                tipo = l.Tipo,
                url = l.Url,
            }),
        });
    }

    /// <summary>Las cinco ligas oficiales, en orden de prioridad.</summary>
    [HttpGet("top-ligas")]
    public async Task<IActionResult> TopLigas()
    {
        var ligas = await _db.TopLigas
            .Find(Builders<TopLiga>.Filter.Empty)
            .SortBy(t => t.Prioridad)
            .ToListAsync();

        return Ok(new
        {
            total = ligas.Count,
            ligas = ligas.Select(t => new
            {
                ligaId = t.LigaId,
                nombre = t.Nombre,
                pais = t.Pais,
                prioridad = t.Prioridad,
                partidosGuardados = t.PartidosGuardados,
            }),
        });
    }

    [HttpGet("paises")]
    public async Task<IActionResult> Paises([FromQuery] int limite = 200)
    {
        var paises = await _db.Paises
            .Find(Builders<PaisGuardado>.Filter.Empty)
            .SortByDescending(p => p.Ligas).ThenBy(p => p.Pais)
            .Limit(Math.Clamp(limite, 1, 500))
            .ToListAsync();

        return Ok(new
        {
            total = paises.Count,
            paises = paises.Select(p => new { p.Pais, p.Nombre, p.Ligas, p.Partidos, p.Equipos }),
        });
    }

    [HttpGet("equipos")]
    public async Task<IActionResult> Equipos(
        [FromQuery] string? pais,
        [FromQuery] int? liga,
        [FromQuery] int limite = 50)
    {
        var filtro = Builders<EquipoGuardado>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(pais))
            filtro &= Builders<EquipoGuardado>.Filter.Eq(e => e.Pais, pais.Trim().ToUpperInvariant());

        var equipos = await _db.Equipos
            .Find(filtro)
            .SortByDescending(e => e.Partidos).ThenBy(e => e.Nombre)
            .Limit(Math.Clamp(limite, 1, 500))
            .ToListAsync();

        if (liga is int ligaId)
        {
            var nombreLiga = await _db.LigasFm
                .Find(f => f.LigaId == ligaId)
                .Project(f => f.Nombre)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(nombreLiga))
                equipos = equipos.Where(e => e.Ligas.Contains(nombreLiga)).ToList();
        }

        return Ok(new
        {
            total = equipos.Count,
            equipos = equipos.Select(e => new { e.EquipoId, e.Nombre, e.Pais, e.Ligas, e.Partidos, e.Goles, e.UltimaVez }),
        });
    }

    [HttpGet("resumen-diario")]
    public async Task<IActionResult> ResumenDiario([FromQuery] int limite = 30)
    {
        var dias = await _db.ResumenDiario
            .Find(Builders<ResumenDia>.Filter.Empty)
            .SortByDescending(d => d.Dia)
            .Limit(Math.Clamp(limite, 1, 200))
            .ToListAsync();

        return Ok(new
        {
            total = dias.Count,
            dias = dias.Select(d => new { d.Dia, d.Partidos, d.Goles, d.EnVivo, d.Ligas }),
        });
    }

    /// <summary>Vuelve a calcular TopLigas, Equipos, Paises y ResumenDiario.</summary>
    [HttpPost("recalcular")]
    public async Task<IActionResult> Recalcular([FromServices] AlmacenFutbol almacen)
    {
        await almacen.RecalcularDerivadosAsync();

        return Ok(new
        {
            status = "ok",
            topLigas = await _db.TopLigas.CountDocumentsAsync(Builders<TopLiga>.Filter.Empty),
            equipos = await _db.Equipos.CountDocumentsAsync(Builders<EquipoGuardado>.Filter.Empty),
            paises = await _db.Paises.CountDocumentsAsync(Builders<PaisGuardado>.Filter.Empty),
            dias = await _db.ResumenDiario.CountDocumentsAsync(Builders<ResumenDia>.Filter.Empty),
        });
    }

    private static JsonElement Json(string texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? JsonDocument.Parse("{}").RootElement.Clone()
            : JsonDocument.Parse(texto).RootElement.Clone();
}

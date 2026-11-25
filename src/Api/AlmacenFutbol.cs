using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FootballStats.Domain;
using FootballStats.Infrastructure;
using MongoDB.Driver;

namespace FootballStats.API;

/// <summary>
/// Almacén en MongoDB de lo que el visor ya trae de FotMob.
///
/// No es un adorno: cada respuesta útil del puente se guarda y luego se puede
/// volver a leer, así que sirve de respaldo cuando la fuente falla o cuando
/// deja de publicar un dato (por ejemplo la alineación de un partido viejo).
///
///   matches?date=…        → PartidosFm + LigasFm
///   matchDetails?matchId= → Alineaciones + Estadisticas + Eventos + Jugadores
///   leagues?id=…          → LigasFm (nombre y temporada de la competición)
///   todas las peticiones  → ConsultasApi
/// </summary>
public class AlmacenFutbol
{
    private static readonly Regex ParametroFecha = new(@"date=(\d{8})", RegexOptions.Compiled);
    private static readonly Regex ParametroId = new(@"id=(\d+)", RegexOptions.Compiled);

    /// <summary>Las cinco ligas oficiales de referencia y el orden en que se muestran.</summary>
    private static readonly (int Id, string Nombre, string Pais)[] TopOficiales =
    {
        (47, "Premier League", "ENG"),
        (87, "LaLiga", "ESP"),
        (55, "Serie A", "ITA"),
        (54, "Bundesliga", "GER"),
        (53, "Ligue 1", "FRA"),
    };

    private readonly FootballStatsContext _db;
    private readonly ILogger<AlmacenFutbol> _log;

    public AlmacenFutbol(FootballStatsContext db, ILogger<AlmacenFutbol> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>Crea los índices (incluido el TTL del log de consultas). Si Mongo no responde, solo avisa.</summary>
    public async Task AsegurarIndicesAsync()
    {
        try
        {
            await _db.PartidosFm.Indexes.CreateOneAsync(new CreateIndexModel<PartidoGuardado>(
                Builders<PartidoGuardado>.IndexKeys.Ascending("match_id"),
                new CreateIndexOptions { Unique = true, Name = "match_id_unico" }));

            await _db.PartidosFm.Indexes.CreateOneAsync(new CreateIndexModel<PartidoGuardado>(
                Builders<PartidoGuardado>.IndexKeys.Ascending("dia").Ascending("fecha"),
                new CreateIndexOptions { Name = "dia_fecha" }));

            await _db.LigasFm.Indexes.CreateOneAsync(new CreateIndexModel<LigaGuardada>(
                Builders<LigaGuardada>.IndexKeys.Ascending("liga_id"),
                new CreateIndexOptions { Unique = true, Name = "liga_id_unico" }));

            await _db.Alineaciones.Indexes.CreateOneAsync(new CreateIndexModel<AlineacionGuardada>(
                Builders<AlineacionGuardada>.IndexKeys.Ascending("match_id"),
                new CreateIndexOptions { Unique = true, Name = "match_id_unico" }));

            await _db.Estadisticas.Indexes.CreateOneAsync(new CreateIndexModel<EstadisticaGuardada>(
                Builders<EstadisticaGuardada>.IndexKeys.Ascending("match_id"),
                new CreateIndexOptions { Unique = true, Name = "match_id_unico" }));

            await _db.Eventos.Indexes.CreateOneAsync(new CreateIndexModel<EventoGuardado>(
                Builders<EventoGuardado>.IndexKeys.Ascending("match_id"),
                new CreateIndexOptions { Unique = true, Name = "match_id_unico" }));

            await _db.Jugadores.Indexes.CreateOneAsync(new CreateIndexModel<JugadorGuardado>(
                Builders<JugadorGuardado>.IndexKeys.Ascending("match_id").Ascending("jugador_id"),
                new CreateIndexOptions { Unique = true, Name = "match_jugador_unico" }));

            await _db.ConsultasApi.Indexes.CreateOneAsync(new CreateIndexModel<ConsultaApi>(
                Builders<ConsultaApi>.IndexKeys.Ascending("fecha"),
                new CreateIndexOptions { Name = "ttl_7_dias", ExpireAfter = TimeSpan.FromDays(7) }));

            await _db.LigasSoportadas.Indexes.CreateOneAsync(new CreateIndexModel<LigaSoportada>(
                Builders<LigaSoportada>.IndexKeys.Ascending(l => l.LigaId),
                new CreateIndexOptions { Unique = true, Name = "liga_id_unico" }));

            await _db.LigasSoportadas.Indexes.CreateOneAsync(new CreateIndexModel<LigaSoportada>(
                Builders<LigaSoportada>.IndexKeys.Ascending(l => l.Pais).Ascending(l => l.Nombre),
                new CreateIndexOptions { Name = "pais_nombre" }));

            await _db.TopLigas.Indexes.CreateOneAsync(new CreateIndexModel<TopLiga>(
                Builders<TopLiga>.IndexKeys.Ascending(t => t.LigaId),
                new CreateIndexOptions { Unique = true, Name = "liga_id_unico" }));

            await _db.TopLigas.Indexes.CreateOneAsync(new CreateIndexModel<TopLiga>(
                Builders<TopLiga>.IndexKeys.Ascending(t => t.Prioridad),
                new CreateIndexOptions { Unique = true, Name = "prioridad_unica" }));

            await _db.Paises.Indexes.CreateOneAsync(new CreateIndexModel<PaisGuardado>(
                Builders<PaisGuardado>.IndexKeys.Ascending(p => p.Pais),
                new CreateIndexOptions { Unique = true, Name = "pais_unico" }));

            await _db.Equipos.Indexes.CreateOneAsync(new CreateIndexModel<EquipoGuardado>(
                Builders<EquipoGuardado>.IndexKeys.Ascending(e => e.EquipoId),
                new CreateIndexOptions { Unique = true, Name = "equipo_id_unico" }));

            await _db.Equipos.Indexes.CreateOneAsync(new CreateIndexModel<EquipoGuardado>(
                Builders<EquipoGuardado>.IndexKeys.Ascending(e => e.Nombre),
                new CreateIndexOptions { Name = "nombre_equipo" }));

            await _db.ResumenDiario.Indexes.CreateOneAsync(new CreateIndexModel<ResumenDia>(
                Builders<ResumenDia>.IndexKeys.Ascending(d => d.Dia),
                new CreateIndexOptions { Unique = true, Name = "dia_unico" }));

            _log.LogInformation("MongoDB: índices del almacén del visor listos");

            // Deja las colecciones derivadas al día desde lo que ya hay guardado.
            await RecalcularDerivadosAsync();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "MongoDB no responde: el visor seguirá funcionando sin guardar datos");
        }
    }

    /// <summary>Guarda la respuesta del puente según la ruta pedida. Nunca lanza.</summary>
    public async Task GuardarAsync(string? ruta, string? query, string body)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body)) return;

            var nombre = (ruta ?? string.Empty).Trim('/').ToLowerInvariant();
            if (nombre is not ("matches" or "matchdetails" or "leagues" or "allleagues")) return;

            using var documento = JsonDocument.Parse(body);
            switch (nombre)
            {
                case "matches":
                    await GuardarJornadaAsync(documento.RootElement, query ?? string.Empty);
                    break;
                case "matchdetails":
                    await GuardarDetalleAsync(documento.RootElement);
                    break;
                case "leagues":
                    await GuardarCompeticionAsync(documento.RootElement, query ?? string.Empty);
                    break;
                case "allleagues":
                    await GuardarCatalogoAsync(documento.RootElement);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo guardar en MongoDB la respuesta de {Ruta}", ruta);
        }
    }

    /// <summary>Registra la petición en ConsultasApi (colección con TTL de 7 días).</summary>
    public async Task RegistrarConsultaAsync(string ruta, string consulta, int estado, long ms)
    {
        try
        {
            await _db.ConsultasApi.InsertOneAsync(new ConsultaApi
            {
                Ruta = ruta,
                Consulta = consulta,
                Estado = estado,
                Ms = ms,
                Fecha = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "No se pudo registrar la consulta en MongoDB");
        }
    }

    /* -------------------- escritura -------------------- */

    private async Task GuardarJornadaAsync(JsonElement root, string query)
    {
        var dia = ParametroFecha.Match(query) is { Success: true } m
            ? m.Groups[1].Value
            : DateTime.UtcNow.ToString("yyyyMMdd");

        var ligas = Hijo(root, "leagues");
        if (ligas is not { ValueKind: JsonValueKind.Array }) return;

        var partidos = new List<WriteModel<PartidoGuardado>>();
        var catalogo = new List<WriteModel<LigaGuardada>>();
        var ahora = DateTime.UtcNow;

        foreach (var liga in ligas.Value.EnumerateArray())
        {
            var ligaId = Entero(Hijo(liga, "primaryId")) ?? Entero(Hijo(liga, "id")) ?? 0;
            var nombreLiga = Texto(Hijo(liga, "name"));
            var pais = Texto(Hijo(liga, "ccode"));

            if (ligaId > 0 && !string.IsNullOrWhiteSpace(nombreLiga))
            {
                var actualizacion = Builders<LigaGuardada>.Update
                    .Set(l => l.LigaId, ligaId)
                    .Set(l => l.Nombre, nombreLiga)
                    .Set(l => l.Codigo, Texto(Hijo(liga, "id")))
                    .Set(l => l.Pais, pais)
                    .Set(l => l.Actualizado, ahora);

                catalogo.Add(new UpdateOneModel<LigaGuardada>(
                    Builders<LigaGuardada>.Filter.Eq(l => l.LigaId, ligaId),
                    actualizacion) { IsUpsert = true });
            }

            var listaPartidos = Hijo(liga, "matches");
            if (listaPartidos is not { ValueKind: JsonValueKind.Array }) continue;

            foreach (var match in listaPartidos.Value.EnumerateArray())
            {
                var matchId = Entero(Hijo(match, "id"));
                if (matchId == null) continue;

                var estado = Hijo(match, "status");
                var local = Hijo(match, "home");
                var visitante = Hijo(match, "away");
                var terminado = Booleano(Hijo(estado, "finished"));
                var empezado = Booleano(Hijo(estado, "started")) || terminado;

                var sets = new List<UpdateDefinition<PartidoGuardado>>
                {
                    Builders<PartidoGuardado>.Update.Set(p => p.MatchId, matchId.Value),
                    Builders<PartidoGuardado>.Update.Set(p => p.Dia, dia),
                    Builders<PartidoGuardado>.Update.Set(p => p.LigaId, ligaId),
                    Builders<PartidoGuardado>.Update.Set(p => p.Liga, nombreLiga ?? string.Empty),
                    Builders<PartidoGuardado>.Update.Set(p => p.Local, Texto(Hijo(local, "name")) ?? string.Empty),
                    Builders<PartidoGuardado>.Update.Set(p => p.Visitante, Texto(Hijo(visitante, "name")) ?? string.Empty),
                    Builders<PartidoGuardado>.Update.Set(p => p.Estado, terminado ? "post" : empezado ? "in" : "pre"),
                    Builders<PartidoGuardado>.Update.Set(p => p.Jugando, empezado && !terminado),
                    Builders<PartidoGuardado>.Update.Set(p => p.Terminado, terminado),
                    Builders<PartidoGuardado>.Update.Set(p => p.Actualizado, ahora),
                };

                if (pais != null)
                    sets.Add(Builders<PartidoGuardado>.Update.Set(p => p.Pais, pais));

                if (Entero(Hijo(local, "id")) is int idLocal)
                    sets.Add(Builders<PartidoGuardado>.Update.Set(p => p.LocalId, idLocal));

                if (Entero(Hijo(visitante, "id")) is int idVisitante)
                    sets.Add(Builders<PartidoGuardado>.Update.Set(p => p.VisitanteId, idVisitante));

                var golesLocal = Entero(Hijo(local, "score"));
                if (golesLocal != null)
                    sets.Add(Builders<PartidoGuardado>.Update.Set(p => p.GolesLocal, golesLocal));

                var golesVisitante = Entero(Hijo(visitante, "score"));
                if (golesVisitante != null)
                    sets.Add(Builders<PartidoGuardado>.Update.Set(p => p.GolesVisitante, golesVisitante));

                var fecha = FechaHora(estado, "utcTime");
                if (fecha != null)
                    sets.Add(Builders<PartidoGuardado>.Update.Set(p => p.Fecha, fecha.Value));

                partidos.Add(new UpdateOneModel<PartidoGuardado>(
                    Builders<PartidoGuardado>.Filter.Eq(p => p.MatchId, matchId.Value),
                    Builders<PartidoGuardado>.Update.Combine(sets)) { IsUpsert = true });
            }
        }

        if (partidos.Count > 0)
            await _db.PartidosFm.BulkWriteAsync(partidos, new BulkWriteOptions { IsOrdered = false });

        if (catalogo.Count > 0)
            await _db.LigasFm.BulkWriteAsync(catalogo, new BulkWriteOptions { IsOrdered = false });

        _log.LogInformation("MongoDB: jornada {Dia} guardada ({Partidos} partidos, {Ligas} ligas)", dia, partidos.Count, catalogo.Count);

        await RecalcularDerivadosAsync();
    }

    private async Task GuardarDetalleAsync(JsonElement root)
    {
        var general = Hijo(root, "general");
        var matchId = Entero(Hijo(general, "matchId"));
        if (matchId == null) return;

        var content = Hijo(root, "content");
        if (content == null) return;

        var ahora = DateTime.UtcNow;

        // --- alineaciones ---
        var alineacion = Hijo(content, "lineup");
        if (alineacion is { ValueKind: JsonValueKind.Object })
        {
            var local = Hijo(alineacion, "homeTeam");
            var visitante = Hijo(alineacion, "awayTeam");
            var titulares = Largo(Hijo(local, "starters")) + Largo(Hijo(visitante, "starters"));
            var suplentes = Largo(Hijo(local, "subs")) + Largo(Hijo(visitante, "subs"));

            if (titulares + suplentes > 0)
            {
                var nombreLocal = Texto(Hijo(local, "name")) ?? Texto(Hijo(Hijo(general, "homeTeam"), "name")) ?? string.Empty;
                var nombreVisitante = Texto(Hijo(visitante, "name")) ?? Texto(Hijo(Hijo(general, "awayTeam"), "name")) ?? string.Empty;

                await _db.Alineaciones.UpdateOneAsync(
                    Builders<AlineacionGuardada>.Filter.Eq(a => a.MatchId, matchId.Value),
                    Builders<AlineacionGuardada>.Update
                        .Set(a => a.MatchId, matchId.Value)
                        .Set(a => a.Local, nombreLocal)
                        .Set(a => a.Visitante, nombreVisitante)
                        .Set(a => a.FormacionLocal, Texto(Hijo(local, "formation")))
                        .Set(a => a.FormacionVisita, Texto(Hijo(visitante, "formation")))
                        .Set(a => a.Titulares, titulares)
                        .Set(a => a.Suplentes, suplentes)
                        .Set(a => a.Json, alineacion.Value.GetRawText())
                        .Set(a => a.Guardado, ahora),
                    new UpdateOptions { IsUpsert = true });
            }
        }

        // --- estadísticas ---
        var stats = Hijo(content, "stats");
        if (stats is { ValueKind: JsonValueKind.Object } && Hijo(stats, "Periods") is { ValueKind: JsonValueKind.Object })
        {
            await _db.Estadisticas.UpdateOneAsync(
                Builders<EstadisticaGuardada>.Filter.Eq(e => e.MatchId, matchId.Value),
                Builders<EstadisticaGuardada>.Update
                    .Set(e => e.MatchId, matchId.Value)
                    .Set(e => e.Liga, Texto(Hijo(general, "leagueName")))
                    .Set(e => e.Json, stats.Value.GetRawText())
                    .Set(e => e.Guardado, ahora),
                new UpdateOptions { IsUpsert = true });
        }

        // --- eventos ---
        var eventos = Hijo(Hijo(content, "matchFacts"), "events");
        if (eventos is { ValueKind: JsonValueKind.Object })
        {
            var lista = Hijo(eventos, "events");
            var total = Largo(lista);
            if (lista is { ValueKind: JsonValueKind.Array } eventosLista && total > 0)
            {
                var goles = 0;
                foreach (var evento in eventosLista.EnumerateArray())
                {
                    if (string.Equals(Texto(Hijo(evento, "type")), "Goal", StringComparison.OrdinalIgnoreCase))
                        goles++;
                }

                await _db.Eventos.UpdateOneAsync(
                    Builders<EventoGuardado>.Filter.Eq(e => e.MatchId, matchId.Value),
                    Builders<EventoGuardado>.Update
                        .Set(e => e.MatchId, matchId.Value)
                        .Set(e => e.Total, total)
                        .Set(e => e.Goles, goles)
                        .Set(e => e.Json, eventos.Value.GetRawText())
                        .Set(e => e.Guardado, ahora),
                    new UpdateOptions { IsUpsert = true });
            }
        }

        // --- jugadores (un documento por jugador y partido) ---
        var jugadores = Hijo(content, "playerStats");
        if (jugadores is { ValueKind: JsonValueKind.Object })
        {
            var operaciones = new List<WriteModel<JugadorGuardado>>();

            foreach (var propiedad in jugadores.Value.EnumerateObject())
            {
                if (!int.TryParse(propiedad.Name, out var jugadorId)) continue;
                if (propiedad.Value.ValueKind != JsonValueKind.Object) continue;

                var nodo = propiedad.Value;
                var nombre = Texto(Hijo(nodo, "name"));
                if (string.IsNullOrWhiteSpace(nombre)) continue;

                var sets = new List<UpdateDefinition<JugadorGuardado>>
                {
                    Builders<JugadorGuardado>.Update.Set(j => j.MatchId, matchId.Value),
                    Builders<JugadorGuardado>.Update.Set(j => j.JugadorId, jugadorId),
                    Builders<JugadorGuardado>.Update.Set(j => j.Nombre, nombre),
                    Builders<JugadorGuardado>.Update.Set(j => j.Equipo, Texto(Hijo(nodo, "teamName")) ?? string.Empty),
                    Builders<JugadorGuardado>.Update.Set(j => j.Json, nodo.GetRawText()),
                    Builders<JugadorGuardado>.Update.Set(j => j.Actualizado, ahora),
                };

                if (Entero(Hijo(nodo, "shirtNumber")) is int dorsal)
                    sets.Add(Builders<JugadorGuardado>.Update.Set(j => j.Dorsal, dorsal));

                if (Numero(Hijo(nodo, "rating")) is double nota)
                    sets.Add(Builders<JugadorGuardado>.Update.Set(j => j.Nota, nota));

                operaciones.Add(new UpdateOneModel<JugadorGuardado>(
                    Builders<JugadorGuardado>.Filter.Where(j => j.MatchId == matchId.Value && j.JugadorId == jugadorId),
                    Builders<JugadorGuardado>.Update.Combine(sets)) { IsUpsert = true });
            }

            if (operaciones.Count > 0)
                await _db.Jugadores.BulkWriteAsync(operaciones, new BulkWriteOptions { IsOrdered = false });
        }
    }

    private async Task GuardarCompeticionAsync(JsonElement root, string query)
    {
        if (ParametroId.Match(query) is not { Success: true } m || !int.TryParse(m.Groups[1].Value, out var ligaId) || ligaId <= 0)
            return;

        var tabla = Hijo(root, "table");
        if (tabla is not { ValueKind: JsonValueKind.Array } || tabla.Value.GetArrayLength() == 0) return;

        var data = Hijo(Hijo(tabla.Value[0], "data"), "table");
        var nombre = Texto(Hijo(data, "leagueName"));
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var actualizacion = Builders<LigaGuardada>.Update
            .Set(l => l.LigaId, ligaId)
            .Set(l => l.Nombre, nombre)
            .Set(l => l.Actualizado, DateTime.UtcNow);

        if (Texto(Hijo(data, "selectedSeason")) is string temporada)
            actualizacion = actualizacion.Set(l => l.Temporada, temporada);

        if (Texto(Hijo(data, "ccode")) is string pais)
            actualizacion = actualizacion.Set(l => l.Pais, pais);

        await _db.LigasFm.UpdateOneAsync(
            Builders<LigaGuardada>.Filter.Eq(l => l.LigaId, ligaId),
            actualizacion,
            new UpdateOptions { IsUpsert = true });
    }

    /// <summary>Guarda el catálogo completo de competiciones que soporta el visor (allLeagues).</summary>
    private async Task GuardarCatalogoAsync(JsonElement root)
    {
        var ahora = DateTime.UtcNow;
        var operaciones = new List<WriteModel<LigaSoportada>>();

        void Anadir(JsonElement liga, string tipo, string? paisNombre)
        {
            var id = Entero(Hijo(liga, "id"));
            var nombre = Texto(Hijo(liga, "name"));
            if (id is not int ligaId || ligaId <= 0 || string.IsNullOrWhiteSpace(nombre)) return;

            operaciones.Add(new UpdateOneModel<LigaSoportada>(
                Builders<LigaSoportada>.Filter.Eq(l => l.LigaId, ligaId),
                Builders<LigaSoportada>.Update
                    .Set(l => l.LigaId, ligaId)
                    .Set(l => l.Nombre, nombre)
                    .Set(l => l.Pais, Texto(Hijo(liga, "ccode")) ?? string.Empty)
                    .Set(l => l.PaisNombre, paisNombre)
                    .Set(l => l.Tipo, tipo)
                    .Set(l => l.Url, Texto(Hijo(liga, "pageUrl")))
                    .Set(l => l.Actualizado, ahora)) { IsUpsert = true });
        }

        if (Hijo(root, "popular") is { ValueKind: JsonValueKind.Array } populares)
            foreach (var liga in populares.EnumerateArray())
                Anadir(liga, "popular", null);

        if (Hijo(root, "international") is { ValueKind: JsonValueKind.Array } internacionales)
            foreach (var bloque in internacionales.EnumerateArray())
            {
                var paisNombre = Texto(Hijo(bloque, "name"));
                if (Hijo(bloque, "leagues") is not { ValueKind: JsonValueKind.Array } ligas) continue;
                foreach (var liga in ligas.EnumerateArray())
                    Anadir(liga, "internacional", paisNombre);
            }

        if (Hijo(root, "countries") is { ValueKind: JsonValueKind.Array } paises)
            foreach (var bloque in paises.EnumerateArray())
            {
                var paisNombre = Texto(Hijo(bloque, "name"));
                if (Hijo(bloque, "leagues") is not { ValueKind: JsonValueKind.Array } ligas) continue;
                foreach (var liga in ligas.EnumerateArray())
                    Anadir(liga, "pais", paisNombre);
            }

        if (operaciones.Count > 0)
            await _db.LigasSoportadas.BulkWriteAsync(operaciones, new BulkWriteOptions { IsOrdered = false });

        _log.LogInformation("MongoDB: catálogo de ligas guardado ({Ligas} competiciones)", operaciones.Count);

        await RecalcularDerivadosAsync();
    }

    /// <summary>
    /// Recalcula las colecciones derivadas (TopLigas, Equipos, Paises y ResumenDiario)
    /// a partir de lo que hay en PartidosFm y LigasSoportadas. Nunca lanza.
    /// </summary>
    public async Task RecalcularDerivadosAsync()
    {
        try
        {
            var ahora = DateTime.UtcNow;
            var partidos = await _db.PartidosFm.Find(Builders<PartidoGuardado>.Filter.Empty).ToListAsync();
            var catalogo = await _db.LigasSoportadas.Find(Builders<LigaSoportada>.Filter.Empty).ToListAsync();

            // --- Top 5: prioridad fija + cuántos partidos hay guardados de cada una ---
            var porLiga = partidos.GroupBy(p => p.LigaId).ToDictionary(g => g.Key, g => g.Count());
            var opsTop = new List<WriteModel<TopLiga>>();
            for (var i = 0; i < TopOficiales.Length; i++)
            {
                var (ligaId, nombre, pais) = TopOficiales[i];
                opsTop.Add(new UpdateOneModel<TopLiga>(
                    Builders<TopLiga>.Filter.Eq(t => t.LigaId, ligaId),
                    Builders<TopLiga>.Update
                        .Set(t => t.LigaId, ligaId)
                        .Set(t => t.Nombre, nombre)
                        .Set(t => t.Pais, pais)
                        .Set(t => t.Prioridad, i + 1)
                        .Set(t => t.PartidosGuardados, porLiga.TryGetValue(ligaId, out var guardados) ? guardados : 0)
                        .Set(t => t.Actualizado, ahora)) { IsUpsert = true });
            }
            await _db.TopLigas.BulkWriteAsync(opsTop, new BulkWriteOptions { IsOrdered = false });

            // --- Equipos: uno por equipo, con partidos, goles y ligas en las que apareció ---
            var equipos = new Dictionary<int, EquipoGuardado>();

            void Sumar(int? id, string nombre, PartidoGuardado partido, int goles)
            {
                if (id is not int clave || clave <= 0 || string.IsNullOrWhiteSpace(nombre)) return;
                if (!equipos.TryGetValue(clave, out var equipo))
                    equipos[clave] = equipo = new EquipoGuardado { EquipoId = clave, Nombre = nombre };

                equipo.Partidos++;
                equipo.Goles += goles;
                equipo.Pais ??= partido.Pais;
                if (!equipo.Ligas.Contains(partido.Liga)) equipo.Ligas.Add(partido.Liga);
                if (equipo.UltimaVez == null || partido.Fecha > equipo.UltimaVez)
                    equipo.UltimaVez = partido.Fecha;
            }

            foreach (var partido in partidos)
            {
                Sumar(partido.LocalId, partido.Local, partido, partido.GolesLocal ?? 0);
                Sumar(partido.VisitanteId, partido.Visitante, partido, partido.GolesVisitante ?? 0);
            }

            if (equipos.Count > 0)
            {
                var opsEquipos = equipos.Values.Select(equipo => (WriteModel<EquipoGuardado>)new UpdateOneModel<EquipoGuardado>(
                    Builders<EquipoGuardado>.Filter.Eq(e => e.EquipoId, equipo.EquipoId),
                    Builders<EquipoGuardado>.Update
                        .Set(e => e.EquipoId, equipo.EquipoId)
                        .Set(e => e.Nombre, equipo.Nombre)
                        .Set(e => e.Pais, equipo.Pais)
                        .Set(e => e.Ligas, equipo.Ligas)
                        .Set(e => e.Partidos, equipo.Partidos)
                        .Set(e => e.Goles, equipo.Goles)
                        .Set(e => e.UltimaVez, equipo.UltimaVez)
                        .Set(e => e.Actualizado, ahora)) { IsUpsert = true }).ToList();

                await _db.Equipos.BulkWriteAsync(opsEquipos, new BulkWriteOptions { IsOrdered = false });
            }

            // --- Países: ligas del catálogo + partidos y equipos guardados ---
            var paises = new Dictionary<string, PaisGuardado>();

            PaisGuardado Pais(string clave)
            {
                if (!paises.TryGetValue(clave, out var pais))
                    paises[clave] = pais = new PaisGuardado { Pais = clave };
                return pais;
            }

            foreach (var liga in catalogo)
            {
                var clave = string.IsNullOrWhiteSpace(liga.Pais) ? "INT" : liga.Pais;
                var pais = Pais(clave);
                pais.Ligas++;
                pais.Nombre ??= liga.PaisNombre;
            }

            var equiposPorPais = equipos.Values
                .Where(e => !string.IsNullOrWhiteSpace(e.Pais))
                .GroupBy(e => e.Pais!)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var grupo in partidos.GroupBy(p => string.IsNullOrWhiteSpace(p.Pais) ? "INT" : p.Pais!))
                Pais(grupo.Key).Partidos += grupo.Count();

            foreach (var (clave, cuantos) in equiposPorPais)
                Pais(clave).Equipos = cuantos;

            var opsPaises = paises.Values.Select(pais => (WriteModel<PaisGuardado>)new UpdateOneModel<PaisGuardado>(
                Builders<PaisGuardado>.Filter.Eq(p => p.Pais, pais.Pais),
                Builders<PaisGuardado>.Update
                    .Set(p => p.Pais, pais.Pais)
                    .Set(p => p.Nombre, pais.Nombre)
                    .Set(p => p.Ligas, pais.Ligas)
                    .Set(p => p.Partidos, pais.Partidos)
                    .Set(p => p.Equipos, pais.Equipos)
                    .Set(p => p.Actualizado, ahora)) { IsUpsert = true }).ToList();

            if (opsPaises.Count > 0)
                await _db.Paises.BulkWriteAsync(opsPaises, new BulkWriteOptions { IsOrdered = false });

            // --- Resumen por día ---
            var opsDias = partidos.GroupBy(p => p.Dia).Select(grupo => (WriteModel<ResumenDia>)new UpdateOneModel<ResumenDia>(
                Builders<ResumenDia>.Filter.Eq(d => d.Dia, grupo.Key),
                Builders<ResumenDia>.Update
                    .Set(d => d.Dia, grupo.Key)
                    .Set(d => d.Partidos, grupo.Count())
                    .Set(d => d.Goles, grupo.Sum(p => (p.GolesLocal ?? 0) + (p.GolesVisitante ?? 0)))
                    .Set(d => d.EnVivo, grupo.Count(p => p.Estado == "in"))
                    .Set(d => d.Ligas, grupo.Select(p => p.LigaId).Distinct().Count())
                    .Set(d => d.Actualizado, ahora)) { IsUpsert = true }).ToList();

            if (opsDias.Count > 0)
                await _db.ResumenDiario.BulkWriteAsync(opsDias, new BulkWriteOptions { IsOrdered = false });

            _log.LogInformation(
                "MongoDB: derivados al día ({Equipos} equipos, {Paises} países, {Dias} días)",
                equipos.Count, paises.Count, opsDias.Count);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudieron recalcular las colecciones derivadas");
        }
    }

    /* -------------------- lectura de JSON de FotMob -------------------- */

    private static JsonElement? Hijo(JsonElement? padre, string nombre)
    {
        if (padre == null) return null;
        var elemento = padre.Value;
        if (elemento.ValueKind != JsonValueKind.Object || !elemento.TryGetProperty(nombre, out var hijo))
            return null;
        return hijo.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : hijo;
    }

    private static string? Texto(JsonElement? elemento)
    {
        if (elemento == null) return null;
        var valor = elemento.Value;
        return valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static int? Entero(JsonElement? elemento)
    {
        if (elemento == null) return null;
        var valor = elemento.Value;

        if (valor.ValueKind == JsonValueKind.Number && valor.TryGetInt32(out var numero)) return numero;
        if (valor.ValueKind == JsonValueKind.String &&
            int.TryParse(valor.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var texto))
            return texto;

        return null;
    }

    private static double? Numero(JsonElement? elemento)
    {
        if (elemento == null) return null;
        var valor = elemento.Value;

        if (valor.ValueKind == JsonValueKind.Number && valor.TryGetDouble(out var numero)) return numero;

        if (valor.ValueKind == JsonValueKind.Object)
        {
            foreach (var clave in new[] { "value", "rating", "num" })
            {
                if (Numero(Hijo(valor, clave)) is double interno) return interno;
            }
        }

        if (valor.ValueKind == JsonValueKind.String &&
            double.TryParse(valor.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var texto))
            return texto;

        return null;
    }

    private static bool Booleano(JsonElement? elemento)
    {
        if (elemento == null) return false;
        var valor = elemento.Value;
        return valor.ValueKind == JsonValueKind.True;
    }

    private static int Largo(JsonElement? elemento)
    {
        if (elemento == null) return 0;
        var valor = elemento.Value;
        return valor.ValueKind == JsonValueKind.Array ? valor.GetArrayLength() : 0;
    }

    private static DateTime? FechaHora(JsonElement? padre, string nombre)
    {
        if (Texto(Hijo(padre, nombre)) is not string texto) return null;

        return DateTime.TryParse(
            texto,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var fecha)
            ? fecha
            : null;
    }
}

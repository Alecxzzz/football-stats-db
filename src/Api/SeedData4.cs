using MongoDB.Driver;
using FootballStats.Domain;
using FootballStats.Infrastructure;

namespace FootballStats.Seed;

public partial class SeedData
{
    private async Task SeedPartidos()
    {
        var partidosExistentes = _context.Partidos.CountDocuments(FilterDefinition<Partido>.Empty);
        if (partidosExistentes > 0)
        {
            Console.WriteLine("[partidos] Ya existen registros, saltando...");
            return;
        }

        var partidos = new List<Partido>
        {
            new Partido { IdSellado = "10023450", Temporada = "2026-2027", Liga = "Premier League", EquipoLocal = "Manchester City", EquipoVisitante = "Arsenal", Resultado = "1 - 2", Marcador = new Marcador { Local = 1, Visitante = 2 }, Fecha = DateTime.Parse("2026-11-19T18:30:00"), Hora = TimeSpan.Parse("18:30"), LigaId = "pl_2026", LigaNombre = "Premier League", Zonas = "Europe", Enplazamiento = true, EnTransmision = false, AlCampo = true, Jugando = false, Terminado = true, Finalizado = true, FechaCarga = DateTime.UtcNow.AddDays(-1) },
            new Partido { IdSellado = "10023451", Temporada = "2026-2027", Liga = "La Liga", EquipoLocal = "Real Madrid", EquipoVisitante = "Barcelona", Resultado = "2 - 2", Marcador = new Marcador { Local = 2, Visitante = 2 }, Fecha = DateTime.Parse("2026-11-19T21:00:00"), Hora = TimeSpan.Parse("21:00"), LigaId = "lad_2026", LigaNombre = "La Liga", Zonas = "Europe", Enplazamiento = true, EnTransmision = false, AlCampo = true, Jugando = false, Terminado = true, Finalizado = true, FechaCarga = DateTime.UtcNow.AddDays(-1) },
            new Partido { IdSellado = "10023452", Temporada = "2026-2027", Liga = "Serie A", EquipoLocal = "Juventus", EquipoVisitante = "Inter Milan", Resultado = "0 - 1", Marcador = new Marcador { Local = 0, Visitante = 1 }, Fecha = DateTime.Parse("2026-11-19T20:45:00"), Hora = TimeSpan.Parse("20:45"), LigaId = "sa_2026", LigaNombre = "Serie A", Zonas = "Europe", Enplazamiento = true, EnTransmision = false, AlCampo = true, Jugando = false, Terminado = true, Finalizado = true, FechaCarga = DateTime.UtcNow.AddDays(-1) },
            new Partido { IdSellado = "10023453", Temporada = "2026-2027", Liga = "Bundesliga", EquipoLocal = "Bayern Múnich", EquipoVisitante = "Dortmund", Resultado = "3 - 3", Marcador = new Marcador { Local = 3, Visitante = 3 }, Fecha = DateTime.Parse("2026-11-19T16:30:00"), Hora = TimeSpan.Parse("16:30"), LigaId = "bl_2026", LigaNombre = "Bundesliga", Zonas = "Europe", Enplazamiento = true, AlCampo = true, Terminado = true, Finalizado = true, FechaCarga = DateTime.UtcNow.AddDays(-1) },
            new Partido { IdSellado = "10023454", Temporada = "2026-2027", Liga = "Premier League", EquipoLocal = "Liverpool", EquipoVisitante = "Chelsea", Resultado = "2 - 0", Marcador = new Marcador { Local = 2, Visitante = 0 }, Fecha = DateTime.Parse("2026-11-20T16:30:00"), Hora = TimeSpan.Parse("16:30"), LigaId = "pl_2026", LigaNombre = "Premier League", Zonas = "Europe", Enplazamiento = true, AlCampo = true, Terminado = false, Finalizado = false, FechaCarga = DateTime.UtcNow.AddDays(-1) },
            new Partido { IdSellado = "10023455", Temporada = "2026-2027", Liga = "La Liga", EquipoLocal = "Sevilla", EquipoVisitante = "Atlético Madrid", Resultado = null, Marcador = null, Fecha = DateTime.Parse("2026-11-20T20:00:00"), Hora = TimeSpan.Parse("20:00"), LigaId = "lad_2026", LigaNombre = "La Liga", Zonas = "Europe", Enplazamiento = true, AlCampo = true, Terminado = false, Finalizado = false, FechaCarga = DateTime.UtcNow.AddDays(-1) },
            new Partido { IdSellado = "10023456", Temporada = "2026-2027", Liga = "Serie A", EquipoLocal = "Napoli", EquipoVisitante = "Roma", Resultado = null, Marcador = null, Fecha = DateTime.Parse("2026-11-20T20:45:00"), Hora = TimeSpan.Parse("20:45"), LigaId = "sa_2026", LigaNombre = "Serie A", Zonas = "Europe", Enplazamiento = true, AlCampo = true, Terminado = false, Finalizado = false, FechaCarga = DateTime.UtcNow.AddDays(-1) },
            new Partido { IdSellado = "10023457", Temporada = "2026-2027", Liga = "Bundesliga", EquipoLocal = "Leverkusen", EquipoVisitante = "Schalke 04", Resultado = null, Marcador = null, Fecha = DateTime.Parse("2026-11-20T18:30:00"), Hora = TimeSpan.Parse("18:30"), LigaId = "bl_2026", LigaNombre = "Bundesliga", Zonas = "Europe", Enplazamiento = true, AlCampo = true, Terminado = false, Finalizado = false, FechaCarga = DateTime.UtcNow.AddDays(-1) },
            new Partido { IdSellado = "10023458", Temporada = "2026-2027", Liga = "Premier League", EquipoLocal = "Manchester United", EquipoVisitante = "Newcastle United", Resultado = null, Marcador = null, Fecha = DateTime.Parse("2026-11-20T18:30:00"), Hora = TimeSpan.Parse("18:30"), LigaId = "pl_2026", LigaNombre = "Premier League", Zonas = "Europe", Enplazamiento = true, AlCampo = true, Terminado = false, Finalizado = false, FechaCarga = DateTime.UtcNow.AddDays(-1) },
            new Partido { IdSellado = "10023459", Temporada = "2026-2027", Liga = "La Liga", EquipoLocal = "Real Betis", EquipoVisitante = "Villarreal", Resultado = null, Marcador = null, Fecha = DateTime.Parse("2026-11-20T21:00:00"), Hora = TimeSpan.Parse("21:00"), LigaId = "lad_2026", LigaNombre = "La Liga", Zonas = "Europe", Enplazamiento = true, AlCampo = true, Terminado = false, Finalizado = false, FechaCarga = DateTime.UtcNow.AddDays(-1) }
        };

        foreach (var p in partidos)
            await _context.Partidos.InsertOneAsync(p);

        Console.WriteLine($"[partidos] Insertados {partidos.Count} partidos.");
    }
}

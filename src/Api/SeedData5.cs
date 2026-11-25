using MongoDB.Driver;
using FootballStats.Domain;
using FootballStats.Infrastructure;

namespace FootballStats.Seed;

public partial class SeedData
{
    private async Task SeedLigas()
    {
        var ligasExistentes = _context.Ligas.CountDocuments(FilterDefinition<Liga>.Empty);
        if (ligasExistentes > 0)
        {
            Console.WriteLine("[ligas] Ya existen registros, saltando...");
            return;
        }

        var ligas = new List<Liga>
        {
            new Liga { Nombre = "Premier League", Codigo = "PL", Pais = "Inglaterra", Estadio = "Wembley Stadium", LigaHub = true, LigaInternational = true, LigaFemenina = false, LigaKids = false, LigaVip = true, LigaTorneo = false, LigaDenominacion = "Premier League", CreadoEn = DateTime.UtcNow, Activo = true },
            new Liga { Nombre = "La Liga Santander", Codigo = "LA", Pais = "España", Estadio = "Santiago Bernabéu", LigaHub = true, LigaInternational = true, LigaFemenina = false, LigaKids = false, LigaVip = true, LigaTorneo = false, LigaDenominacion = "La Liga", CreadoEn = DateTime.UtcNow, Activo = true },
            new Liga { Nombre = "Serie A", Codigo = "SA", Pais = "Italia", Estadio = "San Siro", LigaHub = true, LigaInternational = true, LigaFemenina = false, LigaKids = false, LigaVip = true, LigaTorneo = true, LigaDenominacion = "Serie A", CreadoEn = DateTime.UtcNow, Activo = true },
            new Liga { Nombre = "Bundesliga", Codigo = "BL", Pais = "Alemania", Estadio = "Allianz Arena", LigaHub = true, LigaInternational = true, LigaFemenina = false, LigaKids = false, LigaVip = true, LigaTorneo = false, LigaDenominacion = "Bundesliga", CreadoEn = DateTime.UtcNow, Activo = true },
            new Liga { Nombre = "Ligue 1", Codigo = "LF", Pais = "Francia", Estadio = "Parc des Princes", LigaHub = true, LigaInternational = true, LigaFemenina = false, LigaKids = false, LigaVip = true, LigaTorneo = false, LigaDenominacion = "Ligue 1", CreadoEn = DateTime.UtcNow, Activo = true }
        };

        foreach (var l in ligas)
            await _context.Ligas.InsertOneAsync(l);

        Console.WriteLine($"[ligas] Insertadas {ligas.Count} ligas.");
    }

    private async Task SeedCuotas()
    {
        var cuotasExistentes = _context.Cuotas.CountDocuments(FilterDefinition<Cuota>.Empty);
        if (cuotasExistentes > 0)
        {
            Console.WriteLine("[cuotas] Ya existen registros, saltando...");
            return;
        }

        var partidos = await _context.Partidos.Find(FilterDefinition<Partido>.Empty).ToListAsync();
        var partidoId = partidos.FirstOrDefault(p => p.IdSellado == "10023450")?.Id ?? string.Empty;

        var cuotas = new List<Cuota>
        {
            new Cuota { ReferenciaPartido = partidoId, Temporada = "2026-2027", Ligas = "Premier League", ResultadoEsperado = "1 - 2", Perdido = "2 - 1", TipoCuota = "1X2", Cuotado = "1", CuotadoN = 1, IdOcupante = "10023460", CuotaValor = 2.45m, Linea = 1.85m, FechaActualizacion = DateTime.UtcNow.AddHours(-1), Activa = true, CreadoEn = DateTime.UtcNow.AddHours(-2) },
            new Cuota { ReferenciaPartido = partidoId, Temporada = "2026-2027", Ligas = "Premier League", ResultadoEsperado = "1 - 2", Perdido = "2 - 1", TipoCuota = "1X2", Cuotado = "X", CuotadoN = 2, IdOcupante = "10023461", CuotaValor = 3.20m, Linea = null, FechaActualizacion = DateTime.UtcNow.AddHours(-1), Activa = true, CreadoEn = DateTime.UtcNow.AddHours(-2) },
            new Cuota { ReferenciaPartido = partidoId, Temporada = "2026-2027", Ligas = "Premier League", ResultadoEsperado = "1 - 2", Perdido = "2 - 1", TipoCuota = "1X2", Cuotado = "2", CuotadoN = 3, IdOcupante = "10023462", CuotaValor = 2.90m, Linea = null, FechaActualizacion = DateTime.UtcNow.AddHours(-1), Activa = true, CreadoEn = DateTime.UtcNow.AddHours(-2) },
            new Cuota { ReferenciaPartido = partidoId, Temporada = "2026-2027", Ligas = "Premier League", ResultadoEsperado = "1 - 2", Perdido = "2 - 1", TipoCuota = "Double Chance", Cuotado = "1X", CuotadoN = 4, IdOcupante = "10023463", CuotaValor = 1.55m, Linea = null, FechaActualizacion = DateTime.UtcNow.AddHours(-1), Activa = true, CreadoEn = DateTime.UtcNow.AddHours(-2) },
            new Cuota { ReferenciaPartido = partidoId, Temporada = "2026-2027", Ligas = "Premier League", ResultadoEsperado = "1 - 2", Perdido = "2 - 1", TipoCuota = "Over/Under", Cuotado = "Over 2.5", CuotadoN = 5, IdOcupante = "10023464", CuotaValor = 1.80m, Linea = null, FechaActualizacion = DateTime.UtcNow.AddHours(-1), Activa = true, CreadoEn = DateTime.UtcNow.AddHours(-2) }
        };

        foreach (var c in cuotas)
            await _context.Cuotas.InsertOneAsync(c);

        Console.WriteLine($"[cuotas] Insertadas {cuotas.Count} cuotas.");
    }
}

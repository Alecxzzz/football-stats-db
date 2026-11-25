using MongoDB.Driver;
using FootballStats.Domain;

namespace FootballStats.Infrastructure;

/// <summary>
/// Repositorio especializado para Partidos.
/// </summary>
public class PartidoRepository : IRepository<Partido>
{
    private readonly Repository<Partido> _repo;
    private readonly IMongoCollection<Partido> _collection;

    public PartidoRepository(FootballStatsContext context)
    {
        _collection = context.Partidos;
        _repo = new Repository<Partido>(context);
    }

    public Task<Partido> CreateAsync(Partido entity)
        => _repo.CreateAsync(entity);

    public Task<IEnumerable<Partido>> GetAllAsync(FilterDefinition<Partido>? filter = null)
        => _repo.GetAllAsync(filter);

    public Task<Partido?> GetByIdAsync(string id)
        => _repo.GetByIdAsync(id);

    public Task<bool> UpdateAsync(string id, UpdateDefinition<Partido> update)
        => _repo.UpdateAsync(id, update);

    public Task<bool> DeleteAsync(string id)
        => _repo.DeleteAsync(id);

    public Task<bool> SoftDeleteAsync(string id)
        => _repo.SoftDeleteAsync(id);

    /// <summary>
    /// Obtiene los partidos para la página de estadísticas:
    /// ayer, hoy y mañana.
    /// </summary>
    public async Task<IEnumerable<Partido>> GetPartidosDiariosAsync(DateTime fechaBase)
    {
        // Ayer, hoy y mañana en tiempo local de la zona de la mente del usuario
        var dias = new[] { -1, 0, 1 };
        var filtros = dias.Select(d =>
        {
            var desde = fechaBase.Date.AddDays(d);
            var hasta = desde.AddDays(1);

            return Builders<Partido>.Filter.And(
                Builders<Partido>.Filter.Eq(p => p.Activo, true),
                Builders<Partido>.Filter.Gte(p => p.Fecha, desde),
                Builders<Partido>.Filter.Lt(p => p.Fecha, hasta)
            );
        }).ToList();

        var filtro = Builders<Partido>.Filter.Or(filtros);
        return await _collection.Find(filtro).ToListAsync();
    }

    /// <summary>
    /// Obtiene partidos en vivo o en plena transmisión.
    /// </summary>
    public async Task<IEnumerable<Partido>> GetPartidosEnVivoAsync()
    {
        var filtro = Builders<Partido>.Filter.And(
            Builders<Partido>.Filter.Eq(p => p.Activo, true),
            Builders<Partido>.Filter.Where(p => p.Jugando || p.EnTransmision || p.EnPausa)
        );
        return await _collection.Find(filtro).ToListAsync();
    }

    /// <summary>
    /// Obtiene partidos por liga.
    /// </summary>
    public async Task<IEnumerable<Partido>> GetByLigaAsync(string? ligaId)
    {
        var filtro = ligaId is null
            ? Builders<Partido>.Filter.Eq(p => p.Activo, true)
            : Builders<Partido>.Filter.Eq(p => p.LigaId, ligaId);
        return await _collection.Find(filtro).ToListAsync();
    }

    /// <summary>
    /// Obtiene partidos por rango de fechas (para reportes tipo semanal).
    /// </summary>
    public async Task<IEnumerable<Partido>> GetByDateRangeAsync(DateTime fechaDesde, DateTime fechaHasta)
    {
        var filtro = Builders<Partido>.Filter.And(
            Builders<Partido>.Filter.Eq(p => p.Activo, true),
            Builders<Partido>.Filter.Gte(p => p.Fecha, fechaDesde),
            Builders<Partido>.Filter.Lte(p => p.Fecha, fechaHasta)
        );
        return await _collection.Find(filtro).ToListAsync();
    }
}

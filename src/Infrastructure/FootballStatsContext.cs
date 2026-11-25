using MongoDB.Driver;
using FootballStats.Domain;

namespace FootballStats.Infrastructure;

/// <summary>
/// Contexto de MongoDB: expone los Clientes de cada colección.
/// </summary>
public class FootballStatsContext
{
    private readonly IMongoDatabase _database;

    /// <summary>
    /// Colección: usuarios / clientes
    /// </summary>
    public IMongoCollection<Usuario> Usuarios => _database.GetCollection<Usuario>(nameof(Usuarios));

    /// <summary>
    /// Colección: categorías
    /// </summary>
    public IMongoCollection<Categoria> Categorias => _database.GetCollection<Categoria>(nameof(Categorias));

    /// <summary>
    /// Colección: productos / artículos
    /// </summary>
    public IMongoCollection<Producto> Productos => _database.GetCollection<Producto>(nameof(Productos));

    /// <summary>
    /// Colección: pedidos / transacciones
    /// </summary>
    public IMongoCollection<Pedido> Pedidos => _database.GetCollection<Pedido>(nameof(Pedidos));

    /// <summary>
    /// Colección: inventario / movimientos
    /// </summary>
    public IMongoCollection<Inventario> Inventarios => _database.GetCollection<Inventario>(nameof(Inventarios));

    /// <summary>
    /// Colección: partidos de futbol (caso de uso específico del proyecto)
    /// </summary>
    public IMongoCollection<Partido> Partidos => _database.GetCollection<Partido>(nameof(Partidos));

    /// <summary>
    /// Colección: ligas / torneos
    /// </summary>
    public IMongoCollection<Liga> Ligas => _database.GetCollection<Liga>(nameof(Ligas));

    /// <summary>
    /// Colección: cuotas / odds (apuestas)
    /// </summary>
    public IMongoCollection<Cuota> Cuotas => _database.GetCollection<Cuota>(nameof(Cuotas));

    // ------------------------------------------------------------------
    // Almacén del visor: lo que se guarda de verdad al navegar los partidos.
    // ------------------------------------------------------------------

    /// <summary>Partidos del día pedido a FotMob (uno por match_id).</summary>
    public IMongoCollection<PartidoGuardado> PartidosFm => _database.GetCollection<PartidoGuardado>("PartidosFm");

    /// <summary>Competiciones vistas, con su id de FotMob.</summary>
    public IMongoCollection<LigaGuardada> LigasFm => _database.GetCollection<LigaGuardada>("LigasFm");

    /// <summary>Alineaciones publicadas por FotMob, guardadas por partido.</summary>
    public IMongoCollection<AlineacionGuardada> Alineaciones => _database.GetCollection<AlineacionGuardada>("Alineaciones");

    /// <summary>Estadísticas del partido (posesión, tiros, córners…).</summary>
    public IMongoCollection<EstadisticaGuardada> Estadisticas => _database.GetCollection<EstadisticaGuardada>("Estadisticas");

    /// <summary>Cronología de goles, tarjetas y cambios.</summary>
    public IMongoCollection<EventoGuardado> Eventos => _database.GetCollection<EventoGuardado>("Eventos");

    /// <summary>Estadísticas individuales: un documento por jugador y partido.</summary>
    public IMongoCollection<JugadorGuardado> Jugadores => _database.GetCollection<JugadorGuardado>("Jugadores");

    /// <summary>Registro de peticiones al puente de datos (se purga a los 7 días).</summary>
    public IMongoCollection<ConsultaApi> ConsultasApi => _database.GetCollection<ConsultaApi>("ConsultasApi");

    /// <summary>Catálogo completo de competiciones que soporta el visor (Todas las ligas).</summary>
    public IMongoCollection<LigaSoportada> LigasSoportadas => _database.GetCollection<LigaSoportada>("LigasSoportadas");

    /// <summary>Las cinco ligas oficiales de referencia (Premier, LaLiga, Serie A, Bundesliga, Ligue 1).</summary>
    public IMongoCollection<TopLiga> TopLigas => _database.GetCollection<TopLiga>("TopLigas");

    /// <summary>Países con su número de ligas, partidos y equipos.</summary>
    public IMongoCollection<PaisGuardado> Paises => _database.GetCollection<PaisGuardado>("Paises");

    /// <summary>Equipos vistos en los partidos guardados, sin repetir.</summary>
    public IMongoCollection<EquipoGuardado> Equipos => _database.GetCollection<EquipoGuardado>("Equipos");

    /// <summary>Una fila por día con partidos, goles, en vivo y ligas.</summary>
    public IMongoCollection<ResumenDia> ResumenDiario => _database.GetCollection<ResumenDia>("ResumenDiario");

    public FootballStatsContext(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }
}

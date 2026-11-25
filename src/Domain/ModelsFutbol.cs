using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FootballStats.Domain;

/// <summary>
/// Colección: partidos de futbol (caso de uso específico).
/// Almacena los partidos de ayer, hoy y mañana con estado, marcador y enplazamiento.
/// </summary>
public class Partido : MongoId
{
    [BsonElement("id_sellado")]
    public string? IdSellado { get; set; }

    [BsonElement("temporada")]
    public string? Temporada { get; set; }

    [BsonElement("liga")]
    public string? Liga { get; set; }

    [BsonElement("equipo_local")]
    public string? EquipoLocal { get; set; }

    [BsonElement("equipo_visitante")]
    public string? EquipoVisitante { get; set; }

    [BsonElement("resultado")]
    public string? Resultado { get; set; }

    [BsonElement("marcador")]
    public Marcador? Marcador { get; set; }

    [BsonElement("fecha")]
    public DateTime Fecha { get; set; }

    [BsonElement("hora")]
    public TimeSpan Hora { get; set; }

    [BsonElement("liga_id")]
    public string? LigaId { get; set; }

    [BsonElement("liga_nombre")]
    public string? LigaNombre { get; set; }

    [BsonElement("zonas")]
    public string? Zonas { get; set; }

    [BsonElement("wan")]
    public string? Wan { get; set; }

    [BsonElement("tipo")]
    public string? Tipo { get; set; }

    [BsonElement("enplazamiento")]
    public bool Enplazamiento { get; set; }

    [BsonElement("en_transmision")]
    public bool EnTransmision { get; set; }

    [BsonElement("al_campo")]
    public bool AlCampo { get; set; }

    [BsonElement("jugando")]
    public bool Jugando { get; set; }

    [BsonElement("terminado")]
    public bool Terminado { get; set; }

    [BsonElement("finalizado")]
    public bool Finalizado { get; set; }

    [BsonElement("en_pausa")]
    public bool EnPausa { get; set; }

    [BsonElement("inicio")]
    public DateTime? Inicio { get; set; }

    [BsonElement("jugadoras")]
    public bool Jugadoras { get; set; }

    [BsonElement("fecha_carga")]
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;

    [BsonElement("activo")]
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Datos del marcador (local vs visitante).
/// </summary>
public class Marcador
{
    [BsonElement("local")]
    public int Local { get; set; }

    [BsonElement("visitante")]
    public int Visitante { get; set; }
}

/// <summary>
/// Colección: ligas / torneos.
/// </summary>
public class Liga : MongoId
{
    [BsonElement("nombre")]
    public string? Nombre { get; set; }

    [BsonElement("codigo")]
    public string? Codigo { get; set; }

    [BsonElement("pais")]
    public string? Pais { get; set; }

    [BsonElement("estadio")]
    public string? Estadio { get; set; }

    [BsonElement("liga_hub")]
    public bool LigaHub { get; set; }

    [BsonElement("liga_international")]
    public bool LigaInternational { get; set; }

    [BsonElement("liga_femenina")]
    public bool LigaFemenina { get; set; }

    [BsonElement("liga_kids")]
    public bool LigaKids { get; set; }

    [BsonElement("liga_vip")]
    public bool LigaVip { get; set; }

    [BsonElement("liga_torneo")]
    public bool LigaTorneo { get; set; }

    [BsonElement("liga_denominacion")]
    public string? LigaDenominacion { get; set; }

    [BsonElement("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    [BsonElement("activo")]
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Colección: cuotas / odds de apuestas.
/// </summary>
public class Cuota : MongoId
{
    [BsonElement("referencia_partido")]
    public string? ReferenciaPartido { get; set; }

    [BsonElement("temporada")]
    public string? Temporada { get; set; }

    [BsonElement("ligas")]
    public string? Ligas { get; set; }

    [BsonElement("resultado_esperado")]
    public string? ResultadoEsperado { get; set; }

    [BsonElement("perdido")]
    public string? Perdido { get; set; }

    [BsonElement("tipo_cuota")]
    public string? TipoCuota { get; set; }

    [BsonElement("cuotado")]
    public string? Cuotado { get; set; }

    [BsonElement("cuotado_n")]
    public int CuotadoN { get; set; }

    [BsonElement("id_ocupante")]
    public string? IdOcupante { get; set; }

    [BsonElement("cuota")]
    public decimal CuotaValor { get; set; }

    [BsonElement("linea")]
    public decimal? Linea { get; set; }

    [BsonElement("fecha_actualizacion")]
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    [BsonElement("activa")]
    public bool Activa { get; set; } = true;

    [BsonElement("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

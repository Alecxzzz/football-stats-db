using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FootballStats.Domain;

/// <summary>
/// Base de los documentos del almacén: id ObjectId de MongoDB.
/// </summary>
public abstract class DocumentoAlmacen
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
}

/// <summary>
/// Colección PartidosFm: cada partido que FotMob devuelve en el listado del día.
/// Se actualiza (upsert) cada vez que se pide esa jornada.
/// </summary>
public class PartidoGuardado : DocumentoAlmacen
{
    [BsonElement("match_id")]
    public int MatchId { get; set; }

    /// <summary>Día consultado, en formato yyyymmdd.</summary>
    [BsonElement("dia")]
    public string Dia { get; set; } = string.Empty;

    [BsonElement("liga_id")]
    public int LigaId { get; set; }

    [BsonElement("liga")]
    public string Liga { get; set; } = string.Empty;

    [BsonElement("pais")]
    public string? Pais { get; set; }

    [BsonElement("local")]
    public string Local { get; set; } = string.Empty;

    [BsonElement("visitante")]
    public string Visitante { get; set; } = string.Empty;

    [BsonElement("local_id")]
    public int? LocalId { get; set; }

    [BsonElement("visitante_id")]
    public int? VisitanteId { get; set; }

    [BsonElement("goles_local")]
    public int? GolesLocal { get; set; }

    [BsonElement("goles_visitante")]
    public int? GolesVisitante { get; set; }

    [BsonElement("fecha")]
    public DateTime Fecha { get; set; }

    /// <summary>pre · in · post</summary>
    [BsonElement("estado")]
    public string Estado { get; set; } = "pre";

    [BsonElement("jugando")]
    public bool Jugando { get; set; }

    [BsonElement("terminado")]
    public bool Terminado { get; set; }

    [BsonElement("actualizado")]
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección LigasFm: catálogo de competiciones vistas en el listado o en la clasificación.
/// </summary>
public class LigaGuardada : DocumentoAlmacen
{
    [BsonElement("liga_id")]
    public int LigaId { get; set; }

    [BsonElement("nombre")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Id de la edición concreta (fase, grupo…).</summary>
    [BsonElement("codigo")]
    public string? Codigo { get; set; }

    [BsonElement("pais")]
    public string? Pais { get; set; }

    [BsonElement("temporada")]
    public string? Temporada { get; set; }

    [BsonElement("actualizado")]
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección Alineaciones: el once que publicó FotMob, guardado tal cual (json)
/// para poder mostrarlo cuando la fuente deje de servirlo.
/// </summary>
public class AlineacionGuardada : DocumentoAlmacen
{
    [BsonElement("match_id")]
    public int MatchId { get; set; }

    [BsonElement("local")]
    public string Local { get; set; } = string.Empty;

    [BsonElement("visitante")]
    public string Visitante { get; set; } = string.Empty;

    [BsonElement("formacion_local")]
    public string? FormacionLocal { get; set; }

    [BsonElement("formacion_visita")]
    public string? FormacionVisita { get; set; }

    [BsonElement("titulares")]
    public int Titulares { get; set; }

    [BsonElement("suplentes")]
    public int Suplentes { get; set; }

    [BsonElement("json")]
    public string Json { get; set; } = string.Empty;

    [BsonElement("guardado")]
    public DateTime Guardado { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección Estadisticas: bloque content.stats de FotMob por partido.
/// </summary>
public class EstadisticaGuardada : DocumentoAlmacen
{
    [BsonElement("match_id")]
    public int MatchId { get; set; }

    [BsonElement("liga")]
    public string? Liga { get; set; }

    [BsonElement("json")]
    public string Json { get; set; } = string.Empty;

    [BsonElement("guardado")]
    public DateTime Guardado { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección Eventos: cronología (goles, tarjetas, cambios) del partido.
/// </summary>
public class EventoGuardado : DocumentoAlmacen
{
    [BsonElement("match_id")]
    public int MatchId { get; set; }

    [BsonElement("total")]
    public int Total { get; set; }

    [BsonElement("goles")]
    public int Goles { get; set; }

    [BsonElement("json")]
    public string Json { get; set; } = string.Empty;

    [BsonElement("guardado")]
    public DateTime Guardado { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección Jugadores: un documento por jugador y partido, con sus estadísticas.
/// </summary>
public class JugadorGuardado : DocumentoAlmacen
{
    [BsonElement("match_id")]
    public int MatchId { get; set; }

    [BsonElement("jugador_id")]
    public int JugadorId { get; set; }

    [BsonElement("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [BsonElement("equipo")]
    public string Equipo { get; set; } = string.Empty;

    [BsonElement("dorsal")]
    public int? Dorsal { get; set; }

    [BsonElement("nota")]
    public double? Nota { get; set; }

    [BsonElement("json")]
    public string Json { get; set; } = string.Empty;

    [BsonElement("actualizado")]
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección LigasSoportadas: catálogo completo de competiciones que ofrece FotMob.
/// Es la lista del selector "todas las ligas": popular + internacionales + países.
/// </summary>
public class LigaSoportada : DocumentoAlmacen
{
    [BsonElement("liga_id")]
    public int LigaId { get; set; }

    [BsonElement("nombre")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Código de país de 3 letras (ENG, ESP, INT…).</summary>
    [BsonElement("pais")]
    public string Pais { get; set; } = string.Empty;

    [BsonElement("pais_nombre")]
    public string? PaisNombre { get; set; }

    /// <summary>popular · internacional · pais</summary>
    [BsonElement("tipo")]
    public string Tipo { get; set; } = "pais";

    [BsonElement("url")]
    public string? Url { get; set; }

    [BsonElement("actualizado")]
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección TopLigas: las cinco competiciones oficiales de referencia
/// (Premier League, LaLiga, Serie A, Bundesliga y Ligue 1), con su prioridad
/// y cuántos partidos hay guardados de cada una.
/// </summary>
public class TopLiga : DocumentoAlmacen
{
    [BsonElement("liga_id")]
    public int LigaId { get; set; }

    [BsonElement("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [BsonElement("pais")]
    public string Pais { get; set; } = string.Empty;

    /// <summary>1 = la más importante de la lista.</summary>
    [BsonElement("prioridad")]
    public int Prioridad { get; set; }

    [BsonElement("partidos_guardados")]
    public int PartidosGuardados { get; set; }

    [BsonElement("actualizado")]
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección Paises: un documento por país con cuántas ligas soporta y cuántos
/// partidos/equipos hay guardados de él.
/// </summary>
public class PaisGuardado : DocumentoAlmacen
{
    [BsonElement("pais")]
    public string Pais { get; set; } = string.Empty;

    [BsonElement("nombre")]
    public string? Nombre { get; set; }

    [BsonElement("ligas")]
    public int Ligas { get; set; }

    [BsonElement("partidos")]
    public int Partidos { get; set; }

    [BsonElement("equipos")]
    public int Equipos { get; set; }

    [BsonElement("actualizado")]
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección Equipos: equipos vistos en los partidos guardados, sin repetir.
/// </summary>
public class EquipoGuardado : DocumentoAlmacen
{
    [BsonElement("equipo_id")]
    public int EquipoId { get; set; }

    [BsonElement("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [BsonElement("pais")]
    public string? Pais { get; set; }

    [BsonElement("ligas")]
    public List<string> Ligas { get; set; } = new();

    [BsonElement("partidos")]
    public int Partidos { get; set; }

    [BsonElement("goles")]
    public int Goles { get; set; }

    [BsonElement("ultima_vez")]
    public DateTime? UltimaVez { get; set; }

    [BsonElement("actualizado")]
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección ResumenDiario: una fila por día con partidos, goles y ligas.
/// Alimenta el "resumen del día" sin recorrer PartidosFm.
/// </summary>
public class ResumenDia : DocumentoAlmacen
{
    [BsonElement("dia")]
    public string Dia { get; set; } = string.Empty;

    [BsonElement("partidos")]
    public int Partidos { get; set; }

    [BsonElement("goles")]
    public int Goles { get; set; }

    [BsonElement("en_vivo")]
    public int EnVivo { get; set; }

    [BsonElement("ligas")]
    public int Ligas { get; set; }

    [BsonElement("actualizado")]
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección ConsultasApi: registro de cada petición al puente de datos.
/// Se borra sola a los 7 días (índice TTL).
/// </summary>
public class ConsultaApi : DocumentoAlmacen
{
    [BsonElement("ruta")]
    public string Ruta { get; set; } = string.Empty;

    [BsonElement("consulta")]
    public string Consulta { get; set; } = string.Empty;

    [BsonElement("estado")]
    public int Estado { get; set; }

    [BsonElement("ms")]
    public long Ms { get; set; }

    [BsonElement("fecha")]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

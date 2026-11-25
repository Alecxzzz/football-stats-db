using FootballStats.Domain;

namespace FootballStats.API.Dtos;

/// <summary>
/// DTO para la página de estadísticas de futbol.
/// Representa un partido para mostrar en la interfaz.
/// </summary>
public class PartidoDto
{
    public required string Id { get; set; }
    public required string? IdSellado { get; set; }
    public required string? Liga { get; set; }
    public required string? EquipoLocal { get; set; }
    public required string? EquipoVisitante { get; set; }
    public required string? Marcador { get; set; }
    public required DateTime Fecha { get; set; }
    public required string FechaMostrar { get; set; } = string.Empty;
    public required string Hora { get; set; } = string.Empty;
    public required bool EnPlazamiento { get; set; }
    public required bool EnTransmision { get; set; }
    public required bool Jugando { get; set; }
    public required bool Terminado { get; set; }
    public required string? LigaId { get; set; }
    public required string? LigaNombre { get; set; }
    public required bool EnPlazamientoMostrar { get; set; }
}

/// <summary>
/// DTO para el listado de ligas.
/// </summary>
public class LigaDto
{
    public required string Id { get; set; }
    public required string? Nombre { get; set; }
    public required string? Pais { get; set; }
}

/// <summary>
/// DTO para el resumen de estadísticas (agrupación por día).
/// </summary>
public class ResumenDiarioDto
{
    public required string Fecha { get; set; }
    public required int TotalPartidos { get; set; }
    public required int EnPlazamiento { get; set; }
    public required int EnTransmision { get; set; }
    public required int Terminados { get; set; }
}

/// <summary>
/// DTO para el listado de categorías (adaptado para futbol).
/// </summary>
public class CategoriaDto
{
    public required string Id { get; set; }
    public required string Nombre { get; set; }
}

/// <summary>
/// DTO para la tabla de partidos (tabla clasificación).
/// </summary>
public class PartidoTablaDto
{
    public required string Id { get; set; }
    public required string Liga { get; set; }
    public required string EquipoLocal { get; set; }
    public required string EquipoVisitante { get; set; }
    public required string Marcador { get; set; }
    public required string Fecha { get; set; }
    public required string Hora { get; set; }
    public required string Estado { get; set; }
    public required bool EnPlazamiento { get; set; }
}

/// <summary>
/// DTO para el reporte de resultados.
/// </summary>
public class ReporteResultadosDto
{
    public required string FechaDesde { get; set; }
    public required string FechaHasta { get; set; }
    public required int TotalPartidos { get; set; }
    public required int Terminados { get; set; }
    public required int EnVivo { get; set; }
    public required int Aplazados { get; set; }
}

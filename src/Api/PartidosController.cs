using Microsoft.AspNetCore.Mvc;
using FootballStats.Domain;
using FootballStats.Infrastructure;
using FootballStats.API.Dtos;

namespace FootballStats.API.Controllers;

/// <summary>
/// API para la página de estadísticas de futbol.
/// Permite consultar partidos de ayer, hoy y mañana, listar ligas,
/// y obtener reportes por fechas.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public partial class PartidosController : ControllerBase
{
    private readonly PartidoRepository _partidoRepository;
    private readonly ILogger<PartidosController> _logger;

    public PartidosController(PartidoRepository partidoRepository, ILogger<PartidosController> logger)
    {
        _partidoRepository = partidoRepository;
        _logger = logger;
    }

    /// <summary>
    /// GET: api/partidos - Listado de partidos [ayer, hoy, mañana].
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PartidoDto>>> GetPartidosAsync([FromQuery] DateTime? fechaBase)
    {
        try
        {
            var baseFecha = fechaBase ?? DateTime.Today;
            var partidos = await _partidoRepository.GetPartidosDiariosAsync(baseFecha);

            var dtoList = partidos.Select(p => new PartidoDto
            {
                Id = p.Id ?? string.Empty,
                IdSellado = p.IdSellado,
                Liga = p.Liga,
                EquipoLocal = p.EquipoLocal,
                EquipoVisitante = p.EquipoVisitante,
                Marcador = p.Marcador is not null
                    ? $"{p.Marcador.Local} - {p.Marcador.Visitante}"
                    : p.Resultado,
                Fecha = p.Fecha,
                FechaMostrar = p.Fecha.ToShortDateString(),
                Hora = p.Hora.ToString(@"hh\:mm"),
                EnPlazamiento = p.Enplazamiento,
                EnTransmision = p.EnTransmision,
                Jugando = p.Jugando,
                Terminado = p.Terminado,
                LigaId = p.LigaId,
                LigaNombre = p.LigaNombre,
                EnPlazamientoMostrar = p.Enplazamiento || p.EnTransmision || p.Jugando || p.EnPausa
            });

            return Ok(dtoList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener partidos");
            return StatusCode(500, new { mensaje = "Error interno del servidor", error = ex.Message });
        }
    }
}

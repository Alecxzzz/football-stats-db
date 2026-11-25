using Microsoft.AspNetCore.Mvc;
using FootballStats.Domain;
using FootballStats.Infrastructure;
using FootballStats.API.Dtos;

namespace FootballStats.API.Controllers;

public partial class PartidosController : ControllerBase
{
    [HttpGet("reporte")]
    public async Task<ActionResult<ReporteResultadosDto>> GetReporteAsync(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta)
    {
        try
        {
            var partidos = await _partidoRepository.GetByDateRangeAsync(fechaDesde, fechaHasta);

            var total = partidos.Count();
            var terminados = partidos.Count(p => p.Terminado);
            var enVivo = partidos.Count(p => p.Jugando || p.EnTransmision);
            var aplazados = partidos.Count(p => p.Enplazamiento && !p.Terminado && !p.Jugando && !p.EnTransmision);

            return Ok(new ReporteResultadosDto
            {
                FechaDesde = fechaDesde.ToShortDateString(),
                FechaHasta = fechaHasta.ToShortDateString(),
                TotalPartidos = total,
                Terminados = terminados,
                EnVivo = enVivo,
                Aplazados = aplazados
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener reporte");
            return StatusCode(500, new { mensaje = "Error interno", error = ex.Message });
        }
    }

    [HttpGet("estadisticas/por-dia")]
    public async Task<ActionResult<IEnumerable<ResumenDiarioDto>>> GetResumenPorDiaAsync(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta)
    {
        try
        {
            var partidos = await _partidoRepository.GetByDateRangeAsync(fechaDesde, fechaHasta);

            var grupo = partidos.GroupBy(p => p.Fecha.Date)
                .Select(g => new ResumenDiarioDto
                {
                    Fecha = g.Key.ToShortDateString(),
                    TotalPartidos = g.Count(),
                    EnPlazamiento = g.Count(p => p.Enplazamiento && !p.Terminado),
                    EnTransmision = g.Count(p => p.Jugando || p.EnTransmision),
                    Terminados = g.Count(p => p.Terminado)
                });

            return Ok(grupo.OrderBy(r => r.Fecha));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estadísticas por día");
            return StatusCode(500, new { mensaje = "Error interno", error = ex.Message });
        }
    }
}

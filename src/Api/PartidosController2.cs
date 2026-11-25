using Microsoft.AspNetCore.Mvc;
using FootballStats.Domain;
using FootballStats.Infrastructure;
using FootballStats.API.Dtos;

namespace FootballStats.API.Controllers;

public partial class PartidosController : ControllerBase
{
    [HttpGet("rango")]
    public async Task<ActionResult<IEnumerable<PartidoDto>>> GetPartidosByDateRangeAsync(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta)
    {
        try
        {
            var partidos = await _partidoRepository.GetByDateRangeAsync(fechaDesde, fechaHasta);
            return Ok(partidos.Select(p => new PartidoDto
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
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener partidos por rango");
            return StatusCode(500, new { mensaje = "Error interno", error = ex.Message });
        }
    }

    [HttpGet("live")]
    public async Task<ActionResult<IEnumerable<PartidoDto>>> GetPartidosEnVivoAsync()
    {
        try
        {
            var partidos = await _partidoRepository.GetPartidosEnVivoAsync();
            return Ok(partidos.Select(p => new PartidoDto
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
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener partidos en vivo");
            return StatusCode(500, new { mensaje = "Error interno", error = ex.Message });
        }
    }

    [HttpGet("ligas")]
    public async Task<ActionResult<IEnumerable<Liga>>> GetLigasAsync()
    {
        try
        {
            return Ok(await _partidoRepository.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener ligas");
            return StatusCode(500, new { mensaje = "Error interno", error = ex.Message });
        }
    }
}

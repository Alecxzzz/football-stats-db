using MongoDB.Driver;
using FootballStats.Domain;
using FootballStats.Infrastructure;

namespace FootballStats.Seed;

public partial class SeedData
{
    private async Task SeedPedidos()
    {
        var pedidosExistentes = _context.Pedidos.CountDocuments(FilterDefinition<Pedido>.Empty);
        if (pedidosExistentes > 0)
        {
            Console.WriteLine("[pedidos] Ya existen registros, saltando...");
            return;
        }

        var usuarios = await _context.Usuarios.Find(FilterDefinition<Usuario>.Empty).ToListAsync();
        var productos = await _context.Productos.Find(FilterDefinition<Producto>.Empty).ToListAsync();
        var usuarioId = usuarios.FirstOrDefault(u => u.Rol == UserRole.Administrador)?.Id ?? string.Empty;
        var productoId1 = productos.FirstOrDefault(p => p.Nombre.Contains("Boleto"))?.Id ?? string.Empty;
        var productoId2 = productos.FirstOrDefault(p => p.Nombre.Contains("Pase"))?.Id ?? string.Empty;
        var productoId3 = productos.FirstOrDefault(p => p.Nombre.Contains("Set"))?.Id ?? string.Empty;

        var pedidos = new List<Pedido>
        {
            new Pedido { ReferenciaUsuario = usuarioId, ReferenciaProducto = productoId1, Fecha = DateTime.UtcNow.AddDays(-15), Estado = PedidoEstado.Completado, Total = 45.00m, MetodoPago = "Tarjeta Credito", Ubicacion = "Calle Mayor 123, Madrid", Observaciones = "Pedido normal", CreadoEn = DateTime.UtcNow.AddDays(-15), ActualizadoEn = DateTime.UtcNow.AddDays(-14) },
            new Pedido { ReferenciaUsuario = usuarioId, ReferenciaProducto = productoId2, Fecha = DateTime.UtcNow.AddDays(-10), Estado = PedidoEstado.Completado, Total = 250.00m, MetodoPago = "Transferencia", Ubicacion = "Calle Mayor 123, Madrid", Observaciones = "Pago previo confirmado", CreadoEn = DateTime.UtcNow.AddDays(-10), ActualizadoEn = DateTime.UtcNow.AddDays(-9) },
            new Pedido { ReferenciaUsuario = usuarioId, ReferenciaProducto = productoId3, Fecha = DateTime.UtcNow.AddDays(-5), Estado = PedidoEstado.Enviado, Total = 12.50m, MetodoPago = "Tarjeta Credito", Ubicacion = "Calle Mayor 123, Madrid", Observaciones = "Envío prioritario", CreadoEn = DateTime.UtcNow.AddDays(-5), ActualizadoEn = DateTime.UtcNow.AddDays(-4) },
            new Pedido { ReferenciaUsuario = usuarios[1].Id, ReferenciaProducto = productoId1, Fecha = DateTime.UtcNow.AddDays(-3), Estado = PedidoEstado.Pendiente, Total = 45.00m, MetodoPago = "Paypal", Ubicacion = "Calle Lunares 45, Barcelona", Observaciones = "Nuevo cliente", CreadoEn = DateTime.UtcNow.AddDays(-3), ActualizadoEn = DateTime.UtcNow.AddDays(-3) },
            new Pedido { ReferenciaUsuario = usuarioId, ReferenciaProducto = productoId1, Fecha = DateTime.UtcNow.AddDays(-1), Estado = PedidoEstado.Cancelado, Total = 45.00m, MetodoPago = "Tarjeta Credito", Ubicacion = "Calle Mayor 123, Madrid", Observaciones = "Cancelado por error de pago", CreadoEn = DateTime.UtcNow.AddDays(-1), ActualizadoEn = DateTime.UtcNow.AddDays(-1) }
        };

        foreach (var p in pedidos)
            await _context.Pedidos.InsertOneAsync(p);

        Console.WriteLine($"[pedidos] Insertados {pedidos.Count} pedidos.");
    }

    private async Task SeedInventario()
    {
        var inventarioExistentes = _context.Inventarios.CountDocuments(FilterDefinition<Inventario>.Empty);
        if (inventarioExistentes > 0)
        {
            Console.WriteLine("[inventario] Ya existen registros, saltando...");
            return;
        }

        var productos = await _context.Productos.Find(FilterDefinition<Producto>.Empty).ToListAsync();
        var productosIds = productos.Select(p => p.Id ?? string.Empty).ToList();
        var productoId1 = productosIds.Count > 0 ? productosIds[0] : string.Empty;
        var productoId2 = productosIds.Count > 1 ? productosIds[1] : string.Empty;

        var inventario = new List<Inventario>
        {
            new Inventario { ReferenciaProducto = productoId1, TipoMovimiento = TipoMovimiento.Entrada, Cantidad = 100, StockAnterior = 150, StockNuevo = 250, Fecha = DateTime.UtcNow.AddDays(-10), UsuarioResponsable = "Carlos García", Observaciones = "Reposición completa de stock", CreadoEn = DateTime.UtcNow.AddDays(-10) },
            new Inventario { ReferenciaProducto = productoId1, TipoMovimiento = TipoMovimiento.Salida, Cantidad = 30, StockAnterior = 250, StockNuevo = 220, Fecha = DateTime.UtcNow.AddDays(-5), UsuarioResponsable = "Ana Martínez", Observaciones = "Venta online", CreadoEn = DateTime.UtcNow.AddDays(-5) },
            new Inventario { ReferenciaProducto = productoId2, TipoMovimiento = TipoMovimiento.Entrada, Cantidad = 20, StockAnterior = 0, StockNuevo = 20, Fecha = DateTime.UtcNow.AddDays(-3), UsuarioResponsable = "Carlos García", Observaciones = "Nuevo producto añadido", CreadoEn = DateTime.UtcNow.AddDays(-3) },
            new Inventario { ReferenciaProducto = productoId2, TipoMovimiento = TipoMovimiento.Ajuste, Cantidad = 5, StockAnterior = 20, StockNuevo = 15, Fecha = DateTime.UtcNow.AddDays(-2), UsuarioResponsable = "Roberto Hernández", Observaciones = "Ajuste contable por daños", CreadoEn = DateTime.UtcNow.AddDays(-2) },
            new Inventario { ReferenciaProducto = productoId1, TipoMovimiento = TipoMovimiento.Salida, Cantidad = 15, StockAnterior = 220, StockNuevo = 205, Fecha = DateTime.UtcNow.AddDays(-1), UsuarioResponsable = "Juan Pérez", Observaciones = "Pedido de cliente", CreadoEn = DateTime.UtcNow.AddDays(-1) },
            new Inventario { ReferenciaProducto = productoId3, TipoMovimiento = TipoMovimiento.Auditoria, Cantidad = 0, StockAnterior = 500, StockNuevo = 500, Fecha = DateTime.UtcNow.AddDays(-1), UsuarioResponsable = "Carlos García", Observaciones = "Auditoría interna de stock", CreadoEn = DateTime.UtcNow.AddDays(-1) }
        };

        foreach (var i in inventario)
            await _context.Inventarios.InsertOneAsync(i);

        Console.WriteLine($"[inventario] Insertadas {inventario.Count} movimientos.");
    }
}

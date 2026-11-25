namespace FootballStats.Domain;

/// <summary>
/// Petición para crear un usuario.
/// </summary>
public class CrearUsuarioRequest
{
    public required string Nombre { get; set; }
    public required string Apellido { get; set; }
    public required string Email { get; set; }
    public required string Telefono { get; set; }
    public UserRole Rol { get; set; } = UserRole.Cliente;
    public required string Direccion { get; set; }
    public required string Ciudad { get; set; }
    public required string Pais { get; set; }
}

/// <summary>
/// Respuesta partial de usuario.
/// </summary>
public class UsuarioResponse
{
    public required string Id { get; set; }
    public required string Nombre { get; set; }
    public required string Apellido { get; set; }
    public required string Email { get; set; }
    public required string Telefono { get; set; }
    public UserRole Rol { get; set; }
    public bool Activo { get; set; }
}

/// <summary>
/// Petición para crear una categoría.
/// </summary>
public class CrearCategoriaRequest
{
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public string? Icono { get; set; }
    public int Orden { get; set; } = 0;
}

/// <summary>
/// Respuesta partial de categoría.
/// </summary>
public class CategoriaResponse
{
    public required string Id { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public string? Icono { get; set; }
    public bool Activa { get; set; }
    public int Orden { get; set; }
}

/// <summary>
/// Petición para crear un producto.
/// </summary>
public class CrearProductoRequest
{
    public required string ReferenciaCategoria { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public decimal PrecioUnitario { get; set; }
    public int Stock { get; set; } = 0;
    public int StockMinimo { get; set; } = 5;
    public string? Imagen { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Respuesta partial de producto.
/// </summary>
public class ProductoResponse
{
    public required string Id { get; set; }
    public required string ReferenciaCategoria { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public decimal PrecioUnitario { get; set; }
    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public string? Imagen { get; set; }
    public bool Activo { get; set; }
    public DateTime CreadoEn { get; set; }
}

/// <summary>
/// Petición para crear un pedido.
/// </summary>
public class CrearPedidoRequest
{
    public required string ReferenciaUsuario { get; set; }
    public string? ReferenciaProducto { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public PedidoEstado Estado { get; set; } = PedidoEstado.Pendiente;
    public decimal Total { get; set; }
    public required string MetodoPago { get; set; }
    public string? Ubicacion { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// Respuesta partial de pedido.
/// </summary>
public class PedidoResponse
{
    public required string Id { get; set; }
    public required string ReferenciaUsuario { get; set; }
    public string? ReferenciaProducto { get; set; }
    public DateTime Fecha { get; set; }
    public PedidoEstado Estado { get; set; }
    public decimal Total { get; set; }
    public required string MetodoPago { get; set; }
    public string? Ubicacion { get; set; }
}

/// <summary>
/// Petición para crear un movimiento de inventario.
/// </summary>
public class CrearInventarioRequest
{
    public required string ReferenciaProducto { get; set; }
    public TipoMovimiento TipoMovimiento { get; set; }
    public required int Cantidad { get; set; }
    public int StockAnterior { get; set; }
    public int StockNuevo { get; set; }
    public required string UsuarioResponsable { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// Respuesta partial de inventario.
/// </summary>
public class InventarioResponse
{
    public required string Id { get; set; }
    public required string ReferenciaProducto { get; set; }
    public TipoMovimiento TipoMovimiento { get; set; }
    public required int Cantidad { get; set; }
    public int StockAnterior { get; set; }
    public int StockNuevo { get; set; }
    public DateTime Fecha { get; set; }
    public required string UsuarioResponsable { get; set; }
    public string? Observaciones { get; set; }
}

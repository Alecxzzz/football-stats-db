using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FootballStats.Domain;

/// <summary>
/// Referencia a un documento de MongoDB (ObjectId).
/// </summary>
public class MongoId
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
}

/// <summary>
/// Rol del usuario en el sistema.
/// </summary>
public enum UserRole
{
    Administrador,
   jandroCreador,
    Cliente,
    SuperUsuario
}

/// <summary>
/// Colección: usuarios / clientes
/// Almacena perfiles, roles, contacto e historial.
/// </summary>
public class Usuario : MongoId
{
    [BsonElement("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [BsonElement("apellido")]
    public string Apellido { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("telefono")]
    public string Telefono { get; set; } = string.Empty;

    [BsonElement("rol")]
    public UserRole Rol { get; set; } = UserRole.Cliente;

    [BsonElement("activo")]
    public bool Activo { get; set; } = true;

    [BsonElement("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    [BsonElement("ultima_conexion")]
    public DateTime? UltimaConexion { get; set; }

    [BsonElement("direccion")]
    public string Direccion { get; set; } = string.Empty;

    [BsonElement("ciudad")]
    public string Ciudad { get; set; } = string.Empty;

    [BsonElement("pais")]
    public string Pais { get; set; } = string.Empty;
}

/// <summary>
/// Colección: categorías
/// Define los rubros o clasificaciones de productos/servicios.
/// </summary>
public class Categoria : MongoId
{
    [BsonElement("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [BsonElement("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [BsonElement("icono")]
    public string Icono { get; set; } = string.Empty;

    [BsonElement("activa")]
    public bool Activa { get; set; } = true;

    [BsonElement("orden")]
    public int Orden { get; set; }

    [BsonElement("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Colección: productos / artículos
/// Contiene el detalle de los ítems con referencia a categoría.
/// </summary>
public class Producto : MongoId
{
    [BsonElement("referencia_categoria")]
    public string? ReferenciaCategoria { get; set; }

    [BsonElement("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [BsonElement("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [BsonElement("precio_unitario")]
    public decimal PrecioUnitario { get; set; }

    [BsonElement("stock")]
    public int Stock { get; set; }

    [BsonElement("stock_minimo")]
    public int StockMinimo { get; set; }

    [BsonElement("imagen")]
    public string? Imagen { get; set; }

    [BsonElement("activo")]
    public bool Activo { get; set; } = true;

    [BsonElement("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    [BsonElement("actualizado_en")]
    public DateTime? ActualizadoEn { get; set; }
}

/// <summary>
/// Colección: pedidos / transacciones
/// Registra operaciones con fecha, estado, total y usuario.
/// </summary>
public class Pedido : MongoId
{
    [BsonElement("referencia_usuario")]
    public string? ReferenciaUsuario { get; set; }

    [BsonElement("referencia_producto")]
    public string? ReferenciaProducto { get; set; }

    [BsonElement("fecha")]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [BsonElement("estado")]
    public PedidoEstado Estado { get; set; } = PedidoEstado.Pendiente;

    [BsonElement("total")]
    public decimal Total { get; set; }

    [BsonElement("metodo_pago")]
    public string MetodoPago { get; set; } = string.Empty;

    [BsonElement("ubicacion")]
    public string? Ubicacion { get; set; }

    [BsonElement("observaciones")]
    public string Observaciones { get; set; } = string.Empty;

    [BsonElement("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    [BsonElement("actualizado_en")]
    public DateTime? ActualizadoEn { get; set; }
}

public enum PedidoEstado
{
    Pendiente,
    Enviado,
    Completado,
    Cancelado,
    Rechazado
}

/// <summary>
/// Colección: inventario / movimientos
/// Registra entradas, salidas y auditorías de mercancía.
/// </summary>
public class Inventario : MongoId
{
    [BsonElement("referencia_producto")]
    public string? ReferenciaProducto { get; set; }

    [BsonElement("tipo_movimiento")]
    public TipoMovimiento TipoMovimiento { get; set; }

    [BsonElement("cantidad")]
    public int Cantidad { get; set; }

    [BsonElement("stock_anterior")]
    public int StockAnterior { get; set; }

    [BsonElement("stock_nuevo")]
    public int StockNuevo { get; set; }

    [BsonElement("fecha")]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [BsonElement("usuario_responsable")]
    public string? UsuarioResponsable { get; set; }

    [BsonElement("observaciones")]
    public string? Observaciones { get; set; }

    [BsonElement("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public enum TipoMovimiento
{
    Entrada,
    Salida,
    Auditoria,
    Ajuste
}

# 3SIXTYBETS - Estadísticas de Fútbol

## Resumen del Proyecto

Este proyecto consiste en dos partes:

1. **Página de Estadísticas de Fútbol** - Una página de estadísticas de partidos de fútbol (ayer, hoy y mañana) añadida al sitio web existente **3SIXTYBETS**.

## 1. Página de Estadísticas de Fútbol

### Técnologías
- **Frontend**: React 19 + Vite 8 + Tailwind CSS v4
- **Lenguaje**: JavaScript (JSX)
- **API**: Fetch API (consumida desde el backend ASP.NET Core)
- **Estilos**: Tailwind CSS (mismos estilos que la app existente)

## 2. Base de Datos MongoDB con C#/.NET

### Tecnologías
- **Lenguaje**: C#
- **Framework**: .NET 10.0 (SDK compatible con net10.0)
- **Gestor de BBDD**: MongoDB (MongoDB.Driver 3.0.0)

## 3. Colecciones de la Base de Datos

La base de datos `threesixtybets_db` contiene **7 colecciones** relacionadas mediante referencias ObjectId:

### 3.1 Usuarios (usuarios / clientes)
| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | ObjectId | Identificador único |
| Nombre | string | Nombre del usuario |
| Apellido | string | Apellido |
| Email | string | Correo electrónico |
| Telefono | string | Teléfono de contacto |
| Rol | UserRole | Rol (Administrador, Cliente, Creador, SuperUsuario) |
| Activo | bool | Estado de actividad |
| Direccion | string | Dirección de envío |
| Ciudad | string | Ciudad |

## 4. Operaciones CRUD

### Crear (Insert)
```csharp
// Insertar un usuario
var usuario = new Usuario 
{ 
    Nombre = "Carlos", 
    Apellido = "García", 
    Email = "carlos@empresa.com",
    Rol = UserRole.Administrador,
    Activo = true,
    Direccion = "Calle Mayor 123",
    Ciudad = "Madrid",
    Pais = "España"

## 5. Operadores de Agregación

### 5.1 Consulta de resumen por día
```csharp
// Agrupar partidos por fecha
var resumen = partidos
    .GroupBy(p => p.Fecha.Date)

## 6. Endpoints de la API

### PartidosController
| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/partidos` | Listado de partidos para ayer, hoy y mañana |
| GET | `/api/partidos/rango` | Partidos por rango de fechas |
| GET | `/api/partidos/live` | Partidos en vivo o en plena transmisión |
| GET | `/api/partidos/ligas` | Lista de ligas disponibles |
| GET | `/api/partidos/reporte` | Reporte de resultados por rango de fechas |
| GET | `/api/partidos/estadisticas/por-dia` | Resumen de partidos por día |


## 8. Requerimientos Funcionales Cumplidos

| CRUD | Descripción | Estado |
|------|-------------|--------|
| **Crear** | Inserción de usuarios, productos, pedidos e inventarios | ✅ |
| **Crear** | Inserción de partidos de fútbol (ayer, hoy, mañana) | ✅ |
| **Leer** | Listados generales con filtros | ✅ |
| **Leer** | Consulta de partidos por fecha | ✅ |
| **Leer** | Operadores de agregación ($lookup, $match, $group) | ✅ |
| **Actualizar** | Modificación de stock y estado de pedidos | ✅ |
| **Actualizar** | Cambio de estado de partido (Pendiente → Completado) | ✅ |
| **Eliminar** | Borrado físico y lógico de registros | ✅ |

## 9. Entregables

| Entregable | Estado |
|-----------|--------|
| Documentación técnica | ✅ Markdown + puede convertirse a PDF |
| Script con 7 colecciones y 10+ registros | ✅ (10 partidos + 8 usuarios + ...) |
| Aplicación en vivo con CRUD completo | ✅ API ASP.NET Core + React |

## 10. Cómo ejecutar el proyecto

### Backend (.NET)
```bash
cd tresixtybets-football-stats/src/Api
dotnet run --project FootballStats.Api.csproj
```

### Frontend (React)
```bash
cd C:\Users\usuario\Desktop\3\threesixtybets-chat
npm install
npm run dev
```

---

*Documento generado: 2026-11-20*

## 7. Semilla (Seed)

El proyecto incluye un script de seed (`SeedData.cs`) que genera más de 10 registros por colección:

| Colección | Registros generados |
|-----------|---------------------|
| usuarios | 8 |
| categorias | 6 |
| productos | 5 |
| pedidos | 5 |
| inventario | 6 |
| partidos | 10 |
| ligas | 5 |
| cuotas | 5 |

Para ejecutar el seed:
```bash
cd src/Api/SeedProject
dotnet run
```

---

    .Select(g => new ResumenDiarioDto
    {
        Fecha = g.Key.ToShortDateString(),
        TotalPartidos = g.Count(),
        EnPlazamiento = g.Count(p => p.Enplazamiento && !p.Terminado),
        EnTransmision = g.Count(p => p.Jugando || p.EnTransmision),
        Terminados = g.Count(p => p.Terminado)
    })
    .OrderBy(r => r.Fecha)
    .ToList();
```

### 5.2 Consulta con filtrado avanzado
```csharp
// Partidos en vivo utilizando filtros compuestos
var filtroEnVivo = Builders<Partido>.Filter.And(
    Builders<Partido>.Filter.Eq(p => p.Activo, true),
    Builders<Partido>.Filter.Where(p => p.Jugando || p.EnTransmision || p.EnPausa)
);
var partidos = await _context.Partidos.Find(filtroEnVivo).ToListAsync();
```

### 5.3 Concatenación de colecciones con $lookup
```csharp
// Unir partidos con ligas (equivalente a $lookup en SQL)
var pipeline = new EmptyPipelineDefinition<Partido>()
    .Match(Builders<Partido>.Filter.Eq(p => p.Activo, true))
    .Lookup<Partido, Liga, PartidoConLiga>(
        _context.Ligas,
        p => p.LigaId,
        l => l.Id,
        p => p.Liga
    );
```

};
await _context.Usuarios.InsertOneAsync(usuario);

// Insertar un partido
var partido = new Partido
{
    IdSellado = "10023450",
    Liga = "Premier League",
    EquipoLocal = "Manchester City",
    EquipoVisitante = "Arsenal",
    Resultado = "1 - 2",
    Fecha = DateTime.Parse("2026-11-19T18:30:00"),
    Hora = TimeSpan.Parse("18:30"),
    Enplazamiento = true,
    Terminado = true,
    Finalizado = true
};
await _context.Partidos.InsertOneAsync(partido);
```

### Leer (Query)
```csharp
// Obtener todos los usuarios
var usuarios = await _context.Usuarios.Find(Builders<Usuario>.Filter.Empty).ToListAsync();

// Obtener partidos por fecha
var partidos = await _repo.GetPartidosDiariosAsync(fechaBase);

// Obtener partidos en vivo
var partidosEnVivo = await _repo.GetPartidosEnVivoAsync();
```

### Actualizar (Update)
```csharp
// Actualizar estatus de un partido
var filtro = Builders<Partido>.Filter.Eq(p => p.IdSellado, "10023450");
var update = Builders<Partido>.Update.Set(p => p.Terminado, true);
await _context.Partidos.UpdateOneAsync(filtro, update);

// Actualizar stock de un producto
var filtroProducto = Builders<Producto>.Filter.Eq(p => p.Id, productoId);
var updateStock = Builders<Producto>.Update.Set(p => p.Stock, nuevoStock);
await _context.Productos.UpdateOneAsync(filtroProducto, updateStock);
```

### Eliminar
```csharp
// Borrado físico
await _context.Partidos.DeleteOneAsync(Builders<Partido>.Filter.Eq(p => p.IdSellado, "10023450"));

// Borrado lógico (cambiar estado a inactivo)
await _context.Usuarios.UpdateOneAsync(
    Builders<Usuario>.Filter.Eq(u => u.Email, "carlos@empresa.com"),
    Builders<Usuario>.Update.Set(u => u.Activo, false)
);
```

| Pais | string | País |
| CreadoEn | DateTime | Fecha de creación |

### 3.2 Categorías (categorías)
| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | ObjectId | Identificador único |
| Nombre | string | Nombre de la categoría |
| Descripcion | string | Descripción |
| Icono | string | Icono (Unicode) |
| Activa | bool | Estado de actividad |
| Orden | int | Orden de visualización |
| CreadoEn | DateTime | Fecha de creación |

### 3.3 Productos (productos / artículos)
| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | ObjectId | Identificador único |
| ReferenciaCategoria | ObjectId | Referencia a Categoria |
| Nombre | string | Nombre del producto |
| PrecioUnitario | decimal | Precio unitario |
| Stock | int | Stock disponible |
| StockMinimo | int | Stock mínimo |
| Activo | bool | Estado de actividad |
| CreadoEn | DateTime | Fecha de creación |

### 3.4 Pedidos (pedidos / transacciones)
| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | ObjectId | Identificador único |
| ReferenciaUsuario | ObjectId | Referencia a Usuario |
| ReferenciaProducto | ObjectId | Referencia a Producto |
| Fecha | DateTime | Fecha del pedido |
| Estado | PedidoEstado | Estado (Pendiente, Enviado, Completado, Cancelado) |
| Total | decimal | Total del pedido |
| MetodoPago | string | Método de pago |
| CreadoEn | DateTime | Fecha de creación |

### 3.5 Inventario (inventario / movimientos)
| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | ObjectId | Identificador único |
| ReferenciaProducto | ObjectId | Referencia a Producto |
| TipoMovimiento | TipoMovimiento | Tipo (Entrada, Salida, Auditoria, Ajuste) |
| Cantidad | int | Cantidad del movimiento |
| StockAnterior | int | Stock anterior |
| StockNuevo | int | Stock nuevo |
| Fecha | DateTime | Fecha del movimiento |
| UsuarioResponsable | string | Usuario responsable |

### 3.6 Partidos (partidos de futbol) ⭐
| Campo | Tipo | Descripción |
|-------|------|-------------|
| Id | ObjectId | Identificador único |
| IdSellado | string | ID sellado del partido |
| Temporada | string | Temporada (ej: 2026-2027) |
| Liga | string | Nombre de la liga |
| EquipoLocal | string | Equipo local |
| EquipoVisitante | string | Equipo visitante |
| Resultado | string | Resultado (ej: 1-2) |
| Marcador | Marcador | Objeto con goles local y visitante |
| Fecha | DateTime | Fecha del partido |
| Hora | TimeSpan | Hora del partido |
| LigaId | string | ID de la liga |
| LigaNombre | string | Nombre de la liga |
| Enplazamiento | bool | En plazamiento |
| EnTransmision | bool | En transmisión |
| Jugando | bool | En pleno juego |
| Terminado | bool | Terminado |

### 3.7 Ligas (ligas / torneos) + Cuotas (cuotas / odds)
| Campo | Tipo | Descripción |
|-------|------|-------------|
| Liga.Id | ObjectId | Identificador única |
| Liga.Nombre | string | Nombre de la liga |
| Liga.Codigo | string | Código (PL, LA, SA, BL) |
| Liga.Pais | string | País |

| Campo | Tipo | Descripción |
|-------|------|-------------|
| Cuota.Id | ObjectId | Identificador único |
| Cuota.ReferenciaPartido | ObjectId | Referencia a Partido |
| Cuota.TipoCuota | string | Tipo (1X2, Double Chance, Over/Under) |
| Cuota.CuotaValor | decimal | Valor de la cuota |
| Cuota.FechaActualizacion | DateTime | Fecha de actualización |

- **Patrón**: Repository + Unit of Work
- **API**: ASP.NET Core Web API 7.0+

### Estructura del Proyecto
```
threesixtybets-football-stats/
├── src/
│   ├── Api/                          # Proyecto Web API
│   ├── Domain/                       # Modelos de dominio
│   ├── Infrastructure/               # Acceso a datos
│   └── SeedProject/                  # Proyecto de Seed
└── docs/
    └── README.md
```


### Funcionalidades
- **Vista de partidos por fecha**: Ayer, Hoy y Mañana
- **Filtros**: Todos, En vivo, Terminados
- **Mostrar marcador**: Résultados de partidos finalizados
- **Indicadores de estado**: Programado, Plazamiento, En vivo, Terminado
- **Datos en tiempo real**: Los partidos se actualizan según la fecha seleccionada

### Cómo funcionar
1. Abre el proyecto en VS Code
2. Instala las dependencias: `npm install`
3. Inicia el servidor de desarrollo: `npm run dev`
4. La página está disponible en `http://localhost:5173/futbol`

2. **Base de Datos MongoDB con C#/.NET** - Una base de datos NoSQL con MongoDB que contiene 7 colecciones relacionadas mediante referencias ObjectId, con operaciones CRUD completas.

---

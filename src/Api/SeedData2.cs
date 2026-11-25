using MongoDB.Driver;
using FootballStats.Domain;
using FootballStats.Infrastructure;

namespace FootballStats.Seed;

public partial class SeedData
{
    private async Task SeedCategorias()
    {
        var categoriasExistentes = _context.Categorias.CountDocuments(FilterDefinition<Categoria>.Empty);
        if (categoriasExistentes > 0)
        {
            Console.WriteLine("[categorias] Ya existen registros, saltando...");
            return;
        }

        var categorias = new List<Categoria>
        {
            new Categoria { Nombre = "Fútbol", Descripcion = "Partidos y estadísticas de fútbol", Icono = "⚽", Orden = 1, Activa = true, CreadoEn = DateTime.UtcNow },
            new Categoria { Nombre = "Baloncesto", Descripcion = "Partidos y estadísticas de baloncesto", Icono = "🏀", Orden = 2, Activa = true, CreadoEn = DateTime.UtcNow },
            new Categoria { Nombre = "Tenis", Descripcion = "Partidos y estadísticas de tenis", Icono = "🎾", Orden = 3, Activa = true, CreadoEn = DateTime.UtcNow },
            new Categoria { Nombre = "Ciclismo", Descripcion = "Estadísticas de ciclismo", Icono = "🚴", Orden = 4, Activa = true, CreadoEn = DateTime.UtcNow },
            new Categoria { Nombre = "Fútbol Americano", Descripcion = "Estadísticas de fútbol americano", Icono = "🏈", Orden = 5, Activa = true, CreadoEn = DateTime.UtcNow },
            new Categoria { Nombre = "Rugby", Descripcion = "Estadísticas de rugby", Icono = "🤼", Orden = 6, Activa = false, CreadoEn = DateTime.UtcNow }
        };

        foreach (var c in categorias)
            await _context.Categorias.InsertOneAsync(c);

        Console.WriteLine($"[categorias] Insertadas {categorias.Count} categorías.");
    }

    private async Task SeedProductos()
    {
        var productosExistentes = _context.Productos.CountDocuments(FilterDefinition<Producto>.Empty);
        if (productosExistentes > 0)
        {
            Console.WriteLine("[productos] Ya existen registros, saltando...");
            return;
        }

        var categorias = await _context.Categorias.Find(FilterDefinition<Categoria>.Empty).ToListAsync();
        var catFutbol = categorias.FirstOrDefault(c => c.Nombre == "Fútbol")?.Id ?? string.Empty;

        var productos = new List<Producto>
        {
            new Producto { ReferenciaCategoria = catFutbol, Nombre = "Boleto Entrada Premier League 2026/27", Descripcion = "Boleto para los partidos del Premier League", PrecioUnitario = 45.00m, Stock = 250, StockMinimo = 20, Imagen = "/img/boleto.png", Activo = true, CreadoEn = DateTime.UtcNow, ActualizadoEn = DateTime.UtcNow },
            new Producto { ReferenciaCategoria = catFutbol, Nombre = "Pase Directo Final 4 Champions League", Descripcion = "Pase directo para cuartos de final", PrecioUnitario = 250.00m, Stock = 15, StockMinimo = 5, Imagen = "/img/pase.png", Activo = true, CreadoEn = DateTime.UtcNow, ActualizadoEn = DateTime.UtcNow },
            new Producto { ReferenciaCategoria = catFutbol, Nombre = "Set de Programación de Jornadas", Descripcion = "Set impreso con la programación completa", PrecioUnitario = 12.50m, Stock = 500, StockMinimo = 30, Imagen = "/img/programacion.jpg", Activo = true, CreadoEn = DateTime.UtcNow, ActualizadoEn = DateTime.UtcNow },
            new Producto { ReferenciaCategoria = catFutbol, Nombre = "Termo de Nudo Deportes", Descripcion = "Termo de colores con logotipo de la plantilla", PrecioUnitario = 18.00m, Stock = 300, StockMinimo = 10, Imagen = "/img/termo.png", Activo = true, CreadoEn = DateTime.UtcNow, ActualizadoEn = DateTime.UtcNow },
            new Producto { ReferenciaCategoria = catFutbol, Nombre = "Camiseta Club Selectiva", Descripcion = "Camiseta de la selección nacional para el mundial 2026", PrecioUnitario = 38.00m, Stock = 120, StockMinimo = 15, Imagen = "/img/camiseta.png", Activo = true, CreadoEn = DateTime.UtcNow, ActualizadoEn = DateTime.UtcNow }
        };

        foreach (var p in productos)
            await _context.Productos.InsertOneAsync(p);

        Console.WriteLine($"[productos] Insertados {productos.Count} productos.");
    }
}

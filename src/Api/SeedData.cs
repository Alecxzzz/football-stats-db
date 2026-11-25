using MongoDB.Driver;
using FootballStats.Domain;
using FootballStats.Infrastructure;

namespace FootballStats.Seed;

public class DbSeeder
{
    private readonly FootballStatsContext _context;

    public DbSeeder(FootballStatsContext context)
    {
        _context = context;
    }

    public async Task ExecuteAsync()
    {
        Console.WriteLine("=== Seeding Football Stats Database ===");
        Console.WriteLine();

        await SeedUsuarios();
        await SeedCategorias();
        await SeedProductos();
        await SeedPedidos();
        await SeedInventario();
        await SeedPartidos();
        await SeedLigas();
        await SeedCuotas();

        Console.WriteLine();
        Console.WriteLine("=== Seeding completed successfully ===");
    }

    private async Task SeedUsuarios()
    {
        var usuariosExistentes = _context.Usuarios.CountDocuments(FilterDefinition<Usuario>.Empty);
        if (usuariosExistentes > 0)
        {
            Console.WriteLine("[usuarios] Ya existen registros, saltando...");
            return;
        }

        var usuarios = new List<Usuario>
        {
            new Usuario { Nombre = "Carlos", Apellido = "García", Email = "carlos@empresa.com", Telefono = "+34 612 345 678", Rol = UserRole.Administrador, Direccion = "Calle Mayor 123", Ciudad = "Madrid", Pais = "España", Activo = true, CreadoEn = DateTime.UtcNow.AddDays(-30) },
            new Usuario { Nombre = "Ana", Apellido = "Martínez", Email = "ana@empresa.com", Telefono = "+34 623 456 789", Rol = UserRole.Cliente, Direccion = "Calle Lunares 45", Ciudad = "Barcelona", Pais = "España", Activo = true, CreadoEn = DateTime.UtcNow.AddDays(-20) },
            new Usuario { Nombre = "Juan", Apellido = "Pérez", Email = "juan@empresa.com", Telefono = "+34 634 567 890", Rol = UserRole.Cliente, Direccion = "Calle Falsa 789", Ciudad = "Valencia", Pais = "España", Activo = true, CreadoEn = DateTime.UtcNow.AddDays(-15) },
            new Usuario { Nombre = "María", Apellido = "López", Email = "maria@empresa.com", Telefono = "+34 645 678 901", Rol = UserRole.SuperUsuario, Direccion = "Calle Alta 101", Ciudad = "Sevilla", Pais = "España", Activo = true, CreadoEn = DateTime.UtcNow.AddDays(-10) },
            new Usuario { Nombre = "Pedro", Apellido = "Rodríguez", Email = "pedro@empresa.com", Telefono = "+34 656 789 012", Rol = UserRole.Creador, Direccion = "Calle Baja 202", Ciudad = "Bilbao", Pais = "España", Activo = true, CreadoEn = DateTime.UtcNow.AddDays(-5) },
            new Usuario { Nombre = "Laura", Apellido = "Sánchez", Email = "laura@empresa.com", Telefono = "+34 667 890 123", Rol = UserRole.Cliente, Direccion = "Calle Centro 303", Ciudad = "Málaga", Pais = "España", Activo = true, CreadoEn = DateTime.UtcNow.AddDays(-3) },
            new Usuario { Nombre = "Roberto", Apellido = "Hernández", Email = "roberto@empresa.com", Telefono = "+34 678 901 234", Rol = UserRole.Creador, Direccion = "Calle Sur 404", Ciudad = "A Coruña", Pais = "España", Activo = true, CreadoEn = DateTime.UtcNow.AddDays(-2) },
            new Usuario { Nombre = "Isabel", Apellido = "Torres", Email = "isabel@empresa.com", Telefono = "+34 689 012 345", Rol = UserRole.Cliente, Direccion = "Calle Oriente 505", Ciudad = "Prisenças", Pais = "España", Activo = true, CreadoEn = DateTime.UtcNow.AddDays(-1) }
        };

        foreach (var u in usuarios)
            await _context.Usuarios.InsertOneAsync(u);

        Console.WriteLine($"[usuarios] Insertados {usuarios.Count} usuarios.");
    }
}

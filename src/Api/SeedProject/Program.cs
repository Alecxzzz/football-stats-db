using FootballStats.Infrastructure;
using FootballStats.Seed;

// Configuración de conexión a MongoDB
var connectionString = "mongodb://localhost:27017";
var databaseName = "threesixtybets_db";

Console.WriteLine("Conectando a MongoDB...");
using var context = new FootballStatsContext(connectionString, databaseName);

// Verificar conexión
try
{
    var count = await context.Usuarios.CountDocumentsAsync(FilterDefinition<Usuario>.Empty);
    Console.WriteLine($"  -usuarios: {count}");
    var countPartidos = await context.Partidos.CountDocumentsAsync(FilterDefinition<Partido>.Empty);
    Console.WriteLine($"  -partidos: {countPartidos}");
    var countCuotas = await context.Cuotas.CountDocumentsAsync(FilterDefinition<Cuota>.Empty);
    Console.WriteLine($"  -cuotas: {countCuotas}");

    if (count == 0)
    {
        Console.WriteLine();
        Console.WriteLine("Empezando seeding...");
        var seeder = new SeedData(context);
        await seeder.ExecuteAsync();
    }
    else
    {
        Console.WriteLine("La base de datos ya contiene datos. Saltando seeding.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error al conectar a MongoDB: {ex.Message}");
    Console.WriteLine("Asegúrese de que MongoDB esté ejecutándose en localhost:27017");
    Console.WriteLine();
    Console.WriteLine("Para poblar la base de datos manualmente, use el script de inserción.");
}

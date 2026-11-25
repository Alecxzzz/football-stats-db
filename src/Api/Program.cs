using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using FootballStats.Infrastructure;

namespace FootballStats.API;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configuration
        var configuration = builder.Configuration;

        // Add services
        builder.Services.AddControllers();

        // CORS para permitir comunicación con el frontend
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Default", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });

        // MongoDB connection
        var mongoConnectionString = configuration.GetConnectionString("Mongo") ?? "mongodb://localhost:27017";
        var databaseName = configuration.GetValue<string>("DatabaseName") ?? "threesixtybets_db";

        builder.Services.AddSingleton<FootballStatsContext>(_ =>
        {
            return new FootballStatsContext(mongoConnectionString, databaseName);
        });

        // Repositories
        builder.Services.AddScoped<PartidoRepository>();

        // Almacén del visor: guarda en MongoDB lo que se trae de FotMob
        builder.Services.AddSingleton<AlmacenFutbol>();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Índices de las colecciones del visor (si Mongo no responde, solo se avisa)
        await app.Services.GetRequiredService<AlmacenFutbol>().AsegurarIndicesAsync();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("Default");

        // Frontend estático (dashboard de estadísticas) servido desde wwwroot
        app.UseDefaultFiles();
        app.UseStaticFiles();


        app.UseAuthorization();

        app.MapControllers();

        // Render/contenedores: el puerto viene en la variable PORT. En local se respeta ASPNETCORE_URLS.
        var port = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrEmpty(port))
        {
            app.Urls.Clear();
            app.Urls.Add($"http://*:{port}");
        }

        await app.RunAsync();
    }
}

using MongoDB.Driver;
using FootballStats.Domain;

namespace FootballStats.Infrastructure;

/// <summary>
/// Implementación genérica del repositorio CRUD con MongoDB.
/// </summary>
/// <typeparam name="T">Tipo de entidad.</typeparam>
public class Repository<T> : IRepository<T> where T : class
{
    private readonly IMongoCollection<T> _collection;

    public Repository(FootballStatsContext context)
    {
        // Buscamos la propiedad del contexto cuyo tipo sea IMongoCollection<T>,
        // sin depender del nombre singular/plural de la entidad (Partido -> Partidos).
        var propiedad = context.GetType()
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(p => typeof(IMongoCollection<T>).IsAssignableFrom(p.PropertyType))
            ?? throw new InvalidOperationException($"La colección '{typeof(T).Name}' no se encontró en el contexto.");

        _collection = propiedad.GetValue(context) as IMongoCollection<T>
            ?? throw new InvalidOperationException($"La colección '{typeof(T).Name}' no se encontró en el contexto.");
    }

    public async Task<T> CreateAsync(T entity)
    {
        await _collection.InsertOneAsync(entity);
        return entity;
    }

    public async Task<IEnumerable<T>> GetAllAsync(FilterDefinition<T>? filter = null)
    {
        var lista = filter is null
            ? _collection.Find(Builders<T>.Filter.Empty).ToList()
            : _collection.Find(filter).ToList();
        return lista;
    }

    public async Task<T?> GetByIdAsync(string id)
    {
        var objectId = new MongoDB.Bson.ObjectId(id);
        var filtro = Builders<T>.Filter.Eq("_id", objectId);
        return await _collection.Find(filtro).FirstOrDefaultAsync();
    }

    public async Task<bool> UpdateAsync(string id, UpdateDefinition<T> update)
    {
        var objetoId = new MongoDB.Bson.ObjectId(id);
        var filtro = Builders<T>.Filter.Eq("_id", objetoId);
        var resultado = await _collection.UpdateOneAsync(filtro, update);
        return resultado.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var objetoId = new MongoDB.Bson.ObjectId(id);
        var filtro = Builders<T>.Filter.Eq("_id", objetoId);
        var resultado = await _collection.DeleteOneAsync(filtro);
        return resultado.DeletedCount > 0;
    }

    public async Task<bool> SoftDeleteAsync(string id)
    {
        var objetoId = new MongoDB.Bson.ObjectId(id);
        var filtro = Builders<T>.Filter.Eq("_id", objetoId);

        // Establecemos el campo "activo" a false si existe
        if (typeof(T).GetProperty("Activo") is not null)
        {
            var update = Builders<T>.Update.Set("Activo", false);
            var updateResult = await _collection.UpdateOneAsync(filtro, update);
            return updateResult.ModifiedCount > 0;
        }

        // Si no hay campo activo, hacemos borrado físico (porque no es posible el borrado lógico)
        var deleteResult = await _collection.DeleteOneAsync(filtro);
        return deleteResult.DeletedCount > 0;
    }
}

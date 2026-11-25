using MongoDB.Driver;
using FootballStats.Domain;

namespace FootballStats.Infrastructure;

/// <summary>
/// Interfaz genérica de repositorio CRUD.
/// </summary>
/// <typeparam name="T">Tipo de entidad.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Inserta un nuevo documento.
    /// </summary>
    /// <param name="entity">Entidad a insertar.</param>
    /// <returns>Documento insertado (con Id asignado).</returns>
    Task<T> CreateAsync(T entity);

    /// <summary>
    /// Obtiene todos los documentos (opcional filtro).
    /// </summary>
    /// <param name="filter">Filtro opcional (puede ser null).</param>
    /// <returns>Lista de entidades.</returns>
    Task<IEnumerable<T>> GetAllAsync(FilterDefinition<T>? filter = null);

    /// <summary>
    /// Obtiene un documento por su Id.
    /// </summary>
    /// <param name="id">Id del documento.</param>
    /// <returns>Entidad si existe; de lo contrario null.</returns>
    Task<T?> GetByIdAsync(string id);

    /// <summary>
    /// Actualiza un documento existente.
    /// </summary>
    /// <param name="id">Id del documento a actualizar.</param>
    /// <param name="update">Actualización (expresión de actualización).</param>
    /// <returns>True si se actualizó, false si no se encontró.</returns>
    Task<bool> UpdateAsync(string id, UpdateDefinition<T> update);

    /// <summary>
    /// Elimina un documento por su Id.
    /// </summary>
    /// <param name="id">Id del documento a eliminar.</param>
    /// <returns>True si se eliminó, false si no se encontró.</returns>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// Eliminación lógica (cambia estado a inactivo).
    /// </summary>
    /// <param name="id">Id del documento.</param>
    /// <returns>True si se actualizó el estado, false si no se encontró.</returns>
    Task<bool> SoftDeleteAsync(string id);
}

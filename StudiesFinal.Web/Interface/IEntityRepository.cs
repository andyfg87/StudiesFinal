using StudiesFinal.Models.Interface;
using System.Linq.Expressions;

namespace StudiesFinal.Web.Interface
{
    public interface IEntityRepository<TEntity, TKey> where TEntity : class, IEntity<TKey>
    {
        public Task AddAsync(TEntity entity);
        public Task UpdateAsync(TEntity entity);

        /// <summary>Elimina. En entidades ISoftDelete solo las marca como eliminadas (con usuario y fecha).</summary>
        public Task DeleteAsync(TKey id);

        /// <summary>Busca por Id (sin las eliminadas). La entidad queda en seguimiento para modificarla.</summary>
        public Task<TEntity?> GetByIdAsync(TKey id);

        public Task<IQueryable<TEntity>> GetAll(Expression<Func<TEntity, bool>>? filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            string includeProperties = ""); // Para paginación
        public Task<IQueryable<TEntity>> GetAllWithNestedInclude(params string[] includeProperties);

        /// <summary>Todas, incluidas las eliminadas (p. ej. para no repetir un Id o un nombre).</summary>
        public Task<IQueryable<TEntity>> GetAllIncludingDeleted();

        /// <summary>Solo las eliminadas (ISoftDelete), para "Deleted items".</summary>
        public Task<IQueryable<TEntity>> GetDeleted(string includeProperties = "");

        /// <summary>Quita la marca de eliminada. false si no existe o no estaba eliminada.</summary>
        public Task<bool> RestoreAsync(TKey id);

        public Task SaveChangesAsync();

        /// <summary>Deja de seguir una entidad (p. ej. un alta que falló al guardar, para reintentarla).</summary>
        public Task DetachAsync(TEntity entity);
    }
}

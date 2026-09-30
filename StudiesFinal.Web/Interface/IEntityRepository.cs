using StudiesFinal.Models.Interface;
using System.Linq.Expressions;

namespace StudiesFinal.Web.Interface
{
    public interface IEntityRepository<TEntity, TKey> where TEntity : class, IEntity<TKey>
    {
        public Task AddAsync(TEntity entity);
        public Task UpdateAsync(TEntity entity);
        public Task DeleteAsync(TKey id);
        public Task<TEntity?> GetByIdAsync(TKey id);
        public Task<IQueryable<TEntity>> GetAll(Expression<Func<TEntity, bool>>? filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            string includeProperties = ""); // Para paginación
        public Task<IQueryable<TEntity>> GetAllWithNestedInclude(params string[] includeProperties);
        public Task SaveChangesAsync();
    }
}

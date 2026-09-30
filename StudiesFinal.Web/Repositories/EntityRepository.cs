using Microsoft.EntityFrameworkCore;
using StudiesFinal.Models.EF;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Interface;
using System.Linq.Expressions;

namespace StudiesFinal.Web.Repositories
{
    public class EntityRepository<TEntity, TKey> : IEntityRepository<TEntity, TKey>
        where TEntity : class, IEntity<TKey>
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<TEntity> _dbSet;

        public EntityRepository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<TEntity>();
        }

        public async Task AddAsync(TEntity entity) => await _dbSet.AddAsync(entity);

        public Task UpdateAsync(TEntity entity)
        {
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(TKey id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
                _dbSet.Remove(entity);
        }

        public async Task<TEntity?> GetByIdAsync(TKey id) => await _dbSet.FindAsync(id);

        public Task<IQueryable<TEntity>> GetAll(
            Expression<Func<TEntity, bool>>? filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            string includeProperties = "")
        {
            IQueryable<TEntity> query = _dbSet.AsNoTracking(); // Mejor rendimiento para consultas de solo lectura

            // Aplicar filtros dinámicos
            if (filter != null)
            {
                query = query.Where(filter);
            }

            // Incluir propiedades relacionadas (ej: "Patient,SignedBy")
            foreach (var includeProperty in includeProperties.Split(
                new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProperty.Trim());
            }

            // Ordenamiento dinámico
            if (orderBy != null)
            {
                return Task.FromResult<IQueryable<TEntity>>(orderBy(query));
            }

            return Task.FromResult(query);
        }

        public Task<IQueryable<TEntity>> GetAllWithNestedInclude(params string[] includeProperties)
        {
            IQueryable<TEntity> query = _dbSet.AsNoTracking();

            foreach (var includeProperty in includeProperties)
            {
                query = query.Include(includeProperty);
            }

            return Task.FromResult(query);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}

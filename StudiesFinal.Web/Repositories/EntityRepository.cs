using Microsoft.EntityFrameworkCore;
using StudiesFinal.Models.EF;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Extensions;
using StudiesFinal.Web.Interface;
using System.Linq.Expressions;

namespace StudiesFinal.Web.Repositories
{
    public class EntityRepository<TEntity, TKey> : IEntityRepository<TEntity, TKey>
        where TEntity : class, IEntity<TKey>
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<TEntity> _dbSet;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EntityRepository(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _dbSet = context.Set<TEntity>();
            _httpContextAccessor = httpContextAccessor;
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
            if (entity == null) return;

            // Borrado lógico: se guarda quién y cuándo (ApplicationDbContext convierte el Remove en marca)
            if (entity is ISoftDelete soft)
            {
                soft.DeletedAt = DateTime.Now;
                soft.DeletedByName = _httpContextAccessor.HttpContext?.User.FullName() ?? "System";
            }
            _dbSet.Remove(entity);
        }

        // FirstOrDefault (y no Find) para que se apliquen los filtros globales: una entidad
        // eliminada no se puede abrir ni editar escribiendo su Id en la URL.
        public async Task<TEntity?> GetByIdAsync(TKey id) => await _dbSet.FirstOrDefaultAsync(IdEquals(id));

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

        public Task<IQueryable<TEntity>> GetAllIncludingDeleted()
            => Task.FromResult(_dbSet.AsNoTracking().IgnoreQueryFilters());

        public Task<IQueryable<TEntity>> GetDeleted(string includeProperties = "")
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
                return Task.FromResult(Enumerable.Empty<TEntity>().AsQueryable());

            IQueryable<TEntity> query = _dbSet.AsNoTracking().IgnoreQueryFilters()
                .Where(e => EF.Property<bool>(e, nameof(ISoftDelete.IsDeleted)));

            foreach (var includeProperty in includeProperties.Split(',', StringSplitOptions.RemoveEmptyEntries))
                query = query.Include(includeProperty.Trim());

            return Task.FromResult(query);
        }

        public async Task<bool> RestoreAsync(TKey id)
        {
            var entity = await _dbSet.IgnoreQueryFilters().FirstOrDefaultAsync(IdEquals(id));
            if (entity is not ISoftDelete { IsDeleted: true } soft)
                return false;

            soft.IsDeleted = false;
            soft.DeletedAt = null;
            soft.DeletedByName = null;
            return true;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        /// <summary>e => e.Id == id (traducible a SQL para cualquier tipo de clave).</summary>
        private static Expression<Func<TEntity, bool>> IdEquals(TKey id)
        {
            var e = Expression.Parameter(typeof(TEntity), "e");
            var body = Expression.Equal(
                Expression.Property(e, nameof(IEntity<TKey>.Id)),
                Expression.Constant(id, typeof(TKey)));
            return Expression.Lambda<Func<TEntity, bool>>(body, e);
        }
    }
}

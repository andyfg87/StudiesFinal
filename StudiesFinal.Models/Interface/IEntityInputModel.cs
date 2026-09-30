namespace StudiesFinal.Models.Interface
{
    public interface IEntityInputModel<TEntity, TKey>
    {
        TKey Id { get; set; }
        TEntity Export();
        void Import(TEntity entity);
        void Merge(TEntity entity);
    }
}

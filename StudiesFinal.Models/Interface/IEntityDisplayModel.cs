namespace StudiesFinal.Models.Interface
{
    public interface IEntityDisplayModel<TEntity, TKey>
    {
        TKey Id { get; set; }
        void Import(TEntity entity);
    }
}

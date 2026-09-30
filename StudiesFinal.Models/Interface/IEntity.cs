namespace StudiesFinal.Models.Interface
{
    public interface IEntity<TId>
    {
        public TId Id { get; set; }
    }
}

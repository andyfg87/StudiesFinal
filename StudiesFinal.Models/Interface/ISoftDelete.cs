namespace StudiesFinal.Models.Interface
{
    /// <summary>
    /// Entidades con borrado lógico: al eliminarlas no se borran de la base, se marcan.
    /// ApplicationDbContext oculta las eliminadas en todas las consultas (filtro global) y
    /// convierte cualquier Remove en esta marca al guardar. Se pueden restaurar desde
    /// "Deleted items" (TrashController).
    /// </summary>
    public interface ISoftDelete
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
        string? DeletedByName { get; set; }
    }
}

namespace InventoryFlow.Domain.Common;

/// <summary>
/// Provides identity-based equality for domain entities.
/// </summary>
/// <typeparam name="TId">The type used to identify the entity.</typeparam>
public abstract class Entity<TId>
    where TId : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Entity{TId}"/> class.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>
    /// Gets the entity identifier.
    /// </summary>
    public TId Id { get; protected set; }
}

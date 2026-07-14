namespace BuildingBlocks.Domain;

/// <summary>
/// Base class for domain entities. Provides a Guid identity and
/// identity-based equality (two entities are equal when they are the
/// same type and share the same non-default Id).
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }

    protected Entity() { }

    protected Entity(Guid id) => Id = id;

    public override bool Equals(object? obj) =>
        obj is Entity other
        && other.GetType() == GetType()
        && Id != default
        && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right) =>
        Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) =>
        !Equals(left, right);
}

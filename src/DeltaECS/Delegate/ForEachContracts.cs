namespace Delta.ECS;

/// <summary>Selects a ForEach form that supplies the current entity.</summary>
public readonly record struct ForEachEntityTag
{
    public static ForEachEntityTag Instance => default;
}

public delegate void ForEachEntityAction(Entity entity);

public delegate void ForEachAction();

public delegate void ForEachContextAction<TContext>(ref TContext context);

public delegate void ForEachContextEntityAction<TContext>(ref TContext context, Entity entity);

public interface IForEachEntity
{
    void Invoke(Entity entity);
}

public interface IForEach
{
    void Invoke();
}

public interface IForEachContext<TContext>
{
    void Invoke(ref TContext context);
}

public interface IForEachContextEntity<TContext>
{
    void Invoke(ref TContext context, Entity entity);
}

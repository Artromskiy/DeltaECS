namespace Delta.ECS;

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

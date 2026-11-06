namespace Delta.ECS;

/// <summary>Selects grouping component values through explicit registrations.</summary>
public static class ComponentValueQueryExtensions
{
    /// <summary>Requires the selected grouping component to equal the supplied value.</summary>
    public static Query WhereAll<T>(this World world, ComponentId componentId, in T value) where T : struct
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return world.CreateQuery(world.CreateGroupFilter(componentId, in value, GroupMatchKind.All));
    }

    /// <summary>Matches the supplied value of the selected grouping component.</summary>
    public static Query WhereAny<T>(this World world, ComponentId componentId, in T value) where T : struct
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return world.CreateQuery(world.CreateGroupFilter(componentId, in value, GroupMatchKind.Any));
    }

    /// <summary>Excludes the supplied value of the selected grouping component.</summary>
    public static Query WhereNone<T>(this World world, ComponentId componentId, in T value) where T : struct
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return world.CreateQuery(world.CreateGroupFilter(componentId, in value, GroupMatchKind.None));
    }

    /// <summary>Requires the selected grouping component to equal the supplied value.</summary>
    public static Query WhereAll<T>(this Query query, ComponentId componentId, in T value) where T : struct
        => query.CreateValueQuery(componentId, in value, GroupMatchKind.All);

    /// <summary>Adds a value of the selected grouping component to this query's alternatives.</summary>
    public static Query WhereAny<T>(this Query query, ComponentId componentId, in T value) where T : struct
        => query.CreateValueQuery(componentId, in value, GroupMatchKind.Any);

    /// <summary>Excludes the supplied value of the selected grouping component.</summary>
    public static Query WhereNone<T>(this Query query, ComponentId componentId, in T value) where T : struct
        => query.CreateValueQuery(componentId, in value, GroupMatchKind.None);
}

namespace Delta.ECS;

internal interface IComponentTypeRegistrationRoute
{
    bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor);
}

internal interface IComponentRegistrationToken
{
    bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor);
}

internal sealed class ComponentRegistrationTypeToken : IComponentRegistrationToken
{
    private readonly IComponentTypeRegistrationRoute _route;

    internal ComponentRegistrationTypeToken(IComponentTypeRegistrationRoute route) => _route = route;

    public bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
        => _route.TryVisit(componentId, visitor);
}

internal abstract class ComponentTypeRegistrationRoute<TComponent> : IComponentTypeRegistrationRoute
{
    public abstract bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor);
}

internal sealed class UnconstrainedComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is not IUnconstrainedComponentTypeVisitor typedVisitor)
        {
            return false;
        }

        typedVisitor.Visit<TComponent>(componentId);
        return true;
    }
}

internal sealed class StructComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
    where TComponent : struct
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is INewComponentTypeVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is not IStructComponentTypeVisitor typedVisitor)
        {
            return false;
        }

        typedVisitor.Visit<TComponent>(componentId);
        return true;
    }
}

internal sealed class ClassComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
    where TComponent : class
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is not IClassComponentTypeVisitor typedVisitor)
        {
            return false;
        }

        typedVisitor.Visit<TComponent>(componentId);
        return true;
    }
}

internal sealed class UnmanagedComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
    where TComponent : unmanaged
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is INewComponentTypeVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IUnmanagedComponentTypeVisitor unmanagedVisitor)
        {
            unmanagedVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IStructComponentTypeVisitor structVisitor)
        {
            structVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return false;
    }
}

internal sealed class NewComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
    where TComponent : new()
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is not INewComponentTypeVisitor typedVisitor)
        {
            return false;
        }

        typedVisitor.Visit<TComponent>(componentId);
        return true;
    }
}

internal sealed class ClassNewComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
    where TComponent : class, new()
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is IClassNewComponentTypeVisitor classNewVisitor)
        {
            classNewVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IClassComponentTypeVisitor classVisitor)
        {
            classVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is INewComponentTypeVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return false;
    }
}

internal interface IComponentInterfaceVisitorRoute
{
    bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor);
}

internal abstract class ComponentInterfaceVisitorRoute<TComponent, TInterface> : IComponentInterfaceVisitorRoute
    where TComponent : TInterface
{
    public abstract bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor);

    protected static bool HasMatchingConstraint(IComponentTypeVisitorConstraint visitor)
        => visitor.ConstraintType.Equals(typeof(TInterface).TypeHandle);
}

internal sealed class UnconstrainedComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : TInterface
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is not IComponentTypeVisitor<TInterface> typedVisitor)
        {
            return false;
        }

        if (!HasMatchingConstraint(typedVisitor))
        {
            return false;
        }

        typedVisitor.Visit<TComponent>(componentId);
        return true;
    }
}

internal sealed class StructComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : struct, TInterface
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is INewComponentTypeVisitor<TInterface> newVisitor)
        {
            if (!HasMatchingConstraint(newVisitor))
            {
                return false;
            }

            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IStructComponentTypeVisitor<TInterface> structVisitor)
        {
            if (!HasMatchingConstraint(structVisitor))
            {
                return false;
            }

            structVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IComponentTypeVisitor<TInterface> typedVisitor)
        {
            if (!HasMatchingConstraint(typedVisitor))
            {
                return false;
            }

            typedVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return false;
    }
}

internal sealed class ClassComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : class, TInterface
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is IClassComponentTypeVisitor<TInterface> classVisitor)
        {
            if (!HasMatchingConstraint(classVisitor))
            {
                return false;
            }

            classVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IComponentTypeVisitor<TInterface> typedVisitor)
        {
            if (!HasMatchingConstraint(typedVisitor))
            {
                return false;
            }

            typedVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return false;
    }
}

internal sealed class UnmanagedComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : unmanaged, TInterface
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is INewComponentTypeVisitor<TInterface> newVisitor)
        {
            if (!HasMatchingConstraint(newVisitor))
            {
                return false;
            }

            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IUnmanagedComponentTypeVisitor<TInterface> unmanagedVisitor)
        {
            if (!HasMatchingConstraint(unmanagedVisitor))
            {
                return false;
            }

            unmanagedVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IStructComponentTypeVisitor<TInterface> structVisitor)
        {
            if (!HasMatchingConstraint(structVisitor))
            {
                return false;
            }

            structVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IComponentTypeVisitor<TInterface> typedVisitor)
        {
            if (!HasMatchingConstraint(typedVisitor))
            {
                return false;
            }

            typedVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return false;
    }
}

internal sealed class NewComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : TInterface, new()
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is not INewComponentTypeVisitor<TInterface> newVisitor)
        {
            return false;
        }

        if (!HasMatchingConstraint(newVisitor))
        {
            return false;
        }

        newVisitor.Visit<TComponent>(componentId);
        return true;
    }
}

internal sealed class ClassNewComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : class, TInterface, new()
{
    public override bool TryVisit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is IClassNewComponentTypeVisitor<TInterface> classNewVisitor)
        {
            if (!HasMatchingConstraint(classNewVisitor))
            {
                return false;
            }

            classNewVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IClassComponentTypeVisitor<TInterface> classVisitor)
        {
            if (!HasMatchingConstraint(classVisitor))
            {
                return false;
            }

            classVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is INewComponentTypeVisitor<TInterface> newVisitor)
        {
            if (!HasMatchingConstraint(newVisitor))
            {
                return false;
            }

            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IComponentTypeVisitor<TInterface> typedVisitor)
        {
            if (!HasMatchingConstraint(typedVisitor))
            {
                return false;
            }

            typedVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return false;
    }
}

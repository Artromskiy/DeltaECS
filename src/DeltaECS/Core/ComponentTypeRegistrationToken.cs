namespace Delta.ECS;

internal interface IComponentTypeRegistrationRoute
{
    bool TryVisit(ComponentId componentId, IVisitor visitor);
}

internal interface IComponentRegistrationToken
{
    bool TryVisit(ComponentId componentId, IVisitor visitor);
}

internal sealed class ComponentRegistrationTypeToken : IComponentRegistrationToken
{
    private readonly IComponentTypeRegistrationRoute _route;

    internal ComponentRegistrationTypeToken(IComponentTypeRegistrationRoute route) => _route = route;

    public bool TryVisit(ComponentId componentId, IVisitor visitor)
        => _route.TryVisit(componentId, visitor);
}

internal abstract class ComponentTypeRegistrationRoute<TComponent> : IComponentTypeRegistrationRoute
{
    public bool TryVisit(ComponentId componentId, IVisitor visitor)
        => TryVisitTyped(componentId, visitor);

    protected abstract bool TryVisitTyped(ComponentId componentId, IVisitor visitor);
}

internal sealed class UnconstrainedComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
{
    protected override bool TryVisitTyped(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is not IUnconstrainedVisitor typedVisitor)
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
    protected override bool TryVisitTyped(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is INewVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is not IStructVisitor typedVisitor)
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
    protected override bool TryVisitTyped(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is not IClassVisitor typedVisitor)
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
    protected override bool TryVisitTyped(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is INewVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IUnmanagedVisitor unmanagedVisitor)
        {
            unmanagedVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IStructVisitor structVisitor)
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
    protected override bool TryVisitTyped(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is not INewVisitor typedVisitor)
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
    protected override bool TryVisitTyped(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is IClassNewVisitor classNewVisitor)
        {
            classNewVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IClassVisitor classVisitor)
        {
            classVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is INewVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return false;
    }
}

internal interface IComponentInterfaceVisitorRoute
{
    bool TryVisit(ComponentId componentId, IVisitor visitor);
}

internal abstract class ComponentInterfaceVisitorRoute<TComponent, TInterface> : IComponentInterfaceVisitorRoute
    where TComponent : TInterface
{
    public abstract bool TryVisit(ComponentId componentId, IVisitor visitor);

    protected static bool HasMatchingConstraint(IComponentVisitor visitor)
        => visitor.ConstraintType.Equals(typeof(TInterface).TypeHandle);
}

internal sealed class UnconstrainedComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : TInterface
{
    public override bool TryVisit(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is GeneralComponentTypeVisitor<TInterface> generalVisitor)
        {
            if (!HasMatchingConstraint(generalVisitor))
            {
                return false;
            }

            generalVisitor.DispatchUnconstrained<TComponent>(componentId);
            return true;
        }

        if (visitor is not IComponentVisitor<TInterface> typedVisitor)
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
    public override bool TryVisit(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is GeneralComponentTypeVisitor<TInterface> generalVisitor)
        {
            if (!HasMatchingConstraint(generalVisitor))
            {
                return false;
            }

            generalVisitor.DispatchStruct<TComponent>(componentId);
            return true;
        }

        if (visitor is INewVisitor<TInterface> newVisitor)
        {
            if (!HasMatchingConstraint(newVisitor))
            {
                return false;
            }

            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IStructVisitor<TInterface> structVisitor)
        {
            if (!HasMatchingConstraint(structVisitor))
            {
                return false;
            }

            structVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IComponentVisitor<TInterface> typedVisitor)
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
    public override bool TryVisit(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is GeneralComponentTypeVisitor<TInterface> generalVisitor)
        {
            if (!HasMatchingConstraint(generalVisitor))
            {
                return false;
            }

            generalVisitor.DispatchClass<TComponent>(componentId);
            return true;
        }

        if (visitor is IClassVisitor<TInterface> classVisitor)
        {
            if (!HasMatchingConstraint(classVisitor))
            {
                return false;
            }

            classVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IComponentVisitor<TInterface> typedVisitor)
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
    public override bool TryVisit(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is GeneralComponentTypeVisitor<TInterface> generalVisitor)
        {
            if (!HasMatchingConstraint(generalVisitor))
            {
                return false;
            }

            generalVisitor.DispatchUnmanaged<TComponent>(componentId);
            return true;
        }

        if (visitor is INewVisitor<TInterface> newVisitor)
        {
            if (!HasMatchingConstraint(newVisitor))
            {
                return false;
            }

            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IUnmanagedVisitor<TInterface> unmanagedVisitor)
        {
            if (!HasMatchingConstraint(unmanagedVisitor))
            {
                return false;
            }

            unmanagedVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IStructVisitor<TInterface> structVisitor)
        {
            if (!HasMatchingConstraint(structVisitor))
            {
                return false;
            }

            structVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IComponentVisitor<TInterface> typedVisitor)
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
    public override bool TryVisit(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is GeneralComponentTypeVisitor<TInterface> generalVisitor)
        {
            if (!HasMatchingConstraint(generalVisitor))
            {
                return false;
            }

            generalVisitor.DispatchNew<TComponent>(componentId);
            return true;
        }

        if (visitor is not INewVisitor<TInterface> newVisitor)
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
    public override bool TryVisit(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is GeneralComponentTypeVisitor<TInterface> generalVisitor)
        {
            if (!HasMatchingConstraint(generalVisitor))
            {
                return false;
            }

            generalVisitor.DispatchClassNew<TComponent>(componentId);
            return true;
        }

        if (visitor is IClassNewVisitor<TInterface> classNewVisitor)
        {
            if (!HasMatchingConstraint(classNewVisitor))
            {
                return false;
            }

            classNewVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IClassVisitor<TInterface> classVisitor)
        {
            if (!HasMatchingConstraint(classVisitor))
            {
                return false;
            }

            classVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is INewVisitor<TInterface> newVisitor)
        {
            if (!HasMatchingConstraint(newVisitor))
            {
                return false;
            }

            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (visitor is IComponentVisitor<TInterface> typedVisitor)
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

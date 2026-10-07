namespace Delta.ECS;

internal interface IComponentTypeRegistrationRoute
{
    void Visit(ComponentId componentId, IComponentTypeVisitor visitor);
}

internal interface IComponentRegistrationToken
{
    void Visit(ComponentId componentId, IComponentTypeVisitor visitor);
}

internal sealed class ComponentRegistrationTypeToken : IComponentRegistrationToken
{
    private readonly IComponentTypeRegistrationRoute _route;

    internal ComponentRegistrationTypeToken(IComponentTypeRegistrationRoute route) => _route = route;

    public void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
        => _route.Visit(componentId, visitor);
}

internal abstract class ComponentTypeRegistrationRoute<TComponent> : IComponentTypeRegistrationRoute
{
    public abstract void Visit(ComponentId componentId, IComponentTypeVisitor visitor);
}

internal sealed class UnconstrainedComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is not IUnconstrainedComponentTypeVisitor typedVisitor)
        {
            ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support unconstrained component registrations.");
            return;
        }

        typedVisitor.Visit<TComponent>(componentId);
    }
}

internal sealed class StructComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
    where TComponent : struct
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is INewComponentTypeVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is not IStructComponentTypeVisitor typedVisitor)
        {
            ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support struct or new() component registrations.");
            return;
        }

        typedVisitor.Visit<TComponent>(componentId);
    }
}

internal sealed class ClassComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
    where TComponent : class
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is not IClassComponentTypeVisitor typedVisitor)
        {
            ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support class component registrations.");
            return;
        }

        typedVisitor.Visit<TComponent>(componentId);
    }
}

internal sealed class UnmanagedComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
    where TComponent : unmanaged
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is INewComponentTypeVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is IUnmanagedComponentTypeVisitor unmanagedVisitor)
        {
            unmanagedVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is IStructComponentTypeVisitor structVisitor)
        {
            structVisitor.Visit<TComponent>(componentId);
            return;
        }

        ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support unmanaged, struct, or new() component registrations.");
    }
}

internal sealed class NewComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
    where TComponent : new()
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is not INewComponentTypeVisitor typedVisitor)
        {
            ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support new() component registrations.");
            return;
        }

        typedVisitor.Visit<TComponent>(componentId);
    }
}

internal sealed class ClassNewComponentTypeRegistrationRoute<TComponent> : ComponentTypeRegistrationRoute<TComponent>
    where TComponent : class, new()
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is IClassNewComponentTypeVisitor classNewVisitor)
        {
            classNewVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is IClassComponentTypeVisitor classVisitor)
        {
            classVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is INewComponentTypeVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return;
        }

        ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support this class-and-new component registration.");
    }
}

internal interface IComponentInterfaceVisitorRoute
{
    void Visit(ComponentId componentId, IComponentTypeVisitor visitor);
}

internal abstract class ComponentInterfaceVisitorRoute<TComponent, TInterface> : IComponentInterfaceVisitorRoute
    where TComponent : TInterface
{
    public abstract void Visit(ComponentId componentId, IComponentTypeVisitor visitor);

    protected static void EnsureConstraint(IComponentTypeVisitorConstraint visitor)
    {
        if (!visitor.ConstraintType.Equals(typeof(TInterface).TypeHandle))
        {
            ThrowHelper.ThrowComponentVisitorConstraintMismatch();
        }
    }
}

internal sealed class UnconstrainedComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : TInterface
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is not IComponentTypeVisitor<TInterface> typedVisitor)
        {
            ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support this bound interface route.");
            return;
        }

        EnsureConstraint(typedVisitor);
        typedVisitor.Visit<TComponent>(componentId);
    }
}

internal sealed class StructComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : struct, TInterface
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is INewComponentTypeVisitor<TInterface> newVisitor)
        {
            EnsureConstraint(newVisitor);
            newVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is IStructComponentTypeVisitor<TInterface> structVisitor)
        {
            EnsureConstraint(structVisitor);
            structVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is IComponentTypeVisitor<TInterface> typedVisitor)
        {
            EnsureConstraint(typedVisitor);
            typedVisitor.Visit<TComponent>(componentId);
            return;
        }

        ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support this struct interface route.");
    }
}

internal sealed class ClassComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : class, TInterface
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is IClassComponentTypeVisitor<TInterface> classVisitor)
        {
            EnsureConstraint(classVisitor);
            classVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is IComponentTypeVisitor<TInterface> typedVisitor)
        {
            EnsureConstraint(typedVisitor);
            typedVisitor.Visit<TComponent>(componentId);
            return;
        }

        ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support this class interface route.");
    }
}

internal sealed class UnmanagedComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : unmanaged, TInterface
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is INewComponentTypeVisitor<TInterface> newVisitor)
        {
            EnsureConstraint(newVisitor);
            newVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is IUnmanagedComponentTypeVisitor<TInterface> unmanagedVisitor)
        {
            EnsureConstraint(unmanagedVisitor);
            unmanagedVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is IStructComponentTypeVisitor<TInterface> structVisitor)
        {
            EnsureConstraint(structVisitor);
            structVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is IComponentTypeVisitor<TInterface> typedVisitor)
        {
            EnsureConstraint(typedVisitor);
            typedVisitor.Visit<TComponent>(componentId);
            return;
        }

        ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support this unmanaged interface route.");
    }
}

internal sealed class NewComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : TInterface, new()
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is not INewComponentTypeVisitor<TInterface> newVisitor)
        {
            ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support this constructible interface route.");
            return;
        }

        EnsureConstraint(newVisitor);
        newVisitor.Visit<TComponent>(componentId);
    }
}

internal sealed class ClassNewComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : class, TInterface, new()
{
    public override void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        if (visitor is IClassNewComponentTypeVisitor<TInterface> classNewVisitor)
        {
            EnsureConstraint(classNewVisitor);
            classNewVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is IClassComponentTypeVisitor<TInterface> classVisitor)
        {
            EnsureConstraint(classVisitor);
            classVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is INewComponentTypeVisitor<TInterface> newVisitor)
        {
            EnsureConstraint(newVisitor);
            newVisitor.Visit<TComponent>(componentId);
            return;
        }

        if (visitor is IComponentTypeVisitor<TInterface> typedVisitor)
        {
            EnsureConstraint(typedVisitor);
            typedVisitor.Visit<TComponent>(componentId);
            return;
        }

        ThrowHelper.ThrowComponentVisitorMismatch("The visitor does not support this constructible class interface route.");
    }
}

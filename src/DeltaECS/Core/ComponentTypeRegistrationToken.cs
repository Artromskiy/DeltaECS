namespace Delta.ECS;

internal interface IComponentTypeRegistrationRouteVisitor
{
    bool VisitUnconstrained<TComponent>(ComponentId componentId);

    bool VisitStruct<TComponent>(ComponentId componentId) where TComponent : struct;

    bool VisitClass<TComponent>(ComponentId componentId) where TComponent : class;

    bool VisitUnmanaged<TComponent>(ComponentId componentId) where TComponent : unmanaged;

    bool VisitNew<TComponent>(ComponentId componentId) where TComponent : new();

    bool VisitClassNew<TComponent>(ComponentId componentId) where TComponent : class, new();
}

internal interface IComponentTypeRegistrationRoute
{
    bool Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        where TVisitor : struct, IComponentTypeRegistrationRouteVisitor;
}

internal interface IComponentRegistrationToken
{
    bool TryVisit(ComponentId componentId, IVisitor visitor);
}

internal sealed class ComponentRegistrationTypeToken : IComponentRegistrationToken
{
    private readonly IComponentTypeRegistrationRoute _route;

    internal ComponentRegistrationTypeToken(IComponentTypeRegistrationRoute route) => _route = route;

    bool IComponentRegistrationToken.TryVisit(ComponentId componentId, IVisitor visitor)
    {
        var routeVisitor = new ComponentTypeRegistrationRouteVisitor(visitor);
        return _route.Accept(componentId, ref routeVisitor);
    }
}

internal sealed class UnconstrainedComponentTypeRegistrationRoute<TComponent> : IComponentTypeRegistrationRoute
{
    bool IComponentTypeRegistrationRoute.Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitUnconstrained<TComponent>(componentId);
}

internal sealed class StructComponentTypeRegistrationRoute<TComponent> : IComponentTypeRegistrationRoute
    where TComponent : struct
{
    bool IComponentTypeRegistrationRoute.Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitStruct<TComponent>(componentId);
}

internal sealed class ClassComponentTypeRegistrationRoute<TComponent> : IComponentTypeRegistrationRoute
    where TComponent : class
{
    bool IComponentTypeRegistrationRoute.Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitClass<TComponent>(componentId);
}

internal sealed class UnmanagedComponentTypeRegistrationRoute<TComponent> : IComponentTypeRegistrationRoute
    where TComponent : unmanaged
{
    bool IComponentTypeRegistrationRoute.Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitUnmanaged<TComponent>(componentId);
}

internal sealed class NewComponentTypeRegistrationRoute<TComponent> : IComponentTypeRegistrationRoute
    where TComponent : new()
{
    bool IComponentTypeRegistrationRoute.Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitNew<TComponent>(componentId);
}

internal sealed class ClassNewComponentTypeRegistrationRoute<TComponent> : IComponentTypeRegistrationRoute
    where TComponent : class, new()
{
    bool IComponentTypeRegistrationRoute.Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitClassNew<TComponent>(componentId);
}

internal readonly struct ComponentTypeRegistrationRouteVisitor : IComponentTypeRegistrationRouteVisitor
{
    private readonly IVisitor _visitor;

    internal ComponentTypeRegistrationRouteVisitor(IVisitor visitor) => _visitor = visitor;

    bool IComponentTypeRegistrationRouteVisitor.VisitUnconstrained<TComponent>(ComponentId componentId)
        => TryVisitUnconstrained<TComponent>(componentId);

    bool IComponentTypeRegistrationRouteVisitor.VisitStruct<TComponent>(ComponentId componentId)
    {
        if (_visitor is INewVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (_visitor is IStructVisitor structVisitor)
        {
            structVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return TryVisitUnconstrained<TComponent>(componentId);
    }

    bool IComponentTypeRegistrationRouteVisitor.VisitClass<TComponent>(ComponentId componentId)
    {
        if (_visitor is IClassVisitor classVisitor)
        {
            classVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return TryVisitUnconstrained<TComponent>(componentId);
    }

    bool IComponentTypeRegistrationRouteVisitor.VisitUnmanaged<TComponent>(ComponentId componentId)
    {
        if (_visitor is INewVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (_visitor is IUnmanagedVisitor unmanagedVisitor)
        {
            unmanagedVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (_visitor is IStructVisitor structVisitor)
        {
            structVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return TryVisitUnconstrained<TComponent>(componentId);
    }

    bool IComponentTypeRegistrationRouteVisitor.VisitNew<TComponent>(ComponentId componentId)
    {
        if (_visitor is INewVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return TryVisitUnconstrained<TComponent>(componentId);
    }

    bool IComponentTypeRegistrationRouteVisitor.VisitClassNew<TComponent>(ComponentId componentId)
    {
        if (_visitor is IClassNewVisitor classNewVisitor)
        {
            classNewVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (_visitor is IClassVisitor classVisitor)
        {
            classVisitor.Visit<TComponent>(componentId);
            return true;
        }

        if (_visitor is INewVisitor newVisitor)
        {
            newVisitor.Visit<TComponent>(componentId);
            return true;
        }

        return TryVisitUnconstrained<TComponent>(componentId);
    }

    private bool TryVisitUnconstrained<TComponent>(ComponentId componentId)
    {
        if (_visitor is not IUnconstrainedVisitor unconstrainedVisitor)
        {
            return false;
        }

        unconstrainedVisitor.Visit<TComponent>(componentId);
        return true;
    }
}

internal interface IComponentInterfaceRouteVisitor<TInterface>
{
    bool VisitUnconstrained<TComponent>(ComponentId componentId)
        where TComponent : TInterface;

    bool VisitStruct<TComponent>(ComponentId componentId)
        where TComponent : struct, TInterface;

    bool VisitClass<TComponent>(ComponentId componentId)
        where TComponent : class, TInterface;

    bool VisitUnmanaged<TComponent>(ComponentId componentId)
        where TComponent : unmanaged, TInterface;

    bool VisitNew<TComponent>(ComponentId componentId)
        where TComponent : TInterface, new();

    bool VisitClassNew<TComponent>(ComponentId componentId)
        where TComponent : class, TInterface, new();
}

internal interface IComponentInterfaceVisitorRoute
{
    bool TryVisit(ComponentId componentId, IVisitor visitor);
}

internal abstract class ComponentInterfaceVisitorRoute<TComponent, TInterface> : IComponentInterfaceVisitorRoute
    where TComponent : TInterface
{
    bool IComponentInterfaceVisitorRoute.TryVisit(ComponentId componentId, IVisitor visitor)
    {
        if (visitor is not IComponentVisitor)
        {
            return false;
        }

        var routeVisitor = new ComponentInterfaceRouteVisitor<TInterface>(visitor);
        return Accept(componentId, ref routeVisitor);
    }

    internal abstract bool Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        where TVisitor : struct, IComponentInterfaceRouteVisitor<TInterface>;
}

internal sealed class UnconstrainedComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : TInterface
{
    internal override bool Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitUnconstrained<TComponent>(componentId);
}

internal sealed class StructComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : struct, TInterface
{
    internal override bool Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitStruct<TComponent>(componentId);
}

internal sealed class ClassComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : class, TInterface
{
    internal override bool Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitClass<TComponent>(componentId);
}

internal sealed class UnmanagedComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : unmanaged, TInterface
{
    internal override bool Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitUnmanaged<TComponent>(componentId);
}

internal sealed class NewComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : TInterface, new()
{
    internal override bool Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitNew<TComponent>(componentId);
}

internal sealed class ClassNewComponentInterfaceVisitorRoute<TComponent, TInterface> :
    ComponentInterfaceVisitorRoute<TComponent, TInterface>
    where TComponent : class, TInterface, new()
{
    internal override bool Accept<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        => visitor.VisitClassNew<TComponent>(componentId);
}

internal readonly struct ComponentInterfaceRouteVisitor<TInterface> : IComponentInterfaceRouteVisitor<TInterface>
{
    private readonly IVisitor _visitor;

    internal ComponentInterfaceRouteVisitor(IVisitor visitor) => _visitor = visitor;

    bool IComponentInterfaceRouteVisitor<TInterface>.VisitUnconstrained<TComponent>(ComponentId componentId)
        => _visitor switch
        {
            GeneralComponentTypeVisitor<TInterface> visitor => visitor.TryUnconstrained<TComponent>(componentId),
            IComponentVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            _ => false
        };

    bool IComponentInterfaceRouteVisitor<TInterface>.VisitStruct<TComponent>(ComponentId componentId)
        => _visitor switch
        {
            GeneralComponentTypeVisitor<TInterface> visitor => visitor.TryStruct<TComponent>(componentId),
            INewVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            IStructVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            IComponentVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            _ => false
        };

    bool IComponentInterfaceRouteVisitor<TInterface>.VisitClass<TComponent>(ComponentId componentId)
        => _visitor switch
        {
            GeneralComponentTypeVisitor<TInterface> visitor => visitor.TryClass<TComponent>(componentId),
            IClassVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            IComponentVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            _ => false
        };

    bool IComponentInterfaceRouteVisitor<TInterface>.VisitUnmanaged<TComponent>(ComponentId componentId)
        => _visitor switch
        {
            GeneralComponentTypeVisitor<TInterface> visitor => visitor.TryUnmanaged<TComponent>(componentId),
            INewVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            IUnmanagedVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            IStructVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            IComponentVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            _ => false
        };

    bool IComponentInterfaceRouteVisitor<TInterface>.VisitNew<TComponent>(ComponentId componentId)
        => _visitor switch
        {
            GeneralComponentTypeVisitor<TInterface> visitor => visitor.TryNew<TComponent>(componentId),
            INewVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            _ => false
        };

    bool IComponentInterfaceRouteVisitor<TInterface>.VisitClassNew<TComponent>(ComponentId componentId)
        => _visitor switch
        {
            GeneralComponentTypeVisitor<TInterface> visitor => visitor.TryClassNew<TComponent>(componentId),
            IClassNewVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            IClassVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            INewVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            IComponentVisitor<TInterface> visitor => visitor.TryVisit<TComponent>(componentId),
            _ => false
        };
}

using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

public struct Cmp1 { public int Value; }
public struct Cmp2 { public int Value; }
public struct Cmp3 { public int Value; }
public struct Cmp4 { public int Value; }
public struct Dead { }
public struct Alive { }
public struct NeedsRespawn { public int Value; }

public struct Context
{
    public int Value;
}

public struct FunctorR : IForEach
{
    public void Invoke(ref readonly Cmp1 cmp1) => _ = cmp1.Value;
}

public struct FunctorW : IForEach
{
    public void Invoke(ref Cmp1 cmp1) => cmp1.Value++;
}

public struct FunctorI : IForEach
{
    public void Invoke(in Cmp1 cmp1) => _ = cmp1.Value;
}

public struct FunctorV : IForEach
{
    public void Invoke(Cmp1 cmp1) => _ = cmp1.Value;
}

public struct FunctorRW : IForEach
{
    public void Invoke(ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value;
}

public struct FunctorParallelContext : IForEachContext<Context>
{
    public void Invoke(in Context context, ref Cmp1 cmp1) => cmp1.Value += context.Value;
}

public struct FunctorContext : IForEachContext<Context>
{
    public void Invoke(ref Context context, ref Cmp1 cmp1) => cmp1.Value += context.Value;
}

public struct FunctorEntityW : IForEachEntity
{
    public void Invoke(EntityRef entity, ref Cmp1 cmp1) => cmp1.Value += entity.Index;
}

public struct FunctorEntityRW : IForEachEntity
{
    public void Invoke(EntityRef entity, ref Cmp1 cmp1, in Cmp2 cmp2)
        => cmp1.Value += entity.Index + cmp2.Value;
}

public struct FunctorEntityContext : IForEachContextEntity<Context>
{
    public void Invoke(ref Context context, EntityRef entity, ref Cmp1 cmp1)
        => cmp1.Value += context.Value + entity.Index;
}

public struct FunctorEntity : IForEachEntity
{
    public void Invoke(EntityRef entity) => _ = entity;
}

public struct WhereFunctorContext : IForEachContext<Context>
{
    public void Invoke(ref Context context, ref Cmp3 cmp3) => cmp3.Value += context.Value;
}

public struct WhereEntityFunctorContext : IForEachContextEntity<Context>
{
    public void Invoke(ref Context context, EntityRef entity, ref Cmp3 cmp3)
        => cmp3.Value += context.Value + entity.Index;
}

public struct WherePredicateContext : IWherePredicate
{
    public bool Invoke(ref Context context, in Cmp3 cmp3, in Cmp4 cmp4)
    {
        context.Value++;
        return cmp3.Value < cmp4.Value;
    }
}

public struct WhereEntityPredicateContext : IWherePredicate
{
    public bool Invoke(ref Context context, Entity entity, in Cmp3 cmp3, in Cmp4 cmp4)
    {
        context.Value += entity.Index;
        return cmp3.Value < cmp4.Value;
    }
}

public struct WhereEntityPredicate : IWherePredicate
{
    public bool Invoke(Entity entity, in Cmp3 cmp3) => cmp3.Value < entity.Index;
}

public struct WherePredicate : IWherePredicate
{
    public bool Invoke(in Cmp3 cmp3, in Cmp4 cmp4) => cmp3.Value < cmp4.Value;
}

public struct StampFunctor : IForEach
{
    public void Invoke(in Stamp stamp) => _ = stamp;
}

public struct EntityStampFunctor : IForEachEntity
{
    public void Invoke(EntityRef entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode();
}

public struct EntityStampPairFunctor : IForEachEntity
{
    public void Invoke(EntityRef entity, in Stamp first, in Stamp second)
        => _ = entity.Index + first.GetHashCode() + second.GetHashCode();
}

public struct StampContextFunctor : IForEachContext<Context>
{
    public void Invoke(ref Context context, in Stamp stamp) => context.Value += stamp.GetHashCode();
}

public struct StampParallelContextFunctor : IForEachContext<Context>
{
    public void Invoke(in Context context, in Stamp stamp) => _ = context.Value + stamp.GetHashCode();
}

public struct EntityStampContextFunctor : IForEachContextEntity<Context>
{
    public void Invoke(ref Context context, EntityRef entity, in Stamp stamp)
        => context.Value += entity.Index + stamp.GetHashCode();
}

public struct GenericFunctor<TFirst, TSecond> : IForEach
{
    public void Invoke(in TFirst first, in TSecond second) { }
}

public struct GenericEntityFunctor<TFirst, TSecond> : IForEachEntity
{
    public void Invoke(EntityRef entity, in TFirst first, in TSecond second) { }
}

public struct GenericContextFunctor<TFirst, TSecond> : IForEachContext<Context>
{
    public void Invoke(ref Context context, in TFirst first, in TSecond second) => context.Value++;
}

public struct GenericEntityContextFunctor<T> : IForEachContextEntity<Context>
{
    public void Invoke(ref Context context, EntityRef entity, in T value) => context.Value += entity.Index;
}

public struct GenericParallelContextFunctor<T> : IForEachContext<Context>
{
    public void Invoke(in Context context, in T value) => _ = context.Value + value.GetHashCode();
}

public struct GenericParallelEntityContextFunctor<T> : IForEachContextEntity<Context>
{
    public void Invoke(in Context context, EntityRef entity, in T value) => _ = context.Value + entity.Index + value.GetHashCode();
}

public struct Cmp1Cmp2Comparer : IComponentComparer
{
    public int Invoke(in Cmp1 left1, in Cmp2 left2, in Cmp1 right1, in Cmp2 right2)
    {
        int first = left1.Value.CompareTo(right1.Value);
        return first != 0 ? first : left2.Value.CompareTo(right2.Value);
    }
}

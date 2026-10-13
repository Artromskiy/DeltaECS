using System;

using Leopotam.EcsLite;

namespace Ecs.CSharp.Benchmark.Contexts;

internal class LeopotamEcsLiteBaseContext : IDisposable
{

    public EcsWorld World { get; }

    public LeopotamEcsLiteBaseContext()
    {
        World = new EcsWorld();
    }

    public virtual void Dispose() => World.Destroy();
    internal struct Component1
    {
        public int Value;
    }

    internal struct Component2
    {
        public int Value;
    }

    internal struct Component3
    {
        public int Value;
    }
}

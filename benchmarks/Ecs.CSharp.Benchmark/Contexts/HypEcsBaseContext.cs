using System;

using HypEcs;

namespace Ecs.CSharp.Benchmark.Contexts;

internal class HypEcsBaseContext : IDisposable
{

    public World World { get; }

    public HypEcsBaseContext()
    {
        World = new World();
    }

    public virtual void Dispose()
    {
        // World.Destroy();
    }
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

using System;

using DefaultEcs;

namespace Ecs.CSharp.Benchmark.Contexts;

internal class DefaultEcsBaseContext : IDisposable
{

    public World World { get; }

    public DefaultEcsBaseContext()
    {
        World = new World();
    }

    public virtual void Dispose() => World.Dispose();
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

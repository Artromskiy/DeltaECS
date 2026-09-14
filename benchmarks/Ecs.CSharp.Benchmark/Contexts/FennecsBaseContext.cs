using System;
using fennecs;

namespace Ecs.CSharp.Benchmark.Contexts
{
    namespace Fennecs_Components
    {
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

    internal class FennecsBaseContext : IDisposable
    {
        public World World { get; }
        public EntityTemplate<Fennecs_Components.Component1> Component1Template { get; }
        public EntityTemplate<Fennecs_Components.Component1, Fennecs_Components.Component2> Component2Template { get; }
        public EntityTemplate<Fennecs_Components.Component1, Fennecs_Components.Component2, Fennecs_Components.Component3> Component3Template { get; }

        public FennecsBaseContext()
        {
            World = new World();
            Component1Template = World.Template().Needs<Fennecs_Components.Component1>();
            Component2Template = World.Template()
                .Needs<Fennecs_Components.Component1>()
                .Needs<Fennecs_Components.Component2>();
            Component3Template = World.Template()
                .Needs<Fennecs_Components.Component1>()
                .Needs<Fennecs_Components.Component2>()
                .Needs<Fennecs_Components.Component3>();
        }

        public void Dispose()
        {
            Component1Template.Dispose();
            Component2Template.Dispose();
            Component3Template.Dispose();
            World.Dispose();
        }
    }
}

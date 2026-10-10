#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Delta.ECS;
using UnityEngine;

namespace Delta.ECS.Unity
{
    public static class SceneWorldExtensions
    {
        /// <summary>Returns the live ECS context bound to this GameObject, or the default context when unbound.</summary>
        public static EntityContext GetEntity(this GameObject? view)
            => TryGetEntity(view, out EntityContext context) ? context : default;

        /// <summary>Returns whether this GameObject is bound to a live ECS entity.</summary>
        public static bool TryGetEntity(this GameObject? view, out EntityContext context)
        {
            if (view != null && SceneWorld.TryGetBoundEntity(view, out World world, out Entity entity))
            {
                context = new EntityContext(world, entity);
                return true;
            }

            context = default;
            return false;
        }

        /// <summary>Returns the entity bound to this GameObject, or the default entity when no binding exists.</summary>
        /// <param name="world">The bound live ECS world, or <see langword="null"/> when no binding exists.</param>
        public static Entity GetEntity(this GameObject? view, out World? world)
        {
            return TryGetEntity(view, out Entity entity, out world) ? entity : default;
        }

        /// <summary>Returns whether this GameObject is bound to a live ECS entity.</summary>
        public static bool TryGetEntity(
            this GameObject? view,
            out Entity entity,
            [NotNullWhen(true)] out World? world)
        {
            if (view != null && SceneWorld.TryGetBoundEntity(view, out World boundWorld, out entity))
            {
                world = boundWorld;
                return true;
            }

            entity = default;
            world = null;
            return false;
        }

        /// <summary>Returns the entity bound to this MonoBehaviour, or the default entity when no binding exists.</summary>
        /// <param name="world">The bound live ECS world, or <see langword="null"/> when no binding exists.</param>
        public static Entity GetEntity(this MonoBehaviour? behaviour, out World? world)
        {
            return TryGetEntity(behaviour, out Entity entity, out world) ? entity : default;
        }

        /// <summary>Returns the live ECS context bound to this MonoBehaviour, or the default context when unbound.</summary>
        public static EntityContext GetEntity(this MonoBehaviour? behaviour)
            => TryGetEntity(behaviour, out EntityContext context) ? context : default;

        /// <summary>Returns whether this MonoBehaviour is bound to a live ECS entity.</summary>
        public static bool TryGetEntity(this MonoBehaviour? behaviour, out EntityContext context)
        {
            if (behaviour != null)
            {
                return behaviour.gameObject.TryGetEntity(out context);
            }

            context = default;
            return false;
        }

        /// <summary>Returns whether this MonoBehaviour is bound to a live ECS entity.</summary>
        public static bool TryGetEntity(
            this MonoBehaviour? behaviour,
            out Entity entity,
            [NotNullWhen(true)] out World? world)
        {
            if (behaviour != null)
            {
                return behaviour.gameObject.TryGetEntity(out entity, out world);
            }

            entity = default;
            world = null;
            return false;
        }
    }
}

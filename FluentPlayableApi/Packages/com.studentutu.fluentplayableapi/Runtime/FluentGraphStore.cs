#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Playables;

namespace Studentutu.Fluentplayableapi
{
    /// <summary>
    /// Stores fluent metadata for built Unity playable graphs.
    /// </summary>
    public static class FluentGraphStore
    {
        private static readonly Dictionary<int, FluentGraphRegistry> RegistriesByGraphHash = new Dictionary<int, FluentGraphRegistry>();

        internal static void Register(PlayableGraph graph, FluentGraphRegistry registry)
        {
            if (!graph.IsValid())
            {
                throw new ArgumentException("Cannot register metadata for an invalid graph.", nameof(graph));
            }

            RegistriesByGraphHash[graph.GetHashCode()] = registry;
        }

        /// <summary>
        /// Gets fluent metadata registered during Build.
        /// </summary>
        public static FluentGraphRegistry Get(PlayableGraph graph)
        {
            if (!graph.IsValid())
            {
                throw new ArgumentException("Graph is not valid.", nameof(graph));
            }

            if (RegistriesByGraphHash.TryGetValue(graph.GetHashCode(), out FluentGraphRegistry registry))
            {
                return registry;
            }

            throw new InvalidOperationException("PlayableGraph has no fluent metadata. Build the graph through FluentBuilder first.");
        }
    }
}

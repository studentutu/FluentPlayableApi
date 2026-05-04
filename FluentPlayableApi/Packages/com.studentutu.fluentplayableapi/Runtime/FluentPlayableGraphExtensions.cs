#nullable enable

using UnityEngine.Playables;

namespace Studentutu.Fluentplayableapi
{
    /// <summary>
    /// Provides typed lookup helpers for built fluent playable graphs.
    /// </summary>
    public static class FluentPlayableGraphExtensions
    {
        /// <summary>
        /// Resolves a playable by exact path or unique short name.
        /// </summary>
        public static TPlayable Resolve<TPlayable>(this PlayableGraph graph, string key)
            where TPlayable : struct, IPlayable
        {
            return FluentGraphStore.Get(graph).Resolve<TPlayable>(key);
        }

        /// <summary>
        /// Resolves a named input index for a destination playable.
        /// </summary>
        public static int InputIndex<TPlayable>(this PlayableGraph graph, TPlayable destination, string inputName)
            where TPlayable : struct, IPlayable
        {
            return FluentGraphStore.Get(graph).InputIndex(destination, inputName);
        }

        /// <summary>
        /// Resolves a named input index by destination node key.
        /// </summary>
        public static int InputIndex(this PlayableGraph graph, string nodeKey, string inputName)
        {
            return FluentGraphStore.Get(graph).InputIndex(nodeKey, inputName);
        }
    }
}

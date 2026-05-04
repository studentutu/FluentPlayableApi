#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Studentutu.Fluentplayableapi
{
    /// <summary>
    /// Stores authored fluent graph paths and named input indices.
    /// </summary>
    public sealed class FluentGraphRegistry
    {
        private readonly Dictionary<string, NodeRecord> _nodesByPath = new Dictionary<string, NodeRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<NodeRecord>> _nodesByShortName = new Dictionary<string, List<NodeRecord>>(StringComparer.Ordinal);
        private readonly HashSet<string> _reservedScopePaths = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<PlayableHandle, Dictionary<string, int>> _inputNamesByDestination = new Dictionary<PlayableHandle, Dictionary<string, int>>();
        private readonly Dictionary<PlayableHandle, Dictionary<int, string>> _inputIndicesByDestination = new Dictionary<PlayableHandle, Dictionary<int, string>>();

        /// <summary>
        /// Registers a named playable under an exact path.
        /// </summary>
        public void RegisterNode<TPlayable>(string path, TPlayable playable)
            where TPlayable : struct, IPlayable
        {
            string normalizedPath = TopologyPath.Normalize(path);
            if (_reservedScopePaths.Contains(normalizedPath))
            {
                throw new InvalidOperationException($"Path '{normalizedPath}' is already reserved by a scope.");
            }

            RegisterNodeRecord(normalizedPath, playable);
        }

        /// <summary>
        /// Reserves a virtual scope path before the scope root is known.
        /// </summary>
        public void RegisterScopePath(string path)
        {
            string normalizedPath = TopologyPath.Normalize(path);
            if (_nodesByPath.ContainsKey(normalizedPath))
            {
                throw new InvalidOperationException($"Path '{normalizedPath}' is already registered by a node.");
            }

            if (!_reservedScopePaths.Add(normalizedPath))
            {
                throw new InvalidOperationException($"Scope '{normalizedPath}' is already registered.");
            }
        }

        /// <summary>
        /// Registers a scope path as a playable root.
        /// </summary>
        public void RegisterScope<TPlayable>(string path, TPlayable root)
            where TPlayable : struct, IPlayable
        {
            string normalizedPath = TopologyPath.Normalize(path);
            if (!_reservedScopePaths.Contains(normalizedPath))
            {
                RegisterScopePath(normalizedPath);
            }

            RegisterNodeRecord(normalizedPath, root);
        }

        /// <summary>
        /// Registers a stable input name for a destination playable input index.
        /// </summary>
        public void RegisterInputName<TPlayable>(TPlayable destination, int index, string name)
            where TPlayable : struct, IPlayable
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Input name cannot be null, empty, or whitespace.", nameof(name));
            }

            PlayableHandle handle = destination.GetHandle();
            if (!_inputNamesByDestination.TryGetValue(handle, out Dictionary<string, int> names))
            {
                names = new Dictionary<string, int>(StringComparer.Ordinal);
                _inputNamesByDestination.Add(handle, names);
            }

            if (!_inputIndicesByDestination.TryGetValue(handle, out Dictionary<int, string> indices))
            {
                indices = new Dictionary<int, string>();
                _inputIndicesByDestination.Add(handle, indices);
            }

            if (names.TryGetValue(name, out int existingIndex) && existingIndex != index)
            {
                throw new InvalidOperationException($"Input name '{name}' is already registered for input {existingIndex}.");
            }

            if (indices.TryGetValue(index, out string existingName) && existingName != name)
            {
                throw new InvalidOperationException($"Input {index} is already registered as '{existingName}'.");
            }

            names[name] = index;
            indices[index] = name;
        }

        /// <summary>
        /// Resolves a playable by exact path or unique short name.
        /// </summary>
        public TPlayable Resolve<TPlayable>(string key)
            where TPlayable : struct, IPlayable
        {
            NodeRecord record = ResolveRecord(key);
            if (record.PlayableType != typeof(TPlayable))
            {
                throw new InvalidOperationException(
                    $"Playable '{key}' is registered as {record.PlayableType.Name}, not {typeof(TPlayable).Name}.");
            }

            return (TPlayable)record.BoxedPlayable;
        }

        /// <summary>
        /// Resolves a playable by exact path or unique short name without a typed handle.
        /// </summary>
        public Playable ResolveUntyped(string key)
        {
            return ToPlayable(ResolveRecord(key));
        }

        /// <summary>
        /// Resolves a named input index for a destination playable.
        /// </summary>
        public int InputIndex<TPlayable>(TPlayable destination, string inputName)
            where TPlayable : struct, IPlayable
        {
            PlayableHandle handle = destination.GetHandle();
            if (_inputNamesByDestination.TryGetValue(handle, out Dictionary<string, int> names) &&
                names.TryGetValue(inputName, out int index))
            {
                return index;
            }

            throw new KeyNotFoundException($"Input name '{inputName}' is not registered for the destination playable.");
        }

        /// <summary>
        /// Resolves a named input index by destination node key.
        /// </summary>
        public int InputIndex(string nodeKey, string inputName)
        {
            PlayableHandle destination = ResolveHandle(nodeKey);
            if (_inputNamesByDestination.TryGetValue(destination, out Dictionary<string, int> names) &&
                names.TryGetValue(inputName, out int index))
            {
                return index;
            }

            throw new KeyNotFoundException($"Input name '{inputName}' is not registered for '{nodeKey}'.");
        }

        internal IEnumerable<PlayableHandle> RegisteredPlayableHandles
        {
            get
            {
                foreach (NodeRecord record in _nodesByPath.Values)
                {
                    yield return record.Handle;
                }
            }
        }

        internal PlayableHandle ResolveHandle(string key)
        {
            return ResolveRecord(key).Handle;
        }

        private void RegisterNodeRecord<TPlayable>(string path, TPlayable playable)
            where TPlayable : struct, IPlayable
        {
            if (!playable.IsValid())
            {
                throw new ArgumentException($"Playable for '{path}' is not valid.", nameof(playable));
            }

            if (_nodesByPath.ContainsKey(path))
            {
                throw new InvalidOperationException($"Path '{path}' is already registered.");
            }

            var record = new NodeRecord(path, TopologyPath.NameOf(path), playable, playable.GetHandle(), typeof(TPlayable));
            _nodesByPath.Add(path, record);

            if (!_nodesByShortName.TryGetValue(record.ShortName, out List<NodeRecord> records))
            {
                records = new List<NodeRecord>();
                _nodesByShortName.Add(record.ShortName, records);
            }

            records.Add(record);
        }

        private NodeRecord ResolveRecord(string key)
        {
            string normalizedKey = TopologyPath.Normalize(key);
            if (TopologyPath.IsPath(key))
            {
                if (_nodesByPath.TryGetValue(normalizedKey, out NodeRecord exactRecord))
                {
                    return exactRecord;
                }

                throw new KeyNotFoundException($"No playable is registered at path '{normalizedKey}'.");
            }

            if (!_nodesByShortName.TryGetValue(normalizedKey, out List<NodeRecord> records))
            {
                throw new KeyNotFoundException($"No playable is registered with short name '{normalizedKey}'.");
            }

            if (records.Count > 1)
            {
                throw new InvalidOperationException($"Short name '{normalizedKey}' is ambiguous. Use an exact path.");
            }

            return records[0];
        }

        private static Playable ToPlayable(NodeRecord record)
        {
            if (record.BoxedPlayable is Playable playable)
            {
                return playable;
            }

            if (record.BoxedPlayable is AnimationMixerPlayable mixer)
            {
                return mixer;
            }

            if (record.BoxedPlayable is AnimationLayerMixerPlayable layerMixer)
            {
                return layerMixer;
            }

            if (record.BoxedPlayable is AnimationClipPlayable clip)
            {
                return clip;
            }

            if (record.BoxedPlayable is AnimationScriptPlayable script)
            {
                return script;
            }

            throw new NotSupportedException($"Untyped playable resolution is not supported for {record.PlayableType.Name}.");
        }

        private readonly struct NodeRecord
        {
            public NodeRecord(string path, string shortName, object boxedPlayable, PlayableHandle handle, Type playableType)
            {
                Path = path;
                ShortName = shortName;
                BoxedPlayable = boxedPlayable;
                Handle = handle;
                PlayableType = playableType;
            }

            public string Path { get; }
            public string ShortName { get; }
            public object BoxedPlayable { get; }
            public PlayableHandle Handle { get; }
            public Type PlayableType { get; }
        }
    }
}

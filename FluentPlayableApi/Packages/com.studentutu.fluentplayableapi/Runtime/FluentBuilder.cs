#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Fluentplayableapi
{
    /// <summary>
    /// Declares, connects, names, and validates Unity playables through a compact fluent API.
    /// </summary>
    public sealed class FluentBuilder : IDisposable
    {
        private readonly FluentGraphRegistry _registry = new FluentGraphRegistry();
        private readonly List<DeclaredInput> _declaredInputs = new List<DeclaredInput>();
        private readonly HashSet<DestinationInputKey> _declaredDestinationInputs = new HashSet<DestinationInputKey>();
        private readonly HashSet<SourceOutputKey> _claimedSourceOutputs = new HashSet<SourceOutputKey>();
        private readonly HashSet<PlayableOutputHandle> _claimedOutputs = new HashSet<PlayableOutputHandle>();

        private readonly Dictionary<PlayableHandle, List<PlayableHandle>> _sourceInputsByDestination =
            new Dictionary<PlayableHandle, List<PlayableHandle>>();

        private readonly List<PlayableHandle> _outputRoots = new List<PlayableHandle>();
        private readonly HashSet<PendingInput> _suspendedPendingInputs = new HashSet<PendingInput>();
        private PendingInput? _pendingInput;
        private bool _built;
        private bool _disposed;

        private FluentBuilder(PlayableGraph graph)
        {
            Graph = graph;
        }

        /// <summary>
        /// Gets the Unity playable graph being authored.
        /// </summary>
        public PlayableGraph Graph { get; }

        /// <summary>
        /// Creates a new Unity playable graph.
        /// </summary>
        public static FluentBuilder Create(string name = "PlayableGraph")
        {
            return new FluentBuilder(PlayableGraph.Create(name));
        }

        /// <summary>
        /// Reuses an existing Unity playable graph instance.
        /// </summary>
        public static FluentBuilder Create(PlayableGraph graph)
        {
            if (!graph.IsValid())
            {
                throw new ArgumentException("Graph must be valid.", nameof(graph));
            }

            return new FluentBuilder(graph);
        }

        /// <summary>
        /// Creates an animation output owned by the graph.
        /// </summary>
        public FluentBuilder Output(Animator animator, out AnimationPlayableOutput output,
            string name = "AnimationOutput")
        {
            EnsureNotDisposed();
            if (animator == null)
            {
                throw new ArgumentNullException(nameof(animator));
            }

            output = AnimationPlayableOutput.Create(Graph, name, animator);
            return this;
        }

        /// <summary>
        /// Declares the next playable as the source for an animation output.
        /// </summary>
        public FluentBuilder Input(AnimationPlayableOutput output, int index = 0)
        {
            EnsureNotDisposed();
            DeclareOutputInput(output, index);
            return this;
        }

        /// <summary>
        /// Declares the next playable as the source for a destination input.
        /// </summary>
        public FluentBuilder Input<TDestination>(TDestination destination, int index, string? name = null)
            where TDestination : struct, IPlayable
        {
            EnsureNotDisposed();
            DeclarePlayableInput(destination, index, name);
            return this;
        }

        /// <summary>
        /// Expands the destination input count by one and declares the next playable as that input's source.
        /// </summary>
        public FluentBuilder AddInput<TDestination>(TDestination destination, string name, out int index)
            where TDestination : struct, IPlayable
        {
            EnsureNotDisposed();
            index = destination.GetInputCount();
            destination.SetInputCount(index + 1);
            DeclarePlayableInput(destination, index, name);
            return this;
        }

        /// <summary>
        /// Opens a virtual scope for paths declared by the returned scope builder.
        /// </summary>
        public TopologyScope Scope(string path)
        {
            EnsureNotDisposed();
            string normalizedPath = TopologyPath.Normalize(path);
            return BeginScope(normalizedPath);
        }

        /// <summary>
        /// Creates and optionally registers an animation mixer or layer mixer.
        /// </summary>
        public FluentBuilder WithMixer<TMixer>(int inputCount, out TMixer mixer, string? name = null,
            int outputCount = 1)
            where TMixer : struct, IPlayable
        {
            EnsureNotDisposed();
            mixer = CreateMixerWithGraph<TMixer>(inputCount, outputCount);
            RegisterAndAttachCreatedPlayable(mixer, name, null);
            return this;
        }

        /// <summary>
        /// Creates and optionally registers an animation clip playable with safe defaults.
        /// </summary>
        public FluentBuilder WithClip(AnimationClip clip, out AnimationClipPlayable playable, string? name = null,
            bool paused = true)
        {
            EnsureNotDisposed();
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            playable = AnimationClipPlayable.Create(Graph, clip);
            playable.SetOutputCount(1);
            playable.SetApplyFootIK(false);
            if (paused)
            {
                playable.Pause();
            }
            else
            {
                playable.Play();
            }

            RegisterAndAttachCreatedPlayable(playable, name, null);
            return this;
        }

        /// <summary>
        /// Creates and optionally registers a script playable.
        /// </summary>
        public FluentBuilder WithScript<TBehaviour>(
            out ScriptPlayable<TBehaviour> playable,
            int inputCount = 0,
            int outputCount = 1,
            string? name = null)
            where TBehaviour : PlayableBehaviour, new()
        {
            EnsureNotDisposed();
            ValidateCounts(inputCount, outputCount);
            playable = ScriptPlayable<TBehaviour>.Create(Graph, inputCount);
            playable.SetOutputCount(outputCount);
            RegisterAndAttachCreatedPlayable(playable, name, null);
            return this;
        }

        /// <summary>
        /// Creates a playable through a caller-provided factory and optionally registers it.
        /// </summary>
        public FluentBuilder WithPlayable<TPlayable>(
            Func<PlayableGraph, TPlayable> factory,
            out TPlayable playable,
            string? name = null)
            where TPlayable : struct, IPlayable
        {
            EnsureNotDisposed();
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            playable = factory(Graph);
            EnsureValidPlayable(playable, nameof(playable));
            if (playable.GetOutputCount() == 0)
            {
                playable.SetOutputCount(1);
            }

            RegisterAndAttachCreatedPlayable(playable, name, null);
            return this;
        }

        /// <summary>
        /// Connects an existing playable to the pending input.
        /// </summary>
        public FluentBuilder WithPlayable<TPlayable>(TPlayable playable, int sourceOutputPort = 0)
            where TPlayable : struct, IPlayable
        {
            EnsureNotDisposed();
            AttachExistingPlayable(playable, sourceOutputPort);
            return this;
        }

        /// <summary>
        /// Resolves a playable and optionally connects it to the pending input.
        /// </summary>
        public FluentBuilder WithPlayable<TPlayable>(string key, out TPlayable playable, int sourceOutputPort = 0)
            where TPlayable : struct, IPlayable
        {
            EnsureNotDisposed();
            playable = _registry.Resolve<TPlayable>(key);
            if (_pendingInput != null)
            {
                AttachPlayableToPending(playable, sourceOutputPort, _pendingInput);
                _pendingInput = null;
            }

            return this;
        }

        /// <summary>
        /// Assigns a weight to a destination input.
        /// </summary>
        public FluentBuilder WithWeight<TDestination>(TDestination destination, int input, float weight)
            where TDestination : struct, IPlayable
        {
            EnsureNotDisposed();
            ValidatePlayableInputIndex(destination, input);
            destination.SetInputWeight(input, weight);
            return this;
        }

        /// <summary>
        /// Assigns a weight to a named destination input.
        /// </summary>
        public FluentBuilder WithWeight<TDestination>(TDestination destination, string inputName, float weight)
            where TDestination : struct, IPlayable
        {
            EnsureNotDisposed();
            return WithWeight(destination, _registry.InputIndex(destination, inputName), weight);
        }

        /// <summary>
        /// Assigns a weight to a playable output.
        /// </summary>
        public FluentBuilder WithWeight<TOutput>(TOutput output, float weight)
            where TOutput : struct, IPlayableOutput
        {
            EnsureNotDisposed();
            output.SetWeight(weight);
            return this;
        }

        /// <summary>
        /// Configures an animation layer mixer input as override or additive and optionally assigns an avatar mask.
        /// </summary>
        public FluentBuilder Layer(AnimationLayerMixerPlayable layerMixer, int input, bool additive = false,
            AvatarMask? mask = null)
        {
            EnsureNotDisposed();
            ValidatePlayableInputIndex(layerMixer, input);
            layerMixer.SetLayerAdditive((uint)input, additive);
            if (mask != null)
            {
                layerMixer.SetLayerMaskFromAvatarMask((uint)input, mask);
            }

            return this;
        }

        /// <summary>
        /// Validates fluent declarations and optionally starts the graph.
        /// </summary>
        public PlayableGraph Build(bool play = true)
        {
            EnsureNotDisposed();
            ValidateBuild();
            if (play)
            {
                Graph.Play();
            }

            _built = true;
            return Graph;
        }

        /// <summary>
        /// Resolves a playable by exact path or unique short name from this builder instance.
        /// </summary>
        public TPlayable Resolve<TPlayable>(string key)
            where TPlayable : struct, IPlayable
        {
            EnsureNotDisposed();
            EnsureBuilt();
            return _registry.Resolve<TPlayable>(key);
        }

        /// <summary>
        /// Resolves a named input index for a destination playable from this builder instance.
        /// </summary>
        public int InputIndex<TPlayable>(TPlayable destination, string inputName)
            where TPlayable : struct, IPlayable
        {
            EnsureNotDisposed();
            EnsureBuilt();
            return _registry.InputIndex(destination, inputName);
        }

        /// <summary>
        /// Resolves a named input index by destination node key from this builder instance.
        /// </summary>
        public int InputIndex(string nodeKey, string inputName)
        {
            EnsureNotDisposed();
            EnsureBuilt();
            return _registry.InputIndex(nodeKey, inputName);
        }

        /// <summary>
        /// Clears fluent metadata owned by this builder instance.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _pendingInput = null;
            _declaredInputs.Clear();
            _declaredDestinationInputs.Clear();
            _claimedSourceOutputs.Clear();
            _claimedOutputs.Clear();
            _sourceInputsByDestination.Clear();
            _outputRoots.Clear();
            _suspendedPendingInputs.Clear();
            _registry.Clear();
            _built = false;

            _disposed = true;
        }

        internal TopologyScope BeginScope(string normalizedPath)
        {
            EnsureNotDisposed();
            _registry.RegisterScopePath(normalizedPath);
            PendingInput? parentPendingInput = _pendingInput;
            if (parentPendingInput != null)
            {
                _suspendedPendingInputs.Add(parentPendingInput);
                _pendingInput = null;
            }

            return new TopologyScope(this, normalizedPath, parentPendingInput);
        }

        internal void CompileScope<TPlayable>(TopologyScope scope, TPlayable root)
            where TPlayable : struct, IPlayable
        {
            EnsureNotDisposed();
            if (_pendingInput != null)
            {
                throw new InvalidOperationException($"Scope '{scope.Path}' has an unconsumed pending input.");
            }

            _registry.RegisterScope(scope.Path, root);
            if (scope.ParentPendingInput != null)
            {
                AttachPlayableToPending(root, 0, scope.ParentPendingInput);
                _suspendedPendingInputs.Remove(scope.ParentPendingInput);
            }
        }

        internal void DeclareOutputInput(AnimationPlayableOutput output, int sourceOutputPort)
        {
            EnsureNotDisposed();
            if (!output.IsOutputValid())
            {
                throw new ArgumentException("Output must be valid.", nameof(output));
            }

            if (sourceOutputPort < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOutputPort),
                    "Source output port must be non-negative.");
            }

            EnsureNoPendingInput();
            PlayableOutputHandle handle = output.GetHandle();
            if (!_claimedOutputs.Add(handle))
            {
                throw new InvalidOperationException("Output already has a fluent source declaration.");
            }

            var declaredInput = new DeclaredInput();
            _declaredInputs.Add(declaredInput);
            _pendingInput = PendingInput.ForOutput(output, sourceOutputPort, declaredInput);
        }

        internal void DeclarePlayableInput<TDestination>(TDestination destination, int index, string? name)
            where TDestination : struct, IPlayable
        {
            EnsureNotDisposed();
            EnsureValidPlayable(destination, nameof(destination));
            ValidatePlayableInputIndex(destination, index);
            EnsureNoPendingInput();

            var destinationKey = new DestinationInputKey(destination.GetHandle(), index);
            if (!_declaredDestinationInputs.Add(destinationKey))
            {
                throw new InvalidOperationException($"Destination input {index} is already declared.");
            }

            if (name != null)
            {
                _registry.RegisterInputName(destination, index, name);
            }

            var declaredInput = new DeclaredInput();
            _declaredInputs.Add(declaredInput);
            _pendingInput = PendingInput.ForPlayable(destination, index, declaredInput);
        }

        internal void RegisterAndAttachCreatedPlayable<TPlayable>(TPlayable playable, string? name, string? scopePath)
            where TPlayable : struct, IPlayable
        {
            EnsureNotDisposed();
            EnsureValidPlayable(playable, nameof(playable));
            if (name != null)
            {
                string nodePath = scopePath == null ? TopologyPath.Normalize(name) : TopologyPath.Join(scopePath, name);
                _registry.RegisterNode(nodePath, playable);
            }

            if (_pendingInput != null)
            {
                AttachPlayableToPending(playable, 0, _pendingInput);
                _pendingInput = null;
            }
        }

        internal void AttachExistingPlayable<TPlayable>(TPlayable playable, int sourceOutputPort)
            where TPlayable : struct, IPlayable
        {
            EnsureNotDisposed();
            EnsureValidPlayable(playable, nameof(playable));
            if (_pendingInput == null)
            {
                throw new InvalidOperationException("Existing playable attachment requires a pending input.");
            }

            AttachPlayableToPending(playable, sourceOutputPort, _pendingInput);
            _pendingInput = null;
        }

        internal void WithResolvedPlayable<TPlayable>(string key, out TPlayable playable, int sourceOutputPort)
            where TPlayable : struct, IPlayable
        {
            EnsureNotDisposed();
            playable = _registry.Resolve<TPlayable>(key);
            if (_pendingInput != null)
            {
                AttachPlayableToPending(playable, sourceOutputPort, _pendingInput);
                _pendingInput = null;
            }
        }

        private void AttachPlayableToPending<TPlayable>(TPlayable playable, int requestedSourceOutputPort,
            PendingInput pendingInput)
            where TPlayable : struct, IPlayable
        {
            int sourceOutputPort = pendingInput.IsOutput ? pendingInput.SourceOutputPort : requestedSourceOutputPort;
            ValidateSourceOutput(playable, sourceOutputPort);

            SourceOutputKey sourceOutputKey = new SourceOutputKey(playable.GetHandle(), sourceOutputPort);
            if (!_claimedSourceOutputs.Add(sourceOutputKey))
            {
                throw new InvalidOperationException($"Source output port {sourceOutputPort} is already connected.");
            }

            bool connected = pendingInput.Attach(Graph, playable, sourceOutputPort);
            if (!connected)
            {
                throw new InvalidOperationException(
                    $"Unity rejected connection to destination input {pendingInput.DestinationInput}.");
            }

            if (pendingInput.IsOutput)
            {
                _outputRoots.Add(playable.GetHandle());
                pendingInput.DeclaredInput.Consumed = true;
                return;
            }

            PlayableHandle destinationHandle = pendingInput.DestinationHandle;
            if (!_sourceInputsByDestination.TryGetValue(destinationHandle, out List<PlayableHandle> inputs))
            {
                inputs = new List<PlayableHandle>();
                _sourceInputsByDestination.Add(destinationHandle, inputs);
            }

            inputs.Add(playable.GetHandle());
            pendingInput.DeclaredInput.Consumed = true;
        }

        private void ValidateBuild()
        {
            if (_pendingInput != null || _suspendedPendingInputs.Count > 0)
            {
                throw new InvalidOperationException("A pending input was declared but not consumed.");
            }

            for (int i = 0; i < _declaredInputs.Count; i++)
            {
                if (!_declaredInputs[i].Consumed)
                {
                    throw new InvalidOperationException("A declared input was never consumed.");
                }
            }

            ValidateReachability();
        }

        private void ValidateReachability()
        {
            var reachable = new HashSet<PlayableHandle>();
            var stack = new Stack<PlayableHandle>(_outputRoots);

            while (stack.Count > 0)
            {
                PlayableHandle current = stack.Pop();
                if (!reachable.Add(current))
                {
                    continue;
                }

                if (_sourceInputsByDestination.TryGetValue(current, out List<PlayableHandle> inputs))
                {
                    for (int i = 0; i < inputs.Count; i++)
                    {
                        stack.Push(inputs[i]);
                    }
                }
            }

            foreach (PlayableHandle registeredPlayable in _registry.RegisteredPlayableHandles)
            {
                if (!reachable.Contains(registeredPlayable))
                {
                    throw new InvalidOperationException(
                        "A registered playable is unreachable from every authored output.");
                }
            }
        }

        private TMixer CreateMixerWithGraph<TMixer>(int inputCount, int outputCount)
            where TMixer : struct, IPlayable
        {
            ValidateCounts(inputCount, outputCount);

            if (typeof(TMixer) == typeof(AnimationMixerPlayable))
            {
                var mixer = AnimationMixerPlayable.Create(Graph, inputCount);
                mixer.SetOutputCount(outputCount);
                return (TMixer)(object)mixer;
            }

            if (typeof(TMixer) == typeof(AnimationLayerMixerPlayable))
            {
                var mixer = AnimationLayerMixerPlayable.Create(Graph, inputCount);
                mixer.SetOutputCount(outputCount);
                return (TMixer)(object)mixer;
            }

            throw new NotSupportedException(
                $"Mixer type {typeof(TMixer).Name} is not supported. Use AnimationMixerPlayable or AnimationLayerMixerPlayable.");
        }

        private static void ValidateCounts(int inputCount, int outputCount)
        {
            if (inputCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputCount), "Input count must be non-negative.");
            }

            if (outputCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(outputCount), "Output count must be positive.");
            }
        }

        private static void EnsureValidPlayable<TPlayable>(TPlayable playable, string parameterName)
            where TPlayable : struct, IPlayable
        {
            if (!playable.IsValid())
            {
                throw new ArgumentException("Playable must be valid.", parameterName);
            }
        }

        private static void ValidatePlayableInputIndex<TPlayable>(TPlayable playable, int index)
            where TPlayable : struct, IPlayable
        {
            if (index < 0 || index >= playable.GetInputCount())
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Input index {index} is outside input count {playable.GetInputCount()}.");
            }
        }

        private static void ValidateSourceOutput<TPlayable>(TPlayable playable, int sourceOutputPort)
            where TPlayable : struct, IPlayable
        {
            if (sourceOutputPort < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOutputPort),
                    "Source output port must be non-negative.");
            }

            if (sourceOutputPort >= playable.GetOutputCount())
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceOutputPort),
                    $"Source output port {sourceOutputPort} is outside output count {playable.GetOutputCount()}.");
            }
        }

        private void EnsureNoPendingInput()
        {
            if (_pendingInput != null)
            {
                throw new InvalidOperationException("Consume the pending input before declaring another input.");
            }
        }

        private void EnsureBuilt()
        {
            if (!_built)
            {
                throw new InvalidOperationException("Build the fluent builder before querying fluent metadata.");
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FluentBuilder));
            }
        }

        private readonly struct DestinationInputKey : IEquatable<DestinationInputKey>
        {
            private readonly PlayableHandle _destination;
            private readonly int _input;
            private readonly int _hashCode;

            public DestinationInputKey(PlayableHandle destination, int input)
            {
                _destination = destination;
                _input = input;

                _hashCode = (_destination.GetHashCode() * 397) ^ _input;
            }

            public bool Equals(DestinationInputKey other)
            {
                return _destination.Equals(other._destination) && _input == other._input;
            }

            public override bool Equals(object? obj)
            {
                return obj is DestinationInputKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }
        }

        private readonly struct SourceOutputKey : IEquatable<SourceOutputKey>
        {
            private readonly PlayableHandle _source;
            private readonly int _output;
            private readonly int _hashCode;

            public SourceOutputKey(PlayableHandle source, int output)
            {
                _source = source;
                _output = output;

                _hashCode = (_source.GetHashCode() * 397) ^ _output;
            }

            public bool Equals(SourceOutputKey other)
            {
                return _source.Equals(other._source) && _output == other._output;
            }

            public override bool Equals(object? obj)
            {
                return obj is SourceOutputKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }
        }
    }
}
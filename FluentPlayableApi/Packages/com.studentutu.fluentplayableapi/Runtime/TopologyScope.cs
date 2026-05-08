#nullable enable

using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FluentPlayableApi
{
    /// <summary>
    /// Declares nodes under one fluent topology path.
    /// </summary>
    public sealed class TopologyScope
    {
        private readonly FluentBuilder _builder;

        internal TopologyScope(FluentBuilder builder, string path, PendingInput? parentPendingInput)
        {
            _builder = builder;
            Path = path;
            ParentPendingInput = parentPendingInput;
        }

        /// <summary>
        /// Gets the canonical path for this scope.
        /// </summary>
        public string Path { get; }

        internal PendingInput? ParentPendingInput { get; }

        /// <summary>
        /// Opens a child scope below the current scope path.
        /// </summary>
        public TopologyScope Scope(string path)
        {
            return _builder.BeginScope(TopologyPath.Join(Path, path));
        }

        /// <summary>
        /// Declares the next playable as the source for an animation output.
        /// </summary>
        public TopologyScope Input(AnimationPlayableOutput output, int sourceOutputPort = 0)
        {
            return Input<AnimationPlayableOutput>(output, sourceOutputPort);
        }

        /// <summary>
        /// Declares the next playable as the source for a playable output.
        /// </summary>
        public TopologyScope Input<TOutput>(TOutput output, int sourceOutputPort = 0)
            where TOutput : struct, IPlayableOutput
        {
            _builder.DeclareOutputInput(output, sourceOutputPort);
            return this;
        }

        /// <summary>
        /// Declares the next playable as the source for a destination input.
        /// </summary>
        public TopologyScope Input<TDestination>(TDestination destination, int index, string? name = null)
            where TDestination : struct, IPlayable
        {
            _builder.DeclarePlayableInput(destination, index, name);
            return this;
        }

        /// <summary>
        /// Expands the destination input count by one and declares the next playable as that input's source.
        /// </summary>
        public TopologyScope AddInput<TDestination>(TDestination destination, string name, out int index)
            where TDestination : struct, IPlayable
        {
            _builder.AddInput(destination, name, out index);
            return this;
        }

        /// <summary>
        /// Creates and registers an animation mixer or layer mixer under this scope.
        /// </summary>
        public TopologyScope WithMixer<TMixer>(int inputCount, out TMixer mixer, string? name = null, int outputCount = 1)
            where TMixer : struct, IPlayable
        {
            _builder.EnsureNodePathAvailable(name, Path);
            _builder.WithMixer(inputCount, out mixer, null, outputCount);
            if (name != null)
            {
                _builder.RegisterAndAttachCreatedPlayable(mixer, name, Path);
            }

            return this;
        }

        /// <summary>
        /// Creates and registers an animation clip playable under this scope.
        /// </summary>
        public TopologyScope WithClip(AnimationClip clip, out AnimationClipPlayable playable, string? name = null, bool paused = true)
        {
            _builder.EnsureNodePathAvailable(name, Path);
            _builder.WithClip(clip, out playable, null, paused);
            if (name != null)
            {
                _builder.RegisterAndAttachCreatedPlayable(playable, name, Path);
            }

            return this;
        }

        /// <summary>
        /// Creates and registers a script playable under this scope.
        /// </summary>
        public TopologyScope WithScript<TBehaviour>(
            out ScriptPlayable<TBehaviour> playable,
            int inputCount = 0,
            int outputCount = 1,
            string? name = null)
            where TBehaviour : PlayableBehaviour, new()
        {
            _builder.EnsureNodePathAvailable(name, Path);
            _builder.WithScript(out playable, inputCount, outputCount, null);
            if (name != null)
            {
                _builder.RegisterAndAttachCreatedPlayable(playable, name, Path);
            }

            return this;
        }

        /// <summary>
        /// Creates and registers a playable from a caller-provided factory under this scope.
        /// </summary>
        public TopologyScope WithPlayable<TPlayable>(
            Func<PlayableGraph, TPlayable> factory,
            out TPlayable playable,
            string? name = null)
            where TPlayable : struct, IPlayable
        {
            _builder.EnsureNodePathAvailable(name, Path);
            _builder.WithPlayable(factory, out playable, null);
            if (name != null)
            {
                _builder.RegisterAndAttachCreatedPlayable(playable, name, Path);
            }

            return this;
        }

        /// <summary>
        /// Connects an existing playable to the pending input.
        /// </summary>
        public TopologyScope WithPlayable<TPlayable>(TPlayable playable, int sourceOutputPort = 0)
            where TPlayable : struct, IPlayable
        {
            _builder.WithPlayable(playable, sourceOutputPort);
            return this;
        }

        /// <summary>
        /// Resolves a playable and optionally connects it to the pending input.
        /// </summary>
        public TopologyScope WithPlayable<TPlayable>(string key, out TPlayable playable, int sourceOutputPort = 0)
            where TPlayable : struct, IPlayable
        {
            _builder.WithResolvedPlayable(key, out playable, sourceOutputPort);
            return this;
        }

        /// <summary>
        /// Assigns a weight to a destination input.
        /// </summary>
        public TopologyScope WithWeight<TDestination>(TDestination destination, int input, float weight)
            where TDestination : struct, IPlayable
        {
            _builder.WithWeight(destination, input, weight);
            return this;
        }

        /// <summary>
        /// Assigns a weight to a named destination input.
        /// </summary>
        public TopologyScope WithWeight<TDestination>(TDestination destination, string inputName, float weight)
            where TDestination : struct, IPlayable
        {
            _builder.WithWeight(destination, inputName, weight);
            return this;
        }

        /// <summary>
        /// Assigns a weight to a playable output.
        /// </summary>
        public TopologyScope WithWeight<TOutput>(TOutput output, float weight)
            where TOutput : struct, IPlayableOutput
        {
            _builder.WithWeight(output, weight);
            return this;
        }

        /// <summary>
        /// Configures an animation layer mixer input.
        /// </summary>
        public TopologyScope Layer(AnimationLayerMixerPlayable layerMixer, int input, bool additive = false, AvatarMask? mask = null)
        {
            _builder.Layer(layerMixer, input, additive, mask);
            return this;
        }

        /// <summary>
        /// Registers the current scope path as a reusable playable root and returns the parent builder.
        /// </summary>
        public FluentBuilder CompileAs<TPlayable>(TPlayable root)
            where TPlayable : struct, IPlayable
        {
            _builder.CompileScope(this, root);
            return _builder;
        }
    }
}

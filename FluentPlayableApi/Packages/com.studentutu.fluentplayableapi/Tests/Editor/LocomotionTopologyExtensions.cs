#nullable enable

using System;
using System.Collections.Generic;
using Fluentplayableapi;
using UnityEngine;
using UnityEngine.Animations;

namespace Fluentplayableapi.Tests
{
    /// <summary>
    /// Example descriptor used by the locomotion topology extension.
    /// </summary>
    [Serializable]
    public sealed class LocomotionTopologyData
    {
        public AnimationClip? Locomotion;
        public List<AnimationClip>? Leans;
        public AvatarMask? LeanMask;
        public int ActiveLeanIndex;
    }

    /// <summary>
    /// Example project extension methods that compose a reusable scoped locomotion topology.
    /// </summary>
    public static class LocomotionTopologyExtensions
    {
        /// <summary>
        /// [INTEGRATION] Range: valid builder, data, and scope. 
        /// Condition: caller has declared the parent input. 
        /// Output: scope root mixer.
        /// </summary>
        public static FluentBuilder WithLocomotionTopology(
            this FluentBuilder builder,
            LocomotionTopologyData data,
            string scope,
            out AnimationLayerMixerPlayable output)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data.Locomotion == null)
                throw new ArgumentException("Locomotion clip is required.", nameof(data));

            int leanClipCount = CountValidClips(data.Leans);

            return builder.Scope(scope)
                .WithMixer<AnimationLayerMixerPlayable>(
                    inputCount: 2,
                    out output,
                    name: "LocomotionWithAdditiveLeansMixer")
                .Layer(output, input: 0)
                .Layer(output, input: 1, additive: true, mask: data.LeanMask)

                .Input(output, index: 0, name: "LocomotionClips")
                .WithMixer<AnimationMixerPlayable>(
                    inputCount: 1,
                    out AnimationMixerPlayable locomotionClipsMixer,
                    name: "LocomotionClipsMixer")
                .WithWeight(output, "LocomotionClips", 1f)

                    .Input(locomotionClipsMixer, index: 0, name: "Locomotion")
                    .WithClip(data.Locomotion, out _, name: "Locomotion", paused: false)
                    .WithWeight(locomotionClipsMixer, "Locomotion", 1f)

                .Input(output, index: 1, name: "Leans")
                .WithMixer<AnimationMixerPlayable>(
                    inputCount: leanClipCount,
                    out AnimationMixerPlayable leansMixer,
                    name: "LeansMixer")
                .WithWeight(output, "Leans", leanClipCount > 0 ? 1f : 0f)
                    .AddClipsToSlot(leansMixer, data.Leans, "Lean", data.ActiveLeanIndex)

                .CompileAs(output);
        }

        private static int CountValidClips(List<AnimationClip>? clips)
        {
            if (clips == null)
                return 0;

            int count = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                    count++;
            }

            return count;
        }

        private static TopologyScope AddClipsToSlot(
            this TopologyScope topology,
            AnimationMixerPlayable slot,
            List<AnimationClip>? clips,
            string slotName,
            int enabledInput)
        {
            if (clips == null)
                return topology;

            int input = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                    continue;

                string inputName = slotName + input;
                topology.Input(slot, input, inputName)
                    .WithClip(clip, out _, inputName + "Clip", paused: input != enabledInput)
                    .WithWeight(slot, inputName, input == enabledInput ? 1f : 0f);

                input++;
            }

            return topology;
        }
    }
}
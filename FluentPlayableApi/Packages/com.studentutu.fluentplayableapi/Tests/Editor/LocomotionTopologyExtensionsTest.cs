#nullable enable

using System.Collections.Generic;
using FluentPlayableApi;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FluentPlayableApi.Tests
{
    public sealed class LocomotionTopologyExtensionsTest
    {
        [Test]
        public void WithLocomotionTopologyBuildsScopedPlayableTree()
        {
            GameObject animatorObject = new GameObject("LocomotionTopologyTestAnimator");
            Animator animator = animatorObject.AddComponent<Animator>();
            AnimationClip locomotionClip = new AnimationClip();
            AnimationClip leanLeftClip = new AnimationClip();
            AnimationClip leanRightClip = new AnimationClip();
            AvatarMask leanMask = new AvatarMask();
            FluentBuilder? builder = null;
            PlayableGraph graph = default;

            try
            {
                var data = new LocomotionTopologyData
                {
                    Locomotion = locomotionClip,
                    Leans = new List<AnimationClip> { leanLeftClip, leanRightClip },
                    LeanMask = leanMask,
                    ActiveLeanIndex = 1
                };

                builder = FluentBuilder.Create("LocomotionTopologyTestGraph");
                graph = builder
                    .Output(animator, out AnimationPlayableOutput output)
                    .Input(output)
                    .WithMixer<AnimationMixerPlayable>(inputCount: 1, out AnimationMixerPlayable parentMixer,
                        "ParentMixer")
                    .WithWeight(output, 1f)
                    
                    .Input(parentMixer, index: 0, name: "Locomotion")
                    .WithLocomotionTopology(data, scope: "Character/Locomotion",
                        out AnimationLayerMixerPlayable scopedOutput)
                    .WithWeight(parentMixer, "Locomotion", 1f)
                    
                    .Verify();

                AnimationLayerMixerPlayable scopedRoot =
                    builder.Resolve<AnimationLayerMixerPlayable>("Character/Locomotion");
                AnimationLayerMixerPlayable namedScopedRoot =
                    builder.Resolve<AnimationLayerMixerPlayable>(
                        "Character/Locomotion/LocomotionWithAdditiveLeansMixer");
                AnimationMixerPlayable locomotionClipsMixer =
                    builder.Resolve<AnimationMixerPlayable>("Character/Locomotion/LocomotionClipsMixer");
                AnimationMixerPlayable leansMixer =
                    builder.Resolve<AnimationMixerPlayable>("Character/Locomotion/LeansMixer");
                AnimationClipPlayable locomotionPlayable =
                    builder.Resolve<AnimationClipPlayable>("Character/Locomotion/Locomotion");
                AnimationClipPlayable leanLeftPlayable =
                    builder.Resolve<AnimationClipPlayable>("Character/Locomotion/Lean0Clip");
                AnimationClipPlayable leanRightPlayable =
                    builder.Resolve<AnimationClipPlayable>("Character/Locomotion/Lean1Clip");

                Assert.IsTrue(graph.IsValid());
                Assert.AreEqual(scopedOutput.GetHandle(), scopedRoot.GetHandle());
                Assert.AreEqual(scopedOutput.GetHandle(), namedScopedRoot.GetHandle());
                Assert.AreEqual(0, builder.InputIndex(parentMixer, "Locomotion"));
                Assert.AreEqual(2, scopedRoot.GetInputCount());
                Assert.AreEqual(1f, parentMixer.GetInputWeight(0));
                Assert.AreEqual(1f, scopedRoot.GetInputWeight(0));
                Assert.AreEqual(1f, scopedRoot.GetInputWeight(1));
                Assert.AreEqual(1, locomotionClipsMixer.GetInputCount());
                Assert.AreEqual(2, leansMixer.GetInputCount());
                Assert.AreEqual(1f, locomotionClipsMixer.GetInputWeight(0));
                Assert.AreEqual(0f, leansMixer.GetInputWeight(0));
                Assert.AreEqual(1f, leansMixer.GetInputWeight(1));
                Assert.AreEqual(PlayState.Playing, locomotionPlayable.GetPlayState());
                Assert.AreEqual(PlayState.Paused, leanLeftPlayable.GetPlayState());
                Assert.AreEqual(PlayState.Playing, leanRightPlayable.GetPlayState());
            }
            finally
            {
                builder?.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                Object.DestroyImmediate(leanMask);
                Object.DestroyImmediate(leanRightClip);
                Object.DestroyImmediate(leanLeftClip);
                Object.DestroyImmediate(locomotionClip);
                Object.DestroyImmediate(animatorObject);
            }
        }
    }
}

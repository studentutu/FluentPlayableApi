#nullable enable

using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Fluentplayableapi.Tests
{
    public class ExampleTest
    {
        [Test]
        public void BuildCreatesReachableGraphAndRegistersLookup()
        {
            GameObject animatorObject = CreateAnimatorObject(out Animator animator);
            FluentBuilder? builder = null;
            PlayableGraph graph = default;

            try
            {
                builder = FluentBuilder.Create("LookupGraph");
                graph = builder
                    .Output(animator, out AnimationPlayableOutput output)
                    .Input(output)
                    .WithMixer<AnimationMixerPlayable>(1, out AnimationMixerPlayable root, "Root")
                    .WithWeight(output, 0.75f)
                    .Input(root, 0, "Clip")
                    .WithClip(new AnimationClip(), out AnimationClipPlayable clipPlayable, "Clip")
                    .WithWeight(root, "Clip", 1f)
                    .Build(play: false);

                Assert.IsTrue(graph.IsValid());
                Assert.IsFalse(graph.IsPlaying());
                Assert.AreEqual(root.GetHandle(), builder.Resolve<AnimationMixerPlayable>("Root").GetHandle());
                Assert.AreEqual(0, builder.InputIndex("Root", "Clip"));
                Assert.AreEqual(1f, root.GetInputWeight(0));
                Assert.AreEqual(0.75f, output.GetWeight());
                Assert.AreEqual(PlayState.Paused, clipPlayable.GetPlayState());
                Assert.IsFalse(clipPlayable.GetApplyFootIK());
            }
            finally
            {
                builder?.Dispose();
                DestroyGraphAndObject(graph, animatorObject);
            }
        }

        [Test]
        public void BuildPlayFlagControlsGraphPlayState()
        {
            PlayableGraph graph = PlayableGraph.Create("ExistingGraph");
            FluentBuilder? builder = null;

            try
            {
                builder = FluentBuilder.Create(graph);
                PlayableGraph builtGraph = builder.Build(play: true);

                Assert.AreEqual(graph.GetHashCode(), builtGraph.GetHashCode());
                Assert.IsTrue(builtGraph.IsPlaying());
            }
            finally
            {
                builder?.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        [Test]
        public void DisposeClearsBuilderWithoutDestroyingGraph()
        {
            GameObject animatorObject = CreateAnimatorObject(out Animator animator);
            FluentBuilder builder = FluentBuilder.Create("DisposeGraph");
            PlayableGraph graph = default;

            try
            {
                graph = builder
                    .Output(animator, out AnimationPlayableOutput output)
                    .Input(output)
                    .WithMixer<AnimationMixerPlayable>(0, out _, "Root")
                    .Build(play: false);

                builder.Dispose();

                Assert.IsTrue(graph.IsValid());
                Assert.Throws<ObjectDisposedException>(() => builder.Resolve<AnimationMixerPlayable>("Root"));
            }
            finally
            {
                builder.Dispose();
                DestroyGraphAndObject(graph, animatorObject);
            }
        }

        [Test]
        public void InputValidationRejectsBoundsAndUnconsumedPendingInputs()
        {
            FluentBuilder builder = FluentBuilder.Create("InputValidationGraph");

            try
            {
                builder.WithMixer<AnimationMixerPlayable>(1, out AnimationMixerPlayable root);

                Assert.Throws<ArgumentOutOfRangeException>(() => builder.Input(root, 1));

                builder.Input(root, 0, "Valid");

                Assert.Throws<InvalidOperationException>(() => builder.Input(root, 0, "Second"));
                Assert.Throws<InvalidOperationException>(() => builder.Build(play: false));
            }
            finally
            {
                PlayableGraph graph = builder.Graph;
                builder.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        [Test]
        public void DuplicateNodePathThrowsImmediately()
        {
            FluentBuilder builder = FluentBuilder.Create("DuplicatePathGraph");

            try
            {
                builder.WithMixer<AnimationMixerPlayable>(0, out _, "Mixer");

                Assert.Throws<InvalidOperationException>(() =>
                    builder.WithMixer<AnimationMixerPlayable>(0, out _, "Mixer"));
            }
            finally
            {
                PlayableGraph graph = builder.Graph;
                builder.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        [Test]
        public void DuplicateSourceOutputThrows()
        {
            GameObject animatorObject = CreateAnimatorObject(out Animator animator);
            FluentBuilder builder = FluentBuilder.Create("SourceOutputGraph");

            try
            {
                builder
                    .Output(animator, out AnimationPlayableOutput output)
                    .Input(output)
                    .WithMixer<AnimationMixerPlayable>(2, out AnimationMixerPlayable root, "Root")
                    .Input(root, 0, "First")
                    .WithClip(new AnimationClip(), out AnimationClipPlayable clipPlayable, "Clip");

                Assert.Throws<InvalidOperationException>(() =>
                    builder.Input(root, 1, "Second").WithPlayable(clipPlayable));
            }
            finally
            {
                PlayableGraph graph = builder.Graph;
                builder.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                UnityEngine.Object.DestroyImmediate(animatorObject);
            }
        }

        [Test]
        public void ScopeLookupRequiresExactPathWhenShortNameIsAmbiguous()
        {
            GameObject animatorObject = CreateAnimatorObject(out Animator animator);
            FluentBuilder? builder = null;
            PlayableGraph graph = default;

            try
            {
                builder = FluentBuilder.Create("ScopeLookupGraph");
                graph = builder
                    .Output(animator, out AnimationPlayableOutput output)
                    .Input(output)
                    .WithMixer<AnimationMixerPlayable>(2, out AnimationMixerPlayable root, "Root")
                    .Input(root, 0, "A")
                    .Scope("A")
                        .WithMixer<AnimationMixerPlayable>(0, out AnimationMixerPlayable mixerA, "Mixer")
                        .CompileAs(mixerA)
                    .Input(root, 1, "B")
                    .Scope("B")
                        .WithMixer<AnimationMixerPlayable>(0, out AnimationMixerPlayable mixerB, "Mixer")
                        .CompileAs(mixerB)
                    .Build(play: false);

                Assert.AreEqual(mixerA.GetHandle(), builder.Resolve<AnimationMixerPlayable>("A/Mixer").GetHandle());
                Assert.AreEqual(mixerB.GetHandle(), builder.Resolve<AnimationMixerPlayable>("B/Mixer").GetHandle());
                Assert.Throws<InvalidOperationException>(() => builder.Resolve<AnimationMixerPlayable>("Mixer"));
                Assert.Throws<InvalidOperationException>(() => builder.Resolve<AnimationLayerMixerPlayable>("A/Mixer"));
            }
            finally
            {
                builder?.Dispose();
                DestroyGraphAndObject(graph, animatorObject);
            }
        }

        [Test]
        public void AddInputExpandsMixerAndNamesNewIndex()
        {
            FluentBuilder builder = FluentBuilder.Create("AddInputGraph");

            try
            {
                builder.WithMixer<AnimationMixerPlayable>(0, out AnimationMixerPlayable mixer);
                builder.AddInput(mixer, "Generated", out int generatedIndex)
                    .WithClip(new AnimationClip(), out _);
                builder.Build(play: false);

                Assert.AreEqual(0, generatedIndex);
                Assert.AreEqual(1, mixer.GetInputCount());
                Assert.AreEqual(0, builder.InputIndex(mixer, "Generated"));
                Assert.Throws<InvalidOperationException>(() => builder.Input(mixer, generatedIndex, "Other"));
            }
            finally
            {
                PlayableGraph graph = builder.Graph;
                builder.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        [Test]
        public void ClipPausedParameterControlsInitialPlayState()
        {
            FluentBuilder builder = FluentBuilder.Create("ClipStateGraph");

            try
            {
                builder.WithClip(new AnimationClip(), out AnimationClipPlayable pausedClip);
                builder.WithClip(new AnimationClip(), out AnimationClipPlayable playingClip, paused: false);

                Assert.AreEqual(PlayState.Paused, pausedClip.GetPlayState());
                Assert.AreEqual(PlayState.Playing, playingClip.GetPlayState());
            }
            finally
            {
                PlayableGraph graph = builder.Graph;
                builder.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        private static GameObject CreateAnimatorObject(out Animator animator)
        {
            var animatorObject = new GameObject("FluentBuilderTestsAnimator");
            animator = animatorObject.AddComponent<Animator>();
            return animatorObject;
        }

        private static void DestroyGraphAndObject(PlayableGraph graph, GameObject animatorObject)
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }

            UnityEngine.Object.DestroyImmediate(animatorObject);
        }
    }
}

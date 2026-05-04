using System;
using System.Collections.Generic;
using Studentutu.Fluentplayableapi;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
///     Example to use for the <see cref="FluentBuilder"/>
/// </summary>
public class AnimationGraphExample : MonoBehaviour
{
    [SerializeField] private AnimationClip Locomotion;
    [SerializeField] private List<AnimationClip> FullBody;
    [SerializeField] private List<AnimationClip> UpperBody;
    [SerializeField] private AvatarMask UpperBodyMask;
    [SerializeField] private Animator _animator;

    public float ManualTime;
    public bool Manual = false;
    public bool RecreateGraph = false;

    private PlayableGraph _graph;

    private void OnValidate()
    {
        if (!RecreateGraph)
            return;

        RecreateGraph = false;
        Recreate();
    }

    private void OnEnable()
    {
        Recreate();
    }

    // Update is called once per frame
    private void Recreate()
    {
        OnDestroy();

        if (_animator == null)
            return;
        
        if (Locomotion == null)
            return;

        var builder = FluentBuilder.Create("AnimationGraphExample")
            .Output(_animator, out var output)
            .Input(output)
            .WithMixer<AnimationMixerPlayable>(2, out var rootWithFullBodyMixer, "RootWithFullBodyMixer")
            .WithWeight(output, 1f)

            .Input(rootWithFullBodyMixer, 0, "LocomotionRoot")
            .WithMixer<AnimationLayerMixerPlayable>(2, out var rootWithLocomotionMixer, "RootWithLocomotionMixer")
            .WithWeight(rootWithFullBodyMixer, "LocomotionRoot", 1f)

            .Input(rootWithLocomotionMixer, 0, "Locomotion")
            .WithClip(Locomotion, out _, "Locomotion", paused: false)
            .WithWeight(rootWithLocomotionMixer, "Locomotion", 1f)

            .Input(rootWithLocomotionMixer, 1, "UpperBodySlot")
            .WithMixer<AnimationMixerPlayable>(CountValidClips(UpperBody), out var upperBodySlot, "UpperBodySlot")
            .WithWeight(rootWithLocomotionMixer, "UpperBodySlot", 1f)
            .Layer(rootWithLocomotionMixer, 0)
            .Layer(rootWithLocomotionMixer, 1, additive: true, mask: UpperBodyMask)

            .Input(rootWithFullBodyMixer, 1, "FullBodySlot")
            .WithMixer<AnimationMixerPlayable>(CountValidClips(FullBody), out var fullBodySlot, "FullBodySlot")
            .WithWeight(rootWithFullBodyMixer, "FullBodySlot", 0f);

        AddClipsToSlot(builder, upperBodySlot, UpperBody, "UpperBody", PickRandomValidClipIndex(UpperBody));
        AddClipsToSlot(builder, fullBodySlot, FullBody, "FullBody", enabledInput: -1);

        _graph = builder.Build(play: !Manual);
    }

    private void OnDestroy()
    {
        if(!_graph.IsValid())
            return;
        
        _graph.Destroy();
    }

    private void Update()
    {
        if (!Manual)
            return;

        RefreshGraph(Time.deltaTime);
    }

    private void RefreshGraph(float delta)
    {
        if (!_graph.IsValid())
            return;
        
        _graph.Evaluate(delta);
    }

    private static int CountValidClips(List<AnimationClip> clips)
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

    private static int PickRandomValidClipIndex(List<AnimationClip> clips)
    {
        int validClipCount = CountValidClips(clips);
        if (validClipCount == 0)
            return -1;

        return UnityEngine.Random.Range(0, validClipCount);
    }

    private static void AddClipsToSlot(
        FluentBuilder builder,
        AnimationMixerPlayable slot,
        List<AnimationClip> clips,
        string slotName,
        int enabledInput)
    {
        if (clips == null)
            return;

        int input = 0;
        for (int i = 0; i < clips.Count; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
                continue;

            string inputName = slotName + input;
            builder.Input(slot, input, inputName)
                .WithClip(clip, out _, inputName + "Clip", paused: input != enabledInput)
                .WithWeight(slot, inputName, input == enabledInput ? 1f : 0f);

            input++;
        }
    }
}

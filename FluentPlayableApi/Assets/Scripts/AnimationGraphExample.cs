using System;
using System.Collections.Generic;
using Studentutu.Fluentplayableapi;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
///     Example to use for the <see cref="FluentBuilder"/>
/// </summary>
public class AnimationGraphExample : MonoBehaviour
{
    [SerializeField] private AnimationClip Locomotion;
    [SerializeField] private List<AnimationClip> FullBody;
    [SerializeField] private List<AnimationClip> UpperBody;
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

    // Update is called once per frame
    private void Recreate()
    {
        OnDestroy();
        
        // TODO: Sample of Fluent API to create graph:
        // 1. Locomotion (Clip) -> RootWithLocomotionMixer [0] -> RootWithFullBodyMixer [0] -> Output [0]
        // 2. UpperBodySlot (Mixer of clips) -> RootWithLocomotionMixer [1]
        // 3. FullBodySlot (Mixer of clips) -> RootWithFullBodyMixer[1]
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
}
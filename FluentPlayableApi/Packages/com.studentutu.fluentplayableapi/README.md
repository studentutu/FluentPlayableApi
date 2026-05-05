# Fluent Playable API

## Quick Navigation

- [Summary](#summary)
- [Install](#install)
- [How to use](#core-use), [Example](#multi-output-example)
- [Fluent API](#fluent-api), [Project Extensions](#project-extensions)
- [Validation](#validation)
- [License](#license)

## Summary

Fluent Playable API is a compact declaration layer over Unity's `PlayableGraph`.
It helps create, connect, name, resolve, and validate Unity playables while still
returning a normal Unity `PlayableGraph`.

The builder does not own the graph; it only provides helper API to build it.

It addresses a graph authoring problem: Unity provides a flat view, while Fluent
Playable API keeps topology visible and maintainable in code.

The API is intentionally small. It does not replace Unity's graph types, own
graph lifetime, or add project-specific animation concepts.

## Install

The distributable Unity package lives in this repository at
`FluentPlayableApi/Packages/com.studentutu.fluentplayableapi`. If you only need
the package in another project, install that package path directly instead of
copying repository files by hand.

### Install as Git dependency via Package Manager

1. Open Package Manager in Unity (`Window -> Package Manager`).
2. Click `+` in the top-left corner.
3. Select `Add package from git URL...`.
4. Enter the following URL and click `Add`:

```text
https://github.com/studentutu/FluentPlayableApi.git?path=/FluentPlayableApi/Packages/com.studentutu.fluentplayableapi
```

> NOTE: If you want to pin the install, append `#branch`, `#tag`, or a commit SHA. Do not assume repo tags map cleanly to `package.json` versions.

### Install by editing `Packages/manifest.json`

1. Close Unity if it is holding the manifest open.
2. Open `Packages/manifest.json`.
3. Add the package entry under `"dependencies"`:

```json
"com.studentutu.fluentplayableapi": "https://github.com/studentutu/FluentPlayableApi.git?path=/FluentPlayableApi/Packages/com.studentutu.fluentplayableapi"
```

4. Reopen the project in Unity and let Package Manager resolve the dependency.

### Install from local disk

If this repository is already checked out next to your Unity project, you can point `manifest.json` to the package folder directly:

```json
"com.studentutu.fluentplayableapi": "file:../path-to-cloned-repo/FluentPlayableApi/Packages/com.studentutu.fluentplayableapi"
```

Replace `../path-to-cloned-repo` with the actual relative path from your Unity project's `Packages/manifest.json` file to this repository clone.

## Core Use

```csharp
using Fluentplayableapi;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// 1. Author the topology.
using FluentBuilder builder = FluentBuilder.Create("CharacterGraph");
PlayableGraph graph = builder
  .Output(animator, out AnimationPlayableOutput output)

  .Input(output)
  .WithMixer<AnimationMixerPlayable>(
    inputCount: 1,
    out AnimationMixerPlayable rootMixer,
    name: "RootMixer")
  .WithWeight(output, 1f)

  .Input(rootMixer, index: 0, name: "BaseClip")
  .WithClip(baseClip, out AnimationClipPlayable baseClipPlayable, name: "BaseClip")
  .WithWeight(rootMixer, "BaseClip", 1f)

  .Build();

// 2. Link custom animation classes to this builder's fluent metadata.
AnimationMixerPlayable resolvedRoot = builder.Resolve<AnimationMixerPlayable>("RootMixer");
int baseClipInput = builder.InputIndex("RootMixer", "BaseClip");
```

### Entry Points

```csharp
FluentBuilder.Create("Name of graph")
FluentBuilder.Create(existingGraph)
```

`Create(existingGraph)` reuses the graph instance only. It does not clear graph
outputs, assume an existing output, or own graph destruction.

`Build(play: false)` returns the graph without starting playback.

`Dispose()` clears only metadata and caches owned by the `FluentBuilder`
instance. It does not destroy, stop, or otherwise own the `PlayableGraph`.

## Fluent API

```csharp
.Output(animator, out output, name = "AnimationOutput")

.Scope(path)

.Input(output, index = 0)
.Input(playable, index, name = null)
.AddInput(playable, name, out index)

.WithMixer<TMixer>(inputCount, out mixer, name = null, outputCount = 1)
.WithClip(clip, out clipPlayable, name = null, paused = true)
.WithScript<TBehaviour>(out script, inputCount = 0, outputCount = 1, name = null)
.WithPlayable(factory, out playable, name = null)
.WithPlayable<TPlayable>(key, out playable, sourceOutputPort = 0)
.WithPlayable(playable, sourceOutputPort = 0)

.WithWeight(destination, input, weight)
.WithWeight(destination, inputName, weight)
.WithWeight(output, weight)

.Layer(layerMixer, input, additive = false, mask = null)

.CompileAs(playable)

.Build(play = true) // validation here

.Resolve<TPlayable>(key)
.InputIndex(destination, inputName)
.InputIndex(nodeKey, inputName)

.Dispose() // clears builder metadata only
```

### Mixers

`WithMixer<TMixer>(...)` supports:

- `AnimationMixerPlayable`
- `AnimationLayerMixerPlayable`

Mixer input count is explicit. Mixer output count defaults to `1` and can be
set when a source needs multiple output ports:

```csharp
.WithMixer<AnimationMixerPlayable>(
  inputCount: 1,
  out var cloneBase,
  name: "CloneBase",
  outputCount: 2)
```

### Clips

`WithClip(...)` creates an `AnimationClipPlayable`, disables foot IK, and pauses
the playable by default:

```csharp
clipPlayable.SetApplyFootIK(false);
clipPlayable.Pause();
```

Pass `paused: false` to start an individual clip playable in the playing state:

```csharp
.WithClip(runClip, out var runPlayable, name: "Run", paused: false)
```

Project-specific clip configuration, such as speed or duration behavior, should
be applied by project code after creation.

### Arbitrary Playables

Use the factory overload for Unity playables that are outside the core mixer and
clip helpers:

```csharp
.WithPlayable(
  graph => AnimationScriptPlayable.Create(graph, job, inputCount: 2),
  out var applyAdditive,
  name: "ApplyAdditive")
```

Existing handles can be attached to a pending input:

```csharp
.Input(parentMixer, index: 0, name: "Base")
.WithPlayable(existingPlayable, sourceOutputPort: 0)
```

Key-based lookup can either attach to a pending input or resolve without adding
an edge:

```csharp
.WithPlayable<AnimationMixerPlayable>("Character/Locomotion", out var locomotion)
```

### Scopes And Lookup

`Scope(path)` creates a virtual declaration context. Nodes declared inside a
scope are registered below that path:

```csharp
builder.Scope("Character/UpperBody")
  .WithMixer<AnimationLayerMixerPlayable>(4, out var mixer, "UpperBodyMixer")
  .CompileAs(mixer);
```

This registers:

```text
Character/UpperBody/UpperBodyMixer
Character/UpperBody
```

Lookup accepts exact paths and unique short names:

```csharp
builder.Resolve<AnimationMixerPlayable>("Character/Locomotion");
builder.Resolve<AnimationMixerPlayable>("Locomotion");
```

Path rules:

- `/Character/Locomotion/` normalizes to `Character/Locomotion`.
- Backslashes normalize to slashes.
- Duplicate exact paths throw.
- Duplicate scope paths throw.
- Short-name lookup throws when more than one node has the same short name.
- Typed lookup throws when the registered playable type does not match.
- `.` and `..` path segments are rejected.

### Inputs And Connections

`Input(...)` declares one pending destination input. The next created, resolved,
or existing playable consumes it:

```csharp
.Input(mixer, index: 0, name: "Base")
.WithPlayable(basePlayable)
```

Input rules:

- Input index must be inside the destination playable input count.
- Input name is optional.
- Input names are unique per destination playable.
- One pending input must be consumed before another input is declared.
- A declared input must be consumed exactly once.
- Destination inputs cannot be reassigned.

`AddInput(...)` expands the destination input count by one, names the new index,
and declares it as the pending input:

```csharp
.AddInput(mixer, "Motion:3", out int input)
.WithClip(motionClip, out var clipPlayable, name: "MotionClip:3")
```

Source output ports are explicit. The default is `0`:

```csharp
.Input(applyAdditive, index: 0, name: "BaseClone")
.WithPlayable(cloneBase, sourceOutputPort: 1)
```

Source output rules:

- Source output port must be non-negative.
- Source output port must be less than the source playable output count.
- The same source output port cannot be connected to more than one destination.
- Fan-out requires explicit additional output ports.

### Weights And Layers

Assign weights explicitly:

```csharp
.WithWeight(mixer, input: 0, weight: 1f)
.WithWeight(mixer, inputName: "Base", weight: 1f)
.WithWeight(output, weight: 1f)
```

Configure layer mixer inputs through `Layer(...)`:

```csharp
.Layer(layerMixer, input: 0)
.Layer(layerMixer, input: 1, additive: true)
.Layer(layerMixer, input: 2, additive: true, mask: upperBodyMask)
```

`Layer(...)` accepts only `AnimationLayerMixerPlayable`. The mask is applied only
when it is not `null`.

## Project Extensions

Project-specific extension methods should stay thin and build on `FluentBuilder`
or `TopologyScope`, not on `PlayableGraph`.
Invoke extensions where their result is consumed:

```csharp
builder
  .Input(parentMixer, index: 0, name: "Locomotion")
  .WithLocomotionTopology(data, scope: "Character/Locomotion", out var output)
  .WithWeight(parentMixer, "Locomotion", 1f);
```

Extensions can inspect descriptor data, create jobs, configure returned handles,
and build sidecar runtime data. They should not disconnect, switch, or rewrite
an already declared fluent edge.

## Validation

`Build(...)` validates fluent declarations before returning the graph. Fluent
metadata lookup remains on the actual `FluentBuilder` instance until it is
disposed.

Validation fails when:

- A pending input was not consumed.
- A declared input was never consumed.
- A destination input is reassigned.
- A registered node is unreachable from every authored output.
- A scope path or node path is duplicated.
- A short-name lookup is ambiguous.
- A typed lookup resolves the wrong playable type.
- An input name conflicts with another input on the same destination.
- A source output port is invalid or connected more than once.

Validation applies to fluent declarations. Reserved Unity input capacity that was
not declared through Fluent API is allowed.

## Multi-Output Example

```csharp
using FluentBuilder builder = FluentBuilder.Create(existingGraph);
builder
  .Output(animator, out var output)

  .Input(output)
  .WithMixer<AnimationMixerPlayable>(
    inputCount: 2,
    out var finalMixer,
    name: "FinalMixer")
  .WithWeight(output, 1f)

  .Input(finalMixer, index: 0, name: "Base")
  .WithMixer<AnimationMixerPlayable>(
    inputCount: 1,
    out var cloneBase,
    name: "CloneBase",
    outputCount: 2)
  .WithWeight(finalMixer, "Base", 1f)

  .Input(finalMixer, index: 1, name: "Additive")
  .WithPlayable(
    graph => AnimationScriptPlayable.Create(graph, job, inputCount: 2),
    out var applyAdditive,
    name: "ApplyAdditive")
  .WithWeight(finalMixer, "Additive", 0f)

  .Input(cloneBase, index: 0, name: "Base")
  .WithPlayable(basePlayable, sourceOutputPort: 0)
  .WithWeight(cloneBase, "Base", 1f)

  .Input(applyAdditive, index: 0, name: "BaseClone")
  .WithPlayable(cloneBase, sourceOutputPort: 1)
  .WithWeight(applyAdditive, "BaseClone", 1f)

  .Input(applyAdditive, index: 1, name: "AdditivePose")
  .WithPlayable(additivePlayable, sourceOutputPort: 0)
  .WithWeight(applyAdditive, "AdditivePose", 1f)

  .Build(play: false); // validation here
```

## License

MIT license. See [LICENSE](LICENSE).

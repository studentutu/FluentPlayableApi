# Proposed Fluent Playable API

## Quick Navigation

- [Summary](#summary)
- [How to](#public-surface)
- [Usage](#fluent-api)
- How it works: [Building blocks](#creation-rules), [Rules](#scope-and-lookup-rules), [Connection rules](#input-and-connection-rules), [Mixer rules](#weight-and-layer-rules), [Extensions](#extension-authoring-rules), [Graph Validation](#build-validation)
- [Examples](#resulting-api-example)
- [License](#license)

## Summary

This document defines the actual core of a compact fluent layer over Unity's
`PlayableGraph`.

The API is intentionally small. It is a declaration helper for creating,
connecting, naming, and validating Unity playables. It does not replace Unity's
graph, and it does not introduce project-specific animation concepts.

## Core Goals

The Fluent API must:

- read top-to-bottom in the same order as the graph is declared;
- keep every authored node available as the real Unity playable type through
  `out` parameters;
- return a normal Unity `PlayableGraph`, not a wrapper graph;
- allow project-specific extension methods to declare reusable subgraphs exactly
  where a parent graph consumes them;
- support typed lookup from the returned `PlayableGraph`;
- support strict named input lookup so link code can recover parent-owned input
  indices;
- support `AnimationMixerPlayable` and `AnimationLayerMixerPlayable` through one
  mixer API;
- support layer configuration through one `.Layer(...)` API;
- support arbitrary Unity playable factories through `.WithPlayable(...)`;
- require explicit source output ports for non-trivial fan-out;
- fail fast when the authored graph has dangling nodes, duplicate paths,
  unconsumed inputs, invalid source ports, or unsafe ambiguous lookup.

### Core Non-Goals

The Fluent API must not include:

- automatic source output allocation;
- project specific concepts such as slots, locomotion, hit impacts, leans, root layers,
  transition curves, skeletons, LOD policy, Runtime Budgeting or event dispatch.

Project code can add extension methods over the minimal surface, but those
extensions must still create and connect real Unity playables through this core.

## Public Surface

### Entry Points

```csharp
FluentBuilder.Create("Name of graph")
FluentBuilder.Create(existingGraph)
```

`Create(existingGraph)` reuses the graph instance only. It does not assume an
output already exists, does not clear graph outputs, and does not own the graph
lifetime.

### Fluent API

```csharp
.Output(animator, out output, name = "AnimationOutput")

.Scope(path)

.Input(output, index = 0)
.Input(playable, index, name = null)
.AddInput(playable, name, out index)

.WithMixer<TMixer>(inputCount, out mixer, name = null, outputCount = 1)
.WithClip(clip, out clipPlayable, name = null)
.WithScript<TBehaviour>(out script, inputCount = 0, outputCount = 1, name = null)
.WithPlayable(factory, out playable, name = null)
.WithPlayable<TPlayable>(key, out playable, sourceOutputPort = 0)
.WithPlayable(playable, sourceOutputPort = 0)

.WithWeight(destination, input, weight)
.WithWeight(destination, inputName, weight)
.WithWeight(output, weight)

.Layer(layerMixer, input, additive = false, mask = null)

.CompileAs(playable)

.Build(play = true)
```

### Project Usage

Use Fluent API to build readable and declarative top-down topology.
Use custom extensions to create reusable logic blocks (slots/blendtree).

The factory overload of `WithPlayable(...)` is the escape hatch for Unity
playables that are not part of the compact core:

```csharp
fluentGraph.Resolve<TPlayable>(key)
fluentGraph.InputIndex(playable, inputName)
fluentGraph.InputIndex(nodeKey, inputName)
fluentGraph.WithPlayable(
  graph => AnimationScriptPlayable.Create(graph, job, inputCount: 2),
  out var applyAdditive,
  name: "ApplyAdditive")
```

There is no public `DestroyFluent()` extension. The caller owns graph lifetime in
the same way it owns a normal Unity `PlayableGraph`.

### Notes For Project Extensions

Project extensions should be thin authoring helpers. They may create rich domain
topologies, but they should not expand the core Fluent API surface.

When a project extension needs custom runtime objects, build them during the
graph authoring stage from authoring data, resolve live playable handles, and
named input indices. The Fluent core owns graph declaration and validation only.

## Creation Rules

### Graph Ownership

`Build()` returns the Unity graph:

```csharp
PlayableGraph graph = FluentBuilder.Create("CharacterGraph")
  // declarations
  .Build();
```

When the project already owns the graph, use the same entry point:

```csharp
PlayableGraph graph = FluentBuilder.Create(existingGraph)
  // declarations
  .Build(play: false);
```

`play: false` prevents the builder from starting a graph whose play state is
managed elsewhere.

### Mixers

`WithMixer<TMixer>(...)` supports these built-in mixer types:

```csharp
AnimationMixerPlayable
AnimationLayerMixerPlayable
```

Mixer input count is explicit. Mixer output count defaults to 1 and must be
specified when the node needs more than one source output:

```csharp
.WithMixer<AnimationMixerPlayable>(
  inputCount: 1,
  out var cloneBase,
  name: "CloneBase",
  outputCount: 2)
```

### Clips

`WithClip(...)` creates an `AnimationClipPlayable` and immediately applies the
safe animation defaults:

```csharp
clipPlayable.SetApplyFootIK(false);
clipPlayable.Pause();
```

Any project-specific clip behavior, such as duration or speed, is applied by the
project extension after creation.

### Arbitrary Playables And Jobs

The factory overload of `WithPlayable(...)` is the escape hatch for Unity
playables that are not part of the compact core:

```csharp
.WithPlayable(
  graph => AnimationScriptPlayable.Create(graph, job, inputCount: 2),
  out var applyAdditive,
  name: "ApplyAdditive")
```

Job-specific configuration remains outside the core API:

```csharp
applyAdditive.SetProcessInputs(false);
applyAdditive.SetInputWeight(0, 1f);
applyAdditive.SetInputWeight(1, 1f);
```

This keeps the generic API minimal while still supporting custom animation jobs.

The existing-playable and key-based overloads use the same `WithPlayable(...)`
prefix because they also place a playable into the authored declaration:

```csharp
.WithPlayable(existingPlayable, sourceOutputPort: 0)
.WithPlayable<AnimationMixerPlayable>("Character/Locomotion", out var locomotion)
```

The overloads differ only in how the playable handle is obtained: factory,
existing handle, or registry lookup.

## Scope And Lookup Rules

### Paths

Lookup accepts two forms:

```csharp
"Character/UpperBody/UpperBodyMixer" // exact path
"UpperBodyMixer"                    // unique short name
```

Rules:

- a key containing `/` is treated as an exact path;
- a key without `/` is treated as a unique short name;
- resolving by full path is always exact;
- resolving by short name throws if more than one registered node has that name;
- resolving with the wrong playable type throws;
- no sibling search, parent traversal, wildcard matching, or fuzzy matching is
  supported.

Path normalization:

```csharp
"/Character/Locomotion/"  -> "Character/Locomotion"
"Character//Locomotion"   -> "Character/Locomotion"
"Character\\Locomotion"   -> "Character/Locomotion"
```

Invalid path segments:

```csharp
"../Locomotion"
"./Locomotion"
"Character/../Locomotion"
"Character/./Locomotion"
```

### Scopes

`Scope(path)` does not create a playable. It creates a virtual declaration
context. Named nodes inside the scope are registered as:

```text
scopePath/nodeName
```

Example:

```csharp
builder.Scope("Character/UpperBody")
  .WithMixer<AnimationLayerMixerPlayable>(4, out var mixer, "UpperBodyMixer")
  .CompileAs(mixer);
```

Registers:

```text
Character/UpperBody/UpperBodyMixer
Character/UpperBody
```

A scope path can be declared only once. Reopening the same scope later is
ambiguous and throws. Procedural authoring should keep the returned scope builder
and add generated nodes through that instance.

### CompileAs

Inside a scope:

```csharp
.CompileAs(rootPlayable)
```

registers the current scope path as a reusable playable root and returns the
parent builder. The scope root can be any playable created in that scope. It does
not have to be the first playable in the scope.

## Input And Connection Rules

### Pending Inputs

`Input(...)` declares one pending destination input. The next created or resolved
playable consumes it.

```csharp
.Input(mixer, index: 0, name: "Base")
.WithPlayable(basePlayable)
```

Rules:

- input index must be inside the destination playable's input count;
- input name is optional;
- input name is unique per destination playable;
- one input index cannot have two different names;
- one input name cannot point to two different indices on the same destination;
- a pending input must be consumed exactly once;
- starting another pending input before consuming the previous one throws;
- an already declared destination input cannot be reassigned.

### Dynamic Inputs

`AddInput(destination, name, out index)` expands the destination input count by
one, names the new index, and creates a pending input:

```csharp
.AddInput(mixer, "Motion:3", out var input)
.WithClip(motion.Clip, out var clip, name: "MotionClip:3")
.WithWeight(mixer, input, 0f)
```

Reserved input capacity is allowed. Validation applies only to inputs declared
through `.Input(...)` or `.AddInput(...)`.

### Existing Or Resolved Playables

`WithPlayable(...)` is the authoring verb for selecting the playable that should
occupy the pending input.

Existing handles are attach-only:

```csharp
.Input(mixer, index: 0, name: "Base")
.WithPlayable(basePlayable)
```

`WithPlayable(existingPlayable)` requires a pending input. Calling it without a
pending input throws, because it cannot register or resolve anything by itself.

Key-based handles can resolve and optionally attach:

```csharp
.WithPlayable<AnimationMixerPlayable>("Character/Locomotion", out var locomotion)
```

If a pending input exists, the resolved playable is connected and the pending
input is consumed. If no pending input exists, the call is lookup-only and
creates no edge.

Lookup-only is not a way to leave authored nodes floating. `Build(...)` validates
that registered nodes are reachable from an authored output.

### Source Output Ports

Every connection uses an explicit source output port. The default is 0.

The builder never auto-allocates source output ports. If a source playable feeds
multiple destinations, the author must declare enough output ports and name the
intended source output on each edge:

```csharp
.WithMixer<AnimationMixerPlayable>(1, out var cloneBase, "CloneBase", outputCount: 2)

.Input(byPassMixer, index: 0, name: "Base")
.WithPlayable(cloneBase, sourceOutputPort: 0)

.Input(applyAdditive, index: 0, name: "Base")
.WithPlayable(cloneBase, sourceOutputPort: 1)
```

Rules:

- source output port must be non-negative;
- source output port must be less than the source playable output count;
- the same source output port cannot be connected to more than one destination
  in the authored declaration;
- if fan-out is needed later, it must be introduced as an explicit new feature
  with its own validation rules.

## Weight And Layer Rules

Weights are assigned explicitly to a destination input:

```csharp
.WithWeight(mixer, input: 0, weight: 1f)
.WithWeight(mixer, inputName: "Base", weight: 1f)
.WithWeight(output, weight: 1f)
```

There is no `.WithWeight(float)` overload that modifies the previous edge.

`Input(output, index)` declares which source output port will feed the final
`PlayableOutput`. It does not assign output weight. Output roots must be weighted
explicitly with `WithWeight(output, weight)`, which calls `output.SetWeight(...)`.

Layer configuration is explicit:

```csharp
.Layer(layerMixer, input: 0)
.Layer(layerMixer, input: 1, additive: true)
.Layer(layerMixer, input: 2, additive: true, mask: upperBodyMask)
```

Rules:

- `.Layer(...)` accepts only `AnimationLayerMixerPlayable`;
- input index must be inside the layer mixer input count;
- `additive` defaults to false;
- `mask` defaults to null;
- `SetLayerMaskFromAvatarMask(...)` is called only when `mask != null`.

## Extension Authoring Rules

Project-specific extension methods are encouraged, but they must stay on top of
the core API.

An extension should be invoked exactly where its output is consumed:

```csharp
builder
  .Input(parentMixer, index: 0, name: "Locomotion")
  .WithLocomotionTopology(data, scope: "Character/Locomotion", out var output)
  .WithWeight(parentMixer, "Locomotion", 1f);
```

Rules:

- extensions may inspect descriptor data;
- extensions may choose stable scope names;
- extensions may create jobs through `.WithPlayable(...)`;
- extensions may configure returned handles directly;
- extensions may build sidecar data for later linking;
- extensions must choose the final edge before declaring it;
- extensions must not disconnect, switch, or rewrite an already declared edge.

When descriptor data chooses between two output shapes, choose before consuming
the pending input. For example, if a subgraph may produce either a direct mixer
or an additive wrapper, the extension must compile the scope to the selected
final playable and connect only that final playable to the parent.

## Build Validation

`Build(...)` validates the fluent declaration before returning the graph.

Validation fails when:

- a pending input exists;
- a declared input was never consumed;
- a declared input is consumed more than once;
- a destination input is reassigned;
- a registered node is unreachable from every authored `PlayableOutput`;
- a scope path is registered more than once;
- a node path is registered more than once;
- a short-name lookup is ambiguous;
- a typed lookup resolves the wrong playable type;
- an input name is duplicated for a different input on the same destination;
- an input index is named differently more than once;
- a source output port is negative;
- a source output port is greater than or equal to the source output count;
- the same source output port is connected to more than one destination.

Validation applies to Fluent declarations. It does not require every reserved
input in every Unity playable to be connected, because some projects reserve
inputs for runtime playback systems.

## Resulting API Example

```csharp
var graph = FluentBuilder.Create("CharacterGraph")
  .Output(animator, out var output)

  .Input(output, index: 0)
  .WithMixer<AnimationMixerPlayable>(
    inputCount: 2,
    out var rootMixer,
    name: "RootMixer")
  .WithWeight(output, 1f)

  .Input(rootMixer, index: 0, name: "Base")
  .WithMixer<AnimationMixerPlayable>(
    inputCount: 1,
    out var baseMixer,
    name: "BaseMixer")
  .WithWeight(rootMixer, "Base", 1f)

  .Input(baseMixer, index: 0, name: "BaseClip")
  .WithClip(baseClip, out var baseClipPlayable, name: "BaseClip")
  .WithWeight(baseMixer, "BaseClip", 1f)

  .Input(rootMixer, index: 1, name: "Overlay")
  .Scope("Overlay")
    .WithMixer<AnimationMixerPlayable>(
      inputCount: clips.Count,
      out var overlayMixer,
      name: "Mixer")
    .CompileAs(overlayMixer)
  .WithWeight(rootMixer, "Overlay", 0f)

  .Build();

var resolved = graph.Resolve<AnimationMixerPlayable>("Overlay/Mixer");
var overlayIndex = graph.InputIndex("RootMixer", "Overlay");
```

### Multi-Output Example

```csharp
var job = new ApplyAdditiveAnimationJob(boneInfos, useRootBoneSpaceRotation);

FluentBuilder.Create(existingGraph)
  .Output(animator, out var output, name: "AnimationOutput")

  .Input(output, index: 0)
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

  .Build(play: false);
```

## Implementation Shape [Agent-Development stage]

The implementation should be plain C# classes and extension methods. The exact
file layout can change, but the core responsibilities should remain separate.

```csharp
public static class TopologyPath
{
  public static string Normalize(string path);
  public static string Join(string parent, string child);
  public static string NameOf(string path);
  public static bool IsPath(string key);
}

public sealed class FluentGraphRegistry
{
  public void RegisterNode<TPlayable>(string path, TPlayable playable)
    where TPlayable : struct, IPlayable;

  public void RegisterScope<TPlayable>(string path, TPlayable root)
    where TPlayable : struct, IPlayable;

  public void RegisterInputName<TPlayable>(TPlayable destination, int index, string name)
    where TPlayable : struct, IPlayable;

  public TPlayable Resolve<TPlayable>(string key)
    where TPlayable : struct, IPlayable;

  public Playable ResolveUntyped(string key);

  public int InputIndex<TPlayable>(TPlayable destination, string inputName)
    where TPlayable : struct, IPlayable;

  public int InputIndex(string nodeKey, string inputName);
}

public static class FluentGraphStore
{
  internal static void Register(PlayableGraph graph, FluentGraphRegistry registry);
  public static FluentGraphRegistry Get(PlayableGraph graph);
}

public sealed class FluentBuilder
{
  public PlayableGraph Graph { get; }

  public static FluentBuilder Create(string name = "PlayableGraph");
  public static FluentBuilder Create(PlayableGraph graph);

  public FluentBuilder Output(Animator animator, out AnimationPlayableOutput output, string name = "AnimationOutput");
  public FluentBuilder Input(AnimationPlayableOutput output, int index = 0);
  public FluentBuilder Input<TDestination>(TDestination destination, int index, string name = null)
    where TDestination : struct, IPlayable;
  public FluentBuilder AddInput<TDestination>(TDestination destination, string name, out int index)
    where TDestination : struct, IPlayable;

  public TopologyScope Scope(string path);

  public FluentBuilder WithMixer<TMixer>(int inputCount, out TMixer mixer, string name = null, int outputCount = 1)
    where TMixer : struct, IPlayable;
  public FluentBuilder WithClip(AnimationClip clip, out AnimationClipPlayable playable, string name = null);
  public FluentBuilder WithScript<TBehaviour>(out ScriptPlayable<TBehaviour> playable, int inputCount = 0,
    int outputCount = 1, string name = null)
    where TBehaviour : PlayableBehaviour, new();
  public FluentBuilder WithPlayable<TPlayable>(Func<PlayableGraph, TPlayable> factory, out TPlayable playable,
    string name = null)
    where TPlayable : struct, IPlayable;

  public FluentBuilder WithPlayable<TPlayable>(TPlayable playable, int sourceOutputPort = 0)
    where TPlayable : struct, IPlayable;
  public FluentBuilder WithPlayable<TPlayable>(string key, out TPlayable playable, int sourceOutputPort = 0)
    where TPlayable : struct, IPlayable;

  public FluentBuilder WithWeight<TDestination>(TDestination destination, int input, float weight)
    where TDestination : struct, IPlayable;
  public FluentBuilder WithWeight<TDestination>(TDestination destination, string inputName, float weight)
    where TDestination : struct, IPlayable;
  public FluentBuilder WithWeight<TOutput>(TOutput output, float weight)
    where TOutput : struct, IPlayableOutput;

  public FluentBuilder Layer(AnimationLayerMixerPlayable layerMixer, int input, bool additive = false,
    AvatarMask mask = null);

  public PlayableGraph Build(bool play = true);
}

public sealed class TopologyScope
{
  public string Path { get; }

  public TopologyScope Scope(string path);
  public TopologyScope Input(AnimationPlayableOutput output, int index = 0);
  public TopologyScope Input<TDestination>(TDestination destination, int index, string name = null)
    where TDestination : struct, IPlayable;
  public TopologyScope AddInput<TDestination>(TDestination destination, string name, out int index)
    where TDestination : struct, IPlayable;

  public TopologyScope WithMixer<TMixer>(int inputCount, out TMixer mixer, string name = null, int outputCount = 1)
    where TMixer : struct, IPlayable;
  public TopologyScope WithClip(AnimationClip clip, out AnimationClipPlayable playable, string name = null);
  public TopologyScope WithScript<TBehaviour>(out ScriptPlayable<TBehaviour> playable, int inputCount = 0,
    int outputCount = 1, string name = null)
    where TBehaviour : PlayableBehaviour, new();
  public TopologyScope WithPlayable<TPlayable>(Func<PlayableGraph, TPlayable> factory, out TPlayable playable,
    string name = null)
    where TPlayable : struct, IPlayable;

  public TopologyScope WithPlayable<TPlayable>(TPlayable playable, int sourceOutputPort = 0)
    where TPlayable : struct, IPlayable;
  public TopologyScope WithPlayable<TPlayable>(string key, out TPlayable playable, int sourceOutputPort = 0)
    where TPlayable : struct, IPlayable;

  public TopologyScope WithWeight<TDestination>(TDestination destination, int input, float weight)
    where TDestination : struct, IPlayable;
  public TopologyScope WithWeight<TDestination>(TDestination destination, string inputName, float weight)
    where TDestination : struct, IPlayable;
  public TopologyScope WithWeight<TOutput>(TOutput output, float weight)
    where TOutput : struct, IPlayableOutput;
  public TopologyScope Layer(AnimationLayerMixerPlayable layerMixer, int input, bool additive = false,
    AvatarMask mask = null);

  public FluentBuilder CompileAs<TPlayable>(TPlayable root)
    where TPlayable : struct, IPlayable;
}
```

Implementation requirements:

- `TopologyPath.Normalize(...)` rejects `.` and `..` path segments;
- duplicate full node paths throw immediately;
- duplicate scope paths throw immediately;
- duplicate short names are stored but cannot be resolved by short name;
- wrong typed resolution throws with a clear message;
- `.Input(...)` validates existing input count;
- `.AddInput(...)` expands destination input count and returns the new index;
- pending inputs are tracked explicitly;
- source output claims are tracked as `(sourceHandle, outputPort)` pairs;
- destination input claims are tracked as `(destinationHandle, inputIndex)` pairs;
- `Output(...)` attachments use `SetSourcePlayable(...)`;
- output weights are assigned through `SetWeight(...)`;
- playable attachments use `graph.Connect(...)` and throw if Unity rejects the
  connection;
- `Build(...)` registers the graph metadata and runs validation before optional
  `Graph.Play()`.

## Verification Contract [Agent-Development stage]

The core API is complete only when tests cover the following behavior.

### Rules Public Surface

- `FluentBuilder.Create(string)` creates a new graph.
- `FluentBuilder.Create(PlayableGraph)` reuses the existing graph.
- `Build(play: true)` plays the graph.
- `Build(play: false)` does not play the graph.
- There is no `ForExisting`, `DestroyFluent`, `WithAnimationJob<T>`,
  `WithWeight(float)`, or automatic output-allocation API.

### Creation

- mixer input counts match the requested value;
- mixer output count defaults to 1;
- mixer output count can be set explicitly;
- unsupported mixer types fail clearly;
- clips are paused and have foot IK disabled;
- factory `WithPlayable(...)` registers and connects arbitrary Unity playables;
- existing-handle `WithPlayable(...)` requires a pending input;
- key-based `WithPlayable(...)` resolves and attaches when an input is pending;
- key-based `WithPlayable(...)` is lookup-only when no input is pending.

### Paths And Lookup

- path normalization handles slashes and backslashes;
- `.` and `..` segments throw;
- duplicate node paths throw;
- duplicate scope paths throw;
- full-path lookup is exact;
- unique short-name lookup succeeds;
- ambiguous short-name lookup throws;
- wrong typed lookup throws.

### Inputs And Weights

- `.Input(...)` validates bounds;
- `.AddInput(...)` expands input count by one;
- input names resolve to the expected indices;
- duplicate input names on one destination throw;
- conflicting names for one input index throw;
- unconsumed pending inputs throw at `Build(...)`;
- starting a second pending input before consuming the first throws;
- destination input reassignment throws;
- `WithWeight(destination, input, weight)` updates the exact input;
- `WithWeight(destination, inputName, weight)` resolves and updates the exact
  input.
- `WithWeight(output, weight)` updates the output root weight.

### Connections And Source Ports

- output roots use `SetSourcePlayable(...)`;
- output roots use `SetWeight(...)` when weighted explicitly;
- playable inputs use `graph.Connect(...)`;
- default source output port is 0;
- explicit source output ports are honored;
- negative source output ports throw;
- source output ports outside the source output count throw;
- duplicate source-output claims throw;
- no source output port is allocated automatically.

### Layers

- `.Layer(...)` applies additive flags;
- `.Layer(...)` applies an avatar mask when provided;
- `.Layer(...)` does not apply a mask when null;
- layer input bounds are validated.

### Rules Build Validation

- unreachable registered nodes throw;
- lookup-only nodes still must be reachable from an authored output;
- raw reserved input capacity is allowed when inputs were not declared through
  Fluent metadata;
- all declared inputs must be consumed exactly once;
- graph metadata remains available through `PlayableGraph` extension methods
  after `Build(...)`.

## License

MIT license.

[LICENSE](LICENSE)

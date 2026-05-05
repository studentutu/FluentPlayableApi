# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.0.3] - 2026-05-05

### Changed

- Replaced `FluentBuilder.Build(...)` with `FluentBuilder.Verify()`.
- Playback is now explicit through `PlayableGraph.Play()`.
- Rejected duplicate node paths before creating the Unity playable.

## [1.0.2] - 2026-05-05

Adding proper dispose and changing namespace (to be more compact)

## [1.0.1] - 2026-05-04

Updated docs.

## [1.0.0] - 2026-05-04

### This is the first release of *\<FluentPlayableApi\>*.

Fluent API for the Unity Playable API:
 - added core API
 - added documentation and usage with examples and advanced extension example
 - integrated into project and verified with actual usage

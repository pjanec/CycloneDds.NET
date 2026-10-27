# Changelog

All notable changes to CycloneDDS.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## unreleased

### Added
- **`[DdsQos(AutodisposeUnregisteredInstances = ...)]`** — the writer data lifecycle
  QoS. The default `true` keeps today's behaviour (unregistering an instance also
  disposes it, readers see `NotAliveDisposed`). With `false`, `UnregisterInstance`
  only releases this writer's ownership — readers see `NotAliveNoWriters` once no
  writer is left — so a keyed instance can be handed over to another writer.
  `DdsApi.dds_qset_writer_data_lifecycle` is imported for writers created with an
  explicit QoS handle.
- **`DdsWriter<T>.WaitForAcks(TimeSpan timeout)`** — wraps `dds_wait_for_acks`.
  Returns `true` once every matched reliable reader has acknowledged everything
  written so far (disposes and unregisters included), `false` on timeout; any other
  failure throws `DdsException`. `DdsApi.dds_wait_for_acks` is imported too.
- **`CycloneDDS.Schema.DdsTopicName`** — the one rule for a type's DDS topic name
  (`Of<T>()`, `Of(Type)`, and the string core `Of(string ns, IEnumerable<string> typePath)`),
  now shared by the runtime, the code generator and DdsMonitor.

### Fixed
- **Sender identity could stay unresolved for a delivered sample.** When the identity
  sample had arrived but the thread-pool-driven monitor had not processed it yet (e.g.
  under thread-pool starvation), `DdsLoan.GetSender` returned `null`. A miss now
  drains the identity reader synchronously (under a lock) before giving up.
- **Noisy participant dispose.** Disposing a participant with sender tracking could
  print `[SenderRegistry] Monitor task failed: ...` (`ObjectDisposedException` or
  `dds_take failed: BadParameter`). The registry now stops and awaits its monitor
  before disposing the identity reader.
- **A `[DdsTopic]` without a name now works at runtime.** `DdsWriter`/`DdsReader`
  opened such a topic with a `null` name; they now use the documented derived name
  (namespace + containing types + type name, joined with `_`; a whitespace name counts
  as none). Explicit names are unchanged.
- **The derived topic name is spelled the same everywhere.** For a nested type the
  code generator dropped the outer type (`Ns_Inner`) and DdsMonitor kept the CLR
  `+` (`Ns_Outer+Inner`); both now give `Ns_Outer_Inner`. Top-level types are unchanged.

## 0.3.2

### Fixed
- **SourceLink symbols for the whole library.** `CycloneDDS.Core` and
  `CycloneDDS.Schema` now ship their SourceLink-enabled PDBs in `lib/net8.0`
  (previously only `CycloneDDS.Runtime` did), so consumers can step into all three
  assemblies straight from the package.

### Added
- **Automatic NuGet publish on a version tag.** Pushing a `vX.Y.Z` tag (e.g. via a
  GitHub Release) builds and verifies the cross-platform package on Windows and
  Linux, then publishes it — and the `ddsmonitor` tool — to NuGet.org. Requires the
  `NUGET_API_KEY` repository secret.
- **CI now verifies the packaged output on both platforms** before publishing (a
  Windows smoke test + a Linux job that consumes the finished package), and uploads
  the `win-x64` native as a workflow artifact.
- **`examples/PackageSmokeTest`** and **`build/test-package.{sh,ps1}`** for
  smoke-testing the package (and the `ddsmonitor` tool) locally on either OS.

### Changed
- Packing now succeeds on Linux too (Windows-only pack entries are `Exists`-guarded),
  so a per-platform package can be built locally on either OS.
- CI builds set `ContinuousIntegrationBuild` for deterministic PDBs with normalized
  SourceLink source paths.

## 0.3.1

### Added
- **Linux x64 support.** The package now ships native assets for `linux-x64`
  (`libddsc.so`) alongside `win-x64`, and bundles the Linux `idlc` plus its
  `.so` dependencies under `tools/`. A new `build/native-linux.sh` compiles the
  native CycloneDDS libraries and `idlc` and rewrites their RPATH to `$ORIGIN`
  so they resolve in the flat NuGet `tools/` layout. `IdlcRunner` is now
  platform-aware — it locates `idlc`/`idlc.exe` under the correct RID, sets
  `LD_LIBRARY_PATH`, and restores the Unix execute bit that NuGet does not
  preserve — and the MSBuild native-asset copies are guarded by
  `IsOSPlatform`. The target framework remains `net8.0`.
- **`ddsmonitor` global tool runs on Linux.** Its tool payload now bundles both
  the win-x64 (`ddsc.dll`) and linux-x64 (`libddsc.so`) native runtime, so
  `dotnet tool install -g CycloneDDS.NET.DdsMonitor` works on either OS.
  Previously the tool carried only the pack host's native and would fail with a
  `DllNotFoundException` on the other platform. The bundled `IdlImporter` also
  works on Linux via the platform-aware `idlc` shipped in the package's `tools/`.

### Fixed
- **DDS matched-status events never fired on Linux.** The publication- and
  subscription-matched listener callbacks passed their 24-byte status struct by
  `ref`, which matches the Windows x64 ABI but not the Linux System V ABI (where
  structs larger than 16 bytes are passed by value on the stack), so `arg` was
  read from the wrong register and every callback was silently dropped. The
  status is now passed by value, letting the marshaller emit the correct ABI on
  both platforms, so `PublicationMatched`, `SubscriptionMatched` and
  `WaitForReaderAsync` work on Linux.
- **Mis-cased project references.** Several `ProjectReference` paths used
  `..\..\Src\...` (capital `S`), which resolved only on case-insensitive
  (Windows) filesystems; corrected to `src` so the solution builds on Linux.

### Changed
- CI builds native libraries on both Windows and Linux and produces a single
  cross-platform package. It installs both the .NET 8 and .NET 10 SDKs: the code
  uses C# 13 language features that require the newer compiler, while the target
  framework stays `net8.0`.
- Package description updated from "Win64" to "Windows x64, Linux x64".

## 0.2.3

### Fixed
- **Code-generation stamp file placed outside the generated folder.** A missing path
  separator wrote the incremental stamp as a sibling of the generated directory
  (`...CycloneDdsGeneratedcodegen.stamp`) instead of inside it. When the folder was cleaned
  but the orphaned stamp survived, the next build skipped code generation yet compiled with no
  generated files — producing an assembly missing its `[assembly: DdsIdlMapping]` metadata.
  Downstream projects could then no longer emit the matching `#include` and `idlc` failed with
  *"Scoped name '…' cannot be resolved"*. The stamp now lives inside the generated directory,
  so `Clean` (and any wipe of the folder) removes it together and forces a correct regen.
- **Generated `.idl` not copied to output on a clean build.** The output step ran (via
  `AfterTargets`) before generation completed, so on a clean build the `.idl` never reached
  `bin`; a referencing project's `idlc` then failed with *"Can't open include file"*. The copy
  is now performed deterministically inside the generation target, immediately after the files
  are produced.
- **Inconsistent `#include` emission.** The IDL type name and its `#include` were resolved by
  two independent paths that could disagree, silently producing an un-includable IDL. They now
  share one resolution path, and the generator fails fast with an actionable message (naming the
  type, field, and likely cause) instead of letting `idlc` surface a cryptic error later.

### Changed
- **Code generation now runs only where it's needed.** Previously the build targets flowed to
  every project in the transitive reference closure and ran the (Roslyn-based) generator
  unconditionally — dozens of needless runs. Generation is now gated to projects that (a) have a
  **direct** `PackageReference` to CycloneDDS.NET and (b) actually declare DDS types or enums
  (a fast in-process source scan, no extra process launch). A project that authors DDS types but
  references the package only transitively can opt in with
  `<CycloneDdsCodeGenEnabled>true</CycloneDdsCodeGenEnabled>`.
- **Unified the MSBuild integration into a single source of truth.** The packaged
  (`buildTransitive`) targets and the in-repo targets were duplicated; they are now one file,
  with environment differences expressed as overridable properties.

### Added
- `build/CycloneDDS.NET.props` (imported only for direct package references) and the
  `CycloneDdsCodeGenEnabled` opt-in property.

## 0.2.2
- Partitions
- WaitSets
- Removed Newtonsoft.Json reference
- Incremental code generation. File delta tracking to limit unnecessary disk writes and Roslyn intellisense thrashing.
- Union fix (critical structural alignment bug causing misaligned DDS union payloads.)
- Added explicit translation fallback logic for type identity boundaries across foreign assembly contexts.
- Added support for:
   - sparse/non-contiguous enums via `@value` annotations,
   - enum wire sizing bounds (`@bit_bound`),
   - C# `[InlineArray]` memory patterns,
   - and explicit generation toggles (`CycloneDdsDisableCodeGen`).
- DdsMonitor tool (blazor based UI trafic monitor)
   - Multi-Participant Configuration
   - Dynamic Topic Discovery from .net assemblies
   - Topic explorer
   - Sample list & sample detail windows linked together
   - Data Grid Customization
   - Filtering and Sorting
   - Recording & replay
   - Sample authoring & sending
   - Sender tracking
   - Headless mode (CLI tool)
   - Traffic stats


### 4. CycloneDDS Code Generator Enhancements

* **Schema Extensions:** 

## 0.1.25

### Added
- Initial public release of CycloneDDS.NET
- Zero-allocation write path with custom marshaller
- Zero-copy read path using `ref struct` views
- Code-first schema DSL with attributes (`[DdsTopic]`, `[DdsKey]`, `[DdsStruct]`)
- Automatic IDL generation from C# types
- IDL import tool for converting existing IDL to C# DSL
- Async/await support with `WaitDataAsync`
- Client-side filtering with compiled predicates
- Sender tracking (Computer, PID, custom app ID)
- NuGet package with bundled native binaries and build tools
- Automatic C# code generation during build
- Support for keyed topics with O(1) instance lookup
- Full interoperability with other DDS implementations

### Changed

### Deprecated

### Removed

### Fixed

### Security

---

## Release Notes

### Version Numbering

This project uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) for automatic semantic versioning based on git tags and commit history.

### How to Read This Changelog

- **Added** - New features
- **Changed** - Changes in existing functionality
- **Deprecated** - Soon-to-be removed features
- **Removed** - Removed features
- **Fixed** - Bug fixes
- **Security** - Vulnerability fixes

[Unreleased]: https://github.com/pjanec/CycloneDds.NET/compare/HEAD...HEAD

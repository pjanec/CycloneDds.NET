# Changelog

All notable changes to CycloneDDS.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 1.2.12

Versioned **1.2** for both packages. `CycloneDDS.NET` jumps from 0.3 to 1.2 to share the version
of `CycloneDDS.NET.DdsMonitor` (which already shipped 1.1); from now on the two always carry the
same version, so a monitor matches the library and native Cyclone DDS of the same number. The
1.x line keeps the public API compatible (additive changes only). This release brings a new
native Cyclone DDS major version, new Linux library names and platform baseline.

### Added
- **IntelliSense documentation:** the package now ships the XML doc files of
  `CycloneDDS.Runtime`, `CycloneDDS.Core` and `CycloneDDS.Schema`, so the documentation
  comments show in the editor.
- **DDS Security** (authentication, access control, encryption). The native build enables
  Cyclone DDS's security plugins and the package ships them; they are loaded only when the
  domain configuration contains a `<Security>` section, so nothing changes for applications
  that don't configure it. TCP+TLS stays off, so `ddsc`/`libddsc` themselves do **not**
  depend on OpenSSL. The plugins need OpenSSL 3 at runtime, which is **not shipped**: Linux
  uses the distribution's `libcrypto.so.3`; on Windows the application provides it (on `PATH`,
  via the new `CycloneDdsOpenSslDir` MSBuild property, or preloaded). On Windows the build
  targets copy the plugins next to the application (`CycloneDdsStageSecurityPlugins=false`
  opts out). Building the natives now needs the OpenSSL development files.
  Based on [pjanec/CycloneDds.NET#4](https://github.com/pjanec/CycloneDds.NET/pull/4).
- CI caches the native builds (keyed on the Cyclone DDS revision, `native/`, the build
  script and the runner image); `build/pack.ps1 -SkipNativeBuild` reuses staged natives.
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
- **Ownership, liveliness and deadline QoS.** `[DdsQos]` gains `Ownership`
  (`DdsOwnershipKind`), `LivelinessKind` (`DdsLivelinessKind`), `LivelinessLeaseDurationMs`
  and `DeadlineMs`; all default to the DDS defaults, so existing types are unchanged.
- **`DdsWriterOptions` / `DdsReaderOptions`** — per-entity QoS passed through new constructor
  overloads `DdsWriter(participant, topicName, DdsWriterOptions, partition)` /
  `DdsReader(participant, topicName, DdsReaderOptions, partition)`: `OwnershipStrength`, and
  overrides of the attribute's `AutodisposeUnregisteredInstances`, liveliness and deadline.
  The existing constructors are unchanged.
- **Liveliness and deadline status:** `DdsWriter.AssertLiveliness()`; events
  `DdsReader.LivelinessChanged` / `RequestedDeadlineMissed` and `DdsWriter.LivelinessLost` /
  `OfferedDeadlineMissed`, plus the matching `...Status` properties. The `DdsApi` imports
  behind them (`dds_qset_ownership`, `dds_qset_ownership_strength`, `dds_qset_liveliness`,
  `dds_qset_deadline`, `dds_assert_liveliness`, the listener setters and status getters) are public.
- **`CycloneDDS.Schema.DdsTopicName`** — the one rule for a type's DDS topic name
  (`Of<T>()`, `Of(Type)`, and the string core `Of(string ns, IEnumerable<string> typePath)`),
  now shared by the runtime, the code generator and DdsMonitor.

- **DdsMonitor headless recording for scripts and AI agents.** New `--DdsSettings:` options:
  - `RecordFormat`: `JsonLines` (default) writes one compact record per line, with the DDS
    `TopicName`. Only whole lines are flushed, so the file can be read while it is being written.
    `Json` writes the previous single JSON array.
  - `RecordDurationSeconds`, `RecordMaxSamples`: stop by itself.
  - `RecordConsole`: `Quiet` (default), `Status` (a line every 5 s plus a final per-topic summary,
    on stderr) or `PerSample` (the previous `[RECORD]` line per sample).
  - `RecordSummaryFile`: a small JSON file with per-topic counts, failed topics and
    running/stopped state, rewritten every second.
  - `RecordMaxFileSizeMB`: JSON Lines rotation to `name.0001.jsonl`, `name.0002.jsonl`, ...
  - `RecordBytesAsBase64` (default on): fixed byte buffers as one base64 string; import reads both forms.
  - `HeadlessFilePath=-`: write the capture to stdout; logs then go to stderr.
  - Headless exit codes: `2` when the run fails, `3` when a topic could not be recorded.
  - Import, replay and the UI's replay panel read JSON Lines captures too.
  - The DdsMonitor README has a short runbook: "Recording for scripts and AI agents".
- **`ddsmonitor --help`** prints every command-line argument and the recording workflow on one
  screen, so the installed tool documents itself; `ddsmonitor --version` prints the version. A
  test fails when a setting is missing from the help.
- The `CycloneDDS.NET.DdsMonitor` package carries the DDS Monitor README (with a new quick
  start) instead of the library's, and the main README introduces the tool.
- `IDdsBridge.ReaderFailed` (with a default no-op implementation, so existing implementations
  still compile).

### Changed
- **`CycloneDDS.NET` no longer depends on Roslyn** (`Microsoft.CodeAnalysis.CSharp` /
  `.Common` 4.8.0), which pulled about 9 MB of the C# compiler into every application using
  the package. Nothing in the library used it; the code generator runs from the package's
  `tools/` folder with its own copy. The `ddsmonitor` tool is about as much smaller. An
  application that used Roslyn only through this package must now reference it itself.
- **DdsMonitor's built-in `SelfTest.*` topics exist only while self-send is on.** They used to be
  registered and subscribed on every start, cluttering the topic list and every recording.
- **DdsMonitor headless recording defaults changed.** It now writes JSON Lines with base64 byte
  buffers and prints nothing per sample, instead of one indented JSON array with a `[RECORD]`
  console line per sample. For the old output use `--DdsSettings:RecordFormat=Json
  --DdsSettings:RecordConsole=PerSample --DdsSettings:RecordBytesAsBase64=false`.
- **Package metadata names the native Cyclone DDS version.** The description, release
  notes and `native_version.json` now state the Cyclone DDS version (from its CMake
  project), the exact upstream commit and that it is unmodified, instead of the
  fork-relative "based on / modified from" commits.
- **Native Cyclone DDS upgraded to 11.x** (upstream `master` as of 2026-10-22, i.e. 11.0.1
  plus later fixes), from the January 2026 pre-11.0 snapshot. Brings upstream's hardening
  against malformed network input (CDR deserialization and discovery bounds checks, stricter
  SPDP/SEDP GUID-prefix validation, an address-set use-after-free fix), XTypes improvements
  (recursive types, try-construct, sparse enums, union label fixes) and networking fixes.
  The wire protocol is unchanged: nodes on the previous package and on this one interoperate.
  Generated type descriptors now carry the new required-prefix / try-construct metadata, so
  generated code must be used with the native library of the same package (as before).
- **The `cyclonedds` submodule now tracks the official `eclipse-cyclonedds/cyclonedds`**
  repository, unmodified, instead of the `pjanec/cyclonedds` fork. The only fork change,
  the `idljson` IDL compiler plugin used by the code generator, now lives in this
  repository under `native/idljson` and is built next to Cyclone DDS by the new
  `native/CMakeLists.txt` (used by both native build scripts). Its fork-era workarounds
  for recursive structs and union default labels are superseded by upstream fixes.
  Existing clones: `git submodule sync && git submodule update --init --recursive`.
- **Linux: native libraries are built in an `ubuntu:22.04` container**, so they need
  glibc 2.35 or older instead of the CI runner's 2.38+ (previous packages did not load
  on Ubuntu 22.04 / Debian 12). CI fails if a binary ever requires a newer glibc.
- On Linux the native libraries now carry the SONAME `.so.11` (was `.so.0`);
  `build/native-linux.sh` stages each library under its real SONAME and the package
  bundles `*.so.*`.
- **The code generator refuses an enum whose values do not fit the IDL enum, with a message in C# terms.** Cyclone DDS 11's
  `idlc` reads an enumerator as a **signed** value of `@bit_bound` bits, so a `byte` enum holds 0..127 (not 0..255), a `ushort`
  enum 0..32767 and a `uint` enum 0..2147483647. Such an enum used to reach `idlc` and fail there with `Enumerator overflow` /
  `@value(…) cannot be applied to 'enumerator' element`; the generator now stops first and names the C# type, the member(s), the
  allowed range and the two ways out (lower the value, or widen the underlying type — which changes the enum's size on the wire).
  It does not widen silently: the wire size is part of the contract.
  **Limitation (upgrading from 0.3.x):** IDL has no unsigned enum, so an enum that used the *unsigned* half of its type — `byte`
  values 128..255, a `ushort` above 32767, a `uint` (a flags mask with the top bit set, `0xFFFFFFFF`) — can no longer be a DDS
  type. Such an enum builds with 0.3.x and fails the build with 1.2. Renumber it to the limit or below, or widen the underlying
  type (which changes its size on the wire, so nodes on 0.3.x and 1.2 no longer agree on it), or carry the value as a plain integer field.

### Fixed
- **The IDL importer maps string typedefs to strings** ([#5](https://github.com/pjanec/CycloneDds.NET/issues/5)).
  `typedef string T_String;` and `typedef string<8> T_ShortString;` came out as `byte` fields: the
  idljson plugin recorded a string typedef's C type (`char`) instead of `string` and its bound.
  They now import exactly like the same members declared without the typedef (`string`, with
  `[MaxLength(N)]` for a bounded one, also through a typedef of a typedef), and the plugin's
  layout gives a bounded string typedef its inline `char[N+1]` size.
- **The native Cyclone DDS version is recorded in the package as intended:** in the package
  description and as `native_version.json` in the package root (it reached only the release
  notes, because NuGet had read the description and collected the files before it was added).
- **DDS Monitor installed as a `dotnet tool` can run on a secured domain on Windows.** Windows
  looks for the security plugins beside the started executable, which for a tool is the small
  launcher, not the tool's store folder, so a secured monitor stopped with `Could not load
  Authentication library`. The monitor now adds its own folder to `PATH` at start-up. The
  Windows package test (`build/test-package.ps1`) gained a real publish/record round trip and a
  secured/unsecured check, as `test-package.sh` has on Linux; the README documents the secured
  setup on Windows.
- The DDS Monitor README documented a `PluginDirectories` option that does not exist (plugins
  load from the `plugins` folder next to the executable), and said self-send is on by default
  (it is off).
- **DdsMonitor samples leave in ordinal order.** Reader threads of different topics could hand
  on samples out of ordinal order (neighbouring pairs swapped), so a capture reader that keeps
  the last ordinal it saw (`select(.Ordinal > N)`) could skip a sample. Taking the ordinal and
  handing the sample on are now one step.
- **A headless recording that ends while ddsmonitor is still starting** (e.g. `RecordMaxSamples`
  met at once by durable history) no longer aborts the process (exit 134); the host is stopped
  once it has started, and the recorder's exit code stands.
- **A relative `--AppSettings:TopicSources` path loads its topics.** It silently loaded nothing,
  because the assembly loader needs an absolute path; a headless run now also warns when a
  topic source yields no topic types.
- **DdsMonitor filter macros no longer leak between compilers.** All filter compilers shared
  one static macro registry, so the most recently created compiler's macros applied everywhere;
  each compiler now resolves its own.
- **DdsMonitor reads topics whose sample is 64 KiB or larger** (e.g. a `fixed byte Payload[65536]`
  blob). The reader passed each sample by value to a helper, followed by more arguments; the .NET
  JIT rejects such a call once the struct is 64 KiB or more (`InvalidProgramException` in
  `DynamicReader.DrainReader`). The `CycloneDDS.NET` reader and writer APIs take samples by
  reference (or as their only argument, like `SetFilter`'s predicate) and are not affected.
  These topics recorded nothing, and the error only came out at shutdown, where it killed the process.
  Samples are now sized by reference and boxed once.
- **A topic that cannot be read no longer takes ddsmonitor down.** The error is logged when it
  happens, naming the topic, and only that topic stops. Previously the failed read task stayed silent
  and its exception was rethrown at shutdown. New: `DynamicReaderConfig.OnReadError`,
  `DynamicReader<T>.ReadError`, and a `DdsBridge` constructor overload that takes an `ILogger`.
- **A headless recording is always valid JSON.** Stopping it with Ctrl+C used to skip writing the
  closing `]`. A sample whose payload cannot be serialized is now skipped and logged once per
  topic, instead of ending the recording.
- **DdsMonitor headless mode no longer grabs port 5000.** Headless record/replay serves no UI,
  but its web host still bound Kestrel's default `http://localhost:5000`, so a second headless
  instance (or anything else on 5000) failed to start (the host aborted). It now binds a free
  port. `build/test-package.sh` (run by CI's linux-verify job against the cross-platform
  package) now also runs a DDS round trip between two installed `ddsmonitor` instances, and the
  same with DDS Security on (secured peers exchange data, an unsecured one gets nothing).
- **Linux: the SONAME file now ships where the loader looks.** Since the Cyclone DDS 11 upgrade `libddsc` carries SONAME `.so.11`, but
  the pack items for `runtimes/linux-x64/native` (CycloneDDS.NET) and for the ddsmonitor tool still named `libddsc.so.0`, so they matched
  nothing and silently packed nothing (`libddsc.so.11` only reached `tools/`). Both now use a `libddsc.so.*` glob, so the SONAME file
  sits next to `libddsc.so` and a future SONAME bump needs no edit. Checked on the package listing with stand-in files; the real Linux
  libraries are built and exercised by the CI Linux jobs.
- **FeatureDemo sample:** the stock ticker topic kept only the last tick (unkeyed topic with
  the default KEEP_LAST 1), so subscribers mostly saw one symbol; it now keeps a backlog of
  100. The chat room UI no longer throws on a narrow or redirected console. (Both were
  failing `FeatureDemo.Tests`.)
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

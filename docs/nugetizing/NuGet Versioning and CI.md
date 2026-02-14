# NuGet Versioning and CI


## Prompt:
I have a csharp wrapper library over a native plain c dll. The library is also calling an exe internal tool located next to the dll, as a prebuild step, to generate cs files to be part of compilation. There is also other exe tool for manual use, with command line interface. I want to make a nuget from it that contains the native dll and exe. All 64bit only. Repo on github. Csharp parts are .net 8, multiple assemblies. Native dll and exe compiled using cmake and their source storef as submodule of my repo. I want to know the best practices how 1. Version the library - where to store the nuget version, how to propagate it to csproj versions and to nuget packager 2. Make it easy to release a new version (build script..) which is both manual as well as compatible with ci pipelines (best the one on github). 3. How to make it publishable to a public nuget repo. 4. How to make it easy to use (install nuget to a project and forget) 5. How to prepare for maintaining it - managing issue list, contributions etc. I want to be prepared and be professional so all feels intuitive. Pls suggest, ask etc.

## Response:
Přemýšlení po dobu 13s

Below is a “professional-grade” setup that works well for **.NET 8 multi-assembly managed wrapper + native (CMake) DLL + companion EXEs**, shipped as **64-bit-only NuGet** from **GitHub** with a clean release flow.

* * *

1) Versioning best practices (single source of truth)
-----------------------------------------------------

### What you want

*   **One version number** that:
    *   stamps every `.csproj` (Assembly + NuGet package)
    *   ties to **Git tags/releases**
    *   is reproducible in CI
    *   supports SemVer (`1.2.3`, optionally `-preview.1`)

### Recommended approach: Git-tag driven versioning

Use **Nerdbank.GitVersioning** (very common in .NET libraries) or **MinVer**. Both are fine; I’ll recommend **Nerdbank.GitVersioning** when you have multiple projects and want strong control.

**Pattern:**

*   Store version configuration in repo root (e.g. `version.json`)
*   CI builds derive version from Git history/tags
*   `dotnet pack` produces packages with correct version automatically

**Where to store it**

*   `version.json` at repo root (NBGV)
*   or `Directory.Build.props` if you insist on manual versioning (less ideal for CI)

**Propagate to csproj & packager**

*   Don’t hand-edit versions in every `.csproj`.
*   Use **Central Package/Build properties** + version tool.

**Concrete setup (NBGV)**

*   Add package to repo (one-time):
    *   `Nerdbank.GitVersioning`
*   Add `version.json` (single source of truth)
*   In CI, ensure tags are fetched (`fetch-depth: 0`)

**When to bump versions**

*   You bump by creating a Git tag (or by editing `version.json`, depending on your policy).
*   Recommended release: tag `v1.2.3` → build → pack → push.

* * *

2) Make releasing easy (manual + CI friendly)
---------------------------------------------

### Goal

*   Same steps locally and in GitHub Actions.
*   Release is deterministic and auditable.

### Recommended release pipeline

**A) Local dev flow**

*   Build native: CMake → output `dll` + `exe`s
*   Build managed: `dotnet build`
*   Pack: `dotnet pack -c Release`
*   (Optional) test + smoke test sample app

**B) GitHub Actions flow**  
Trigger on:

*   tag push `v*.*.*`
*   (optional) manual `workflow_dispatch`

Pipeline stages:

1.  Checkout with submodules
2.  Build native (CMake, win-x64)
3.  Build managed
4.  Pack NuGet (including native assets)
5.  Publish to NuGet.org
6.  Create GitHub Release + attach `.nupkg` / `.snupkg`

### Key best practices

*   Use a single script (PowerShell) that GH Actions calls.
*   GH Actions should be minimal glue, not business logic.
*   Keep secrets only in GH Secrets (`NUGET_API_KEY`).

* * *

3) Publishable to public NuGet (what “professional” means)
----------------------------------------------------------

Minimum metadata that makes NuGet consumers trust it:

*   **PackageId**: stable + unique (e.g., `YourCompany.YourLib`)
*   **Authors**, **Company**
*   **RepositoryUrl** (GitHub)
*   **License**: ideally `PackageLicenseExpression` (MIT/Apache-2.0/etc.)
*   **Readme**: `PackageReadmeFile`
*   **Icon**: `PackageIcon`
*   **Tags**, **Description**
*   **Symbol package** + **SourceLink** (debuggable stack traces)

Operational:

*   NuGet.org account with **2FA**
*   Create API key limited to your package scope
*   Sign packages if you’re in a higher-trust environment (optional, but good)

* * *

4) “Install and forget” consumer experience (native + tools + codegen)
----------------------------------------------------------------------

This part is the most important technically because you have:

*   native DLL (must land in output)
*   internal exe used during build (codegen prebuild)
*   separate CLI exe for manual use

### Strong recommendation: split into 2–3 NuGet packages

This is the most intuitive and maintainable long-term:

1.  **`YourLib.Runtime`**
    *   Contains: managed runtime assemblies + native `runtimes/win-x64/native/*.dll` (and _maybe_ runtime-needed exe if it must be deployed)
*   Consumer adds package reference and everything “just works” at runtime.
    2.  **`YourLib.Build`** (or `YourLib.CodeGen`)
    *   Contains: the **codegen EXE** and **MSBuild targets** (`buildTransitive`) that run during build.
*   Consumer references this only if they need generation (often the main package can depend on it if always required).
    3.  **`YourLib.Tool`** (dotnet tool) _optional but highly recommended_
    *   For the manual CLI: publish as a **`.NET tool`** if you can (best UX).
*   If it must remain native exe, package it as a `tools/` asset, but dotnet tools are more discoverable and cross-platform (even if you only support win-x64).

Why split?

*   Runtime dependencies stay clean.
*   Build-time execution is explicit and safer (people dislike packages that run EXEs unexpectedly).
*   CLI distribution becomes first-class.

### If you insist on a single package

You can still do it, but you must be careful:

*   Put native DLL under: `runtimes/win-x64/native/your.dll`
*   Put EXEs under `tools/` or `runtimes/win-x64/native/`
*   Provide `buildTransitive/*.targets` to invoke codegen at build.
*   Ensure it runs **only when needed**, supports incremental builds, and doesn’t run during restore.

### MSBuild integration best practice (codegen)

*   Put MSBuild `.targets` in `buildTransitive/YourLib.targets`
*   In that target:
    *   locate the exe inside the package via `$(MSBuildThisFileDirectory)`
    *   run it before `CoreCompile` (or earlier if generating source)
    *   emit generated `.cs` into `$(IntermediateOutputPath)\Generated\...`
    *   include them via `<Compile Include="...generated..." />`
    *   add inputs/outputs for incremental build (`Inputs`/`Outputs`)
    *   fail with clear error message if prerequisites missing

This gives the user “install package and build” with no manual step.

* * *

5) Maintenance readiness (issues, PRs, contributions)
-----------------------------------------------------

Here’s the lightweight “open-source professional” kit:

### Repo hygiene files (root)

*   `README.md` with:
    *   quickstart
    *   supported platforms: **Windows x64 only**
    *   API usage snippet
    *   how to enable/disable codegen (if relevant)
*   `LICENSE`
*   `CHANGELOG.md` (or GitHub Releases only, but changelog helps)
*   `CONTRIBUTING.md` (build instructions, coding style, how to run tests)
*   `CODE_OF_CONDUCT.md` (use Contributor Covenant)
*   `SECURITY.md` (how to report vulnerabilities)
*   `SUPPORT.md` (optional)

### GitHub configuration

*   Issue templates: bug / feature request
*   PR template with checklist (tests, docs, version impact)
*   Labels: `bug`, `enhancement`, `breaking`, `good first issue`, `help wanted`
*   CI status badge + NuGet badge in README
*   Dependabot for NuGet + Git submodules (or at least NuGet)

### Release discipline

*   Define SemVer rules:
    *   breaking changes → major
    *   new functionality → minor
    *   fixes → patch
*   Automate release notes generation if you want (optional)

* * *

A concrete “good default” blueprint (summary)
---------------------------------------------

**Versioning**

*   Git tags `vX.Y.Z`
*   NBGV computes package version
*   One pack command produces consistent versions across projects

**Packaging**

*   `YourLib.Runtime` (managed + native)
*   `YourLib.Build` (codegen exe + buildTransitive targets)
*   `YourLib.Tool` (dotnet tool for CLI) if feasible

**CI**

*   GitHub Actions builds native via CMake
*   Packs NuGet (incl. `.snupkg`)
*   Pushes to NuGet + creates GitHub Release

**UX**

*   Consumer: `dotnet add package YourLib.Runtime`
*   If codegen required: package dependency brings in build targets automatically (or explicit `YourLib.Build`)
*   Manual tool: `dotnet tool install -g YourLib.Tool` (best)

* * *

A few targeted questions (answering them lets me give you a near drop-in layout)
--------------------------------------------------------------------------------

1.  **OS support**: is this **Windows-only** or do you want to keep the door open for Linux later? (You said “64-bit only”, but not OS.)
2.  **Codegen frequency**: does codegen run **every build**, or only when input files change?
3.  **Native exe tool**: can the manual CLI be reworked into a **dotnet tool**, or must it remain native?
4.  **Consumer model**: do consumers reference _only_ the wrapper assembly, or do they also directly use the generated `.cs` outputs?

If you answer those, I’ll propose:

*   exact NuGet folder layout (`runtimes/`, `buildTransitive/`, `tools/`)
*   the MSBuild `.targets` skeleton (incremental + deterministic)
*   a GitHub Actions workflow skeleton (with submodules + CMake + pack + push)
*   recommended package split + dependency graph for the best UX.

## Prompt:
The internal tool is a native exe that must be deployed with the base library. The manual tool is .net console app (can be made a dotnet tool) that uses the native dlls and the internal tool exe. Codegen cannot be disabled, the while lib depends on it (unusable without it). Now runs every build as it does not evaluate dependencies but that is planned for future.generated cs outputs become part of user app, they are gennerated out of source tree, to intermediate area (obj folder). Not used directly, used under the hood when user calls library api. User should not need to see them or touch them.

I want to support linux x64 later as well, now just win x64

## Response:
Given your constraints, the cleanest “professional + intuitive” shape is:

*   **One primary NuGet** that users reference and then forget.
*   That package contains **runtime native assets** _and_ **build-time codegen logic** (because codegen is mandatory).
*   A **separate dotnet tool package** for the manual CLI (recommended), which reuses the same native/runtime assets to avoid duplication.

Below is a concrete blueprint that scales to **Linux x64 later**, keeps builds deterministic, and makes CI/releasing easy.

* * *

0) High-level packaging strategy
--------------------------------

### Package A (main): `YourLib`

**What it does**

*   Provides managed wrapper assemblies (`net8.0`)
*   Ships native assets:
    *   the native DLL/so
    *   the mandatory native “internal tool” EXE (or ELF later)
*   Ships **MSBuild targets** in `buildTransitive/` that:
    *   run codegen before compile
    *   write generated `.cs` into `obj/.../Generated`
    *   include those files in compilation automatically

**User experience**

```xml
<PackageReference Include="YourLib" Version="x.y.z" />
```

…and that’s it.

### Package B (optional): `YourLib.Cli` as a dotnet tool

*   `dotnet tool install -g YourLib.Cli`
*   This tool depends on (or carries) `YourLib` assets and can find/ship the native pieces + internal tool.
*   Users who never need the CLI never install it.

This split keeps the “install-and-forget library” clean, while giving you a first-class CLI distribution.

* * *

1) NuGet layout for mandatory native exe + native dll
-----------------------------------------------------

For the **main package**, target RID-specific layout so runtime/native resolution works and Linux is easy later:

```
/lib/net8.0/YourLib.dll
/lib/net8.0/YourLib.OtherAssembly.dll
/buildTransitive/YourLib.targets
/buildTransitive/YourLib.props          (optional)

/runtimes/win-x64/native/yourNative.dll
/runtimes/win-x64/native/internal_codegen_tool.exe
/runtimes/win-x64/native/otherNativeDep.dll (if any)

/runtimes/linux-x64/native/libyourNative.so        (later)
/runtimes/linux-x64/native/internal_codegen_tool   (later)
```

**Important detail:** put the internal native exe next to the native DLL _in the package_ so your targets can execute it from a predictable location and your runtime can deploy it.

* * *

2) BuildTransitive MSBuild targets: mandatory codegen “just works”
------------------------------------------------------------------

Since generated `.cs` are _internal implementation detail_ and live in `obj`, the correct approach is:

*   Generate into `$(IntermediateOutputPath)\YourLibGenerated\`
*   Add them to `Compile` item group
*   Mark inputs/outputs later when you add dependency tracking
*   Ensure it runs at the right time and only once per target framework

### Minimal targets skeleton (works now; can be upgraded later)

Create `buildTransitive/YourLib.targets` in the NuGet:

*   Locate tool via `$(MSBuildThisFileDirectory)` (points inside the restored package)
*   Use `Exec` with `WorkingDirectory`
*   Write to `$(IntermediateOutputPath)`
*   Include generated `.cs`

Conceptually (pseudo-structure, not full code-dump):

*   `PropertyGroup`
    *   `YourLib_ToolsDir = $(MSBuildThisFileDirectory)..\runtimes\$(RuntimeIdentifier)\native\`
    *   `YourLib_CodeGenExe = internal_codegen_tool.exe` (win) / `internal_codegen_tool` (linux later)
    *   `YourLib_GenOutDir = $(IntermediateOutputPath)\YourLibGenerated\`
*   `Target Name="YourLib_Generate" BeforeTargets="CoreCompile"`
    *   ensure directory exists
    *   `Exec Command="&quot;$(YourLib_ToolsDir)$(YourLib_CodeGenExe)&quot; ... --out &quot;$(YourLib_GenOutDir)&quot;"`
    *   add `ItemGroup` with `Compile Include="$(YourLib_GenOutDir)**\*.cs"`

### Practical points you should implement immediately

*   **Fail fast, friendly error** if `$(RuntimeIdentifier)` isn’t set (it matters once you add linux).
    *   For now you can default to `win-x64` _only if_ you explicitly choose to, but it will surprise people when they build AnyCPU.
*   Ensure it runs during **design-time build** in VS _only if necessary_.
    *   If you generate a lot, consider skipping on design-time build to avoid slow IntelliSense, but since codegen is mandatory, you may need it. You can detect `$(DesignTimeBuild)` and decide.
*   Put generated files under `obj` and keep them hidden:
    *   add them to compilation but not to project items (don’t emit them into the source tree)

### About “runs every build”

That’s acceptable for v1. For future incremental generation:

*   Use MSBuild `Inputs`/`Outputs` on the target (or `FileWrites` tracking).
*   Emit a stamp file into `$(YourLib_GenOutDir)` and treat it as an output.
*   Use whatever input list is relevant (config, schema, native headers, etc.)

* * *

3) Make runtime deployment correct (native dll + internal exe in output)
------------------------------------------------------------------------

Even though your codegen runs from the package cache during build, you also said the internal tool exe **must be deployed with the base library**.

That means: ensure both native dll and internal exe are copied to the build output so your runtime can find them “next to the dll”.

Two common approaches:

### Approach A (recommended): extract/copy on build via targets

In the same `.targets`, add copy items:

*   After build, copy:
    *   `runtimes/<rid>/native/*` into `$(OutputPath)` (or into `AppContext.BaseDirectory` at runtime)
*   Use `Copy` task or `Content` items with `CopyToOutputDirectory`

This makes it deterministic and works with non-sdk-style consumers too.

### Approach B: rely on NuGet native runtime asset handling

NuGet will copy native assets for the RID when consuming apps restore/build, **but EXEs are not always handled the way you expect** (dll/so are “native”, exe is just another file). Don’t rely on implicit behavior; explicitly copy the exe via build targets.

Bottom line: **explicit copy is safest**.

* * *

4) Future-proofing for Linux x64
--------------------------------

If you package by RID as above, Linux later is mostly:

*   Add `runtimes/linux-x64/native/...`
*   Ensure codegen exe invocation chooses correct filename
*   Ensure tool execution permissions:
    *   on Linux you likely need `chmod +x internal_codegen_tool` at build time or ship with executable bit set (NuGet zip may not preserve it reliably across all tooling). A robust trick is: in targets, run `chmod +x` on first use when `$(OS)` is Unix-like.

Also: plan for `RuntimeIdentifier` use:

*   Consumers building “AnyCPU” might not set RID.
*   Decide policy:
    *   either require `RuntimeIdentifier` for codegen (document it)
    *   or infer defaults per OS (`win-x64` on Windows, `linux-x64` on Linux)

I’d recommend:

*   **Windows now**: if `RuntimeIdentifier` empty, default to `win-x64`.
*   Later, once Linux is supported: if empty, default based on OS.

Document this clearly in README.

* * *

5) The dotnet CLI tool package (`YourLib.Cli`)
----------------------------------------------

Since the manual tool is a .NET console app and uses the same native + internal tool:

### Preferred: publish as dotnet tool

*   Package type: `DotNetTool`
*   Multi-RID isn’t necessary if the tool is pure managed, but it depends on native assets, so:
    *   either include native assets in the tool package too, or
    *   make the tool package depend on `YourLib` and locate native assets from that package at runtime

In practice, dotnet tools are installed into a tool cache; locating dependent package native assets is doable but can be annoying.

**Simplest reliable approach**:

*   Make `YourLib.Cli` tool package _self-contained in terms of runtime assets_ (include native dll + internal tool exe alongside tool).
*   Yes, it duplicates files compared to `YourLib`, but it avoids “tool broke because it can’t find native assets”.

If you really want zero duplication, you can do it, but you’ll spend time on probing logic and edge cases.

* * *

6) Versioning + release automation (what to implement)
------------------------------------------------------

### Versioning (single source of truth)

Use **Nerdbank.GitVersioning**:

*   Root: `version.json`
*   Release tags: `vX.Y.Z`
*   CI builds from tags produce exactly `X.Y.Z`

Then for every project:

*   don’t set `<Version>` in each csproj
*   let NBGV inject it
*   optionally centralize common metadata in `Directory.Build.props`:
    *   `PackageId`, `Authors`, `RepositoryUrl`, `PackageLicenseExpression`, `PackageReadmeFile`, `IncludeSymbols`, `SymbolPackageFormat`, `ContinuousIntegrationBuild`, etc.

### “One-command release” workflow

Have a script, e.g. `./build.ps1` (and later `build.sh`) that:

1.  updates submodules
2.  builds native (CMake) for each RID you support
3.  stages native outputs into a known folder used by packing
4.  `dotnet test`
5.  `dotnet pack -c Release`
6.  optionally runs a smoke test consuming the nupkg locally

CI calls the same script.

### GitHub Actions triggers

*   On `push` tag `v*`:
    *   checkout with submodules
    *   setup .NET 8
    *   build native (win)
    *   pack
    *   push to NuGet (api key secret)
    *   create GitHub release and attach `.nupkg` and `.snupkg`

This is the most standard “professional open-source” release posture.

* * *

7) “Professional maintainer” checklist (fast wins)
--------------------------------------------------

*   README:
    *   clearly says: **Windows x64 today; Linux x64 planned**
    *   shows `PackageReference`
    *   notes any required build properties (RuntimeIdentifier policy)
*   Add:
    *   `LICENSE`, `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`
    *   Issue templates + PR template
*   Add CI badges and NuGet badge

* * *

What I’d do next (concrete, no fluff)
-------------------------------------

If you paste (or describe) just these three things, I can give you an exact file/folder + MSBuild target layout that you can drop in:

1.  Your current repo structure (root folders + where the native build outputs land)
2.  How the codegen tool is invoked today (example command line)
3.  Whether you want to require `RuntimeIdentifier` in consuming projects or prefer auto-defaulting to `win-x64` for now

With that, I’ll propose:

*   exact `buildTransitive/YourLib.targets` structure (including “copy exe+dll to output”)
*   packing strategy (either `dotnet pack` + `None Include=... Pack=true` or a `.nuspec`)
*   a GitHub Actions workflow that builds native + packs + publishes reliably.

## Prompt:
Self containef yourlib.cli is ok, native libs and exe are not big. Nerdbank ok, ci build from git tags ok. Auto defaulting to win x64 for now but extendable later.

## Response:
Good — with those decisions, you can implement a very standard, low-drama setup.

Target outcome
--------------

*   **`YourLib` (NuGet library)**
    *   managed assemblies (`net8.0`)
    *   native dll + mandatory native internal exe (RID-packaged)
    *   **buildTransitive targets** that:
        *   default RID to **win-x64** if unspecified (for now)
        *   run codegen before compile
        *   generate into `obj/.../YourLibGenerated`
        *   include generated `.cs` in compilation
        *   copy native dll + internal exe to output for runtime
*   **`YourLib.Cli` (dotnet tool)**
    *   self-contained package (ships same native dll + internal exe)
    *   uses those assets via predictable relative paths
*   **Versioning:** Nerdbank.GitVersioning with Git tags (`vX.Y.Z`) → CI packs and publishes to NuGet.org.

* * *

1) Repo structure (recommended)
-------------------------------

At repo root:

```
/src
  /YourLib/YourLib.csproj
  /YourLib.Other/...
  /YourLib.Cli/YourLib.Cli.csproj
/build
  pack.ps1
  native.ps1
  native.cmake.ps1 (optional)
/native (submodule working tree)
  CMakeLists.txt ...
/artifacts
  /native/win-x64/...
  /native/linux-x64/... (later)
/eng
  /nuget
    YourLib.targets
    YourLib.props (optional)
Directory.Build.props
version.json
```

Key idea: **CMake outputs go into `artifacts/native/<rid>/...`**, and packing consumes from there. This avoids “where did my build put the exe?” pain.

* * *

2) Versioning with Nerdbank.GitVersioning
-----------------------------------------

**`version.json` (root)**: keep it minimal.

*   Your release tags `v1.2.3` produce `1.2.3`
*   non-tag builds become `1.2.4-alpha.g<sha>` (or similar, configurable)

**`Directory.Build.props`**: put package metadata here (not versions).

*   Authors, RepositoryUrl, License, Readme, icon, etc.
*   Also enable SourceLink + symbols.

Nothing project-specific should carry version numbers.

* * *

3) Packing `YourLib`: include native assets + buildTransitive
-------------------------------------------------------------

### In `YourLib.csproj`

*   `TargetFramework: net8.0`
*   `IsPackable: true`
*   Include:
    *   managed assemblies (default)
    *   `buildTransitive/YourLib.targets`
    *   runtime native assets from `artifacts/native/win-x64/*`

**Where to put targets**

*   Put the actual file in repo: `eng/nuget/YourLib.targets`
*   In csproj, include it as:
    *   `Pack="true" PackagePath="buildTransitive/YourLib.targets"`

**Where to put native**

*   `PackagePath="runtimes/win-x64/native/"`

This RID layout is what you’ll extend later with `linux-x64`.

* * *

4) The MSBuild logic (what your `.targets` must do)
---------------------------------------------------

### A) Default `RuntimeIdentifier` to win-x64 (now; extend later)

In targets:

*   If `$(RuntimeIdentifier)` empty → set to `win-x64`
*   Later you can switch to:
    *   Windows → win-x64
    *   Linux → linux-x64

### B) Locate the packaged tools

Use:

*   `$(MSBuildThisFileDirectory)` = directory of the `.targets` inside the package
*   Tools folder becomes:
    *   `$(MSBuildThisFileDirectory)..\runtimes\$(RuntimeIdentifier)\native\`

From there you can execute `internal_codegen_tool.exe` and reference the native DLL.

### C) Generate into `obj`

*   Output folder:
    *   `$(IntermediateOutputPath)YourLibGenerated\`
*   Run codegen **BeforeTargets="CoreCompile"**
*   Then:
    *   `Compile Include="$(IntermediateOutputPath)YourLibGenerated\**\*.cs"`

### D) Copy native dll + internal exe to app output

Add a second target:

*   `AfterTargets="Build"` (or `CopyFilesToOutputDirectory`)
*   `Copy` from that `runtimes/<rid>/native/` folder into `$(OutputPath)`
*   This guarantees the exe is next to the native dll at runtime.

### E) Future incremental build hooks

Even if you run every build now, structure the target so you can later add:

*   `Inputs="@(YourLib_GenInputs)" Outputs="$(IntermediateOutputPath)YourLibGenerated\stamp.txt"`

* * *

5) Packing `YourLib.Cli` as a dotnet tool (self-contained assets)
-----------------------------------------------------------------

In `YourLib.Cli.csproj`:

*   `PackAsTool=true`
*   `ToolCommandName=yourlib` (or similar)
*   Include native + internal exe into the tool package under something like:
    *   `tools/net8.0/any/native/` (or another deterministic folder)
*   At runtime, the CLI resolves its own install directory and loads/copies native assets as needed.

Because you’re ok with duplication, this is straightforward and robust.

* * *

6) Release automation (manual + GitHub Actions)
-----------------------------------------------

### Scripts (recommended)

*   `build/native.ps1`
    *   builds CMake for `win-x64`
    *   outputs into `artifacts/native/win-x64/`
*   `build/pack.ps1`
    *   calls native build
    *   `dotnet restore`
    *   `dotnet build -c Release`
    *   `dotnet test -c Release`
    *   `dotnet pack -c Release -o artifacts/nuget`

### GitHub Actions

Trigger on tag `v*`:

*   checkout with submodules + full history (`fetch-depth: 0`)
*   setup .NET 8
*   run `pwsh build/pack.ps1`
*   `dotnet nuget push artifacts/nuget/*.nupkg` to NuGet.org
*   optionally create GitHub Release and attach packages

This is the standard “tag = release” posture.

* * *

7) NuGet.org publish readiness checklist
----------------------------------------

In `Directory.Build.props` set:

*   `RepositoryUrl`, `RepositoryType=git`
*   `PackageLicenseExpression`
*   `PackageReadmeFile`
*   `IncludeSymbols=true`, `SymbolPackageFormat=snupkg`
*   SourceLink package (Microsoft.SourceLink.GitHub)
*   `ContinuousIntegrationBuild=true` in CI

This makes your packages debuggable and properly attributed.

* * *

8) What I need from you to give you drop-in files (no further guessing)
-----------------------------------------------------------------------

Paste these (small) snippets and I’ll return **exact working** `Directory.Build.props`, `YourLib.targets`, and two `.csproj` pack sections + GH Actions YAML:

1.  **Codegen command line** you run today (example with arguments)
2.  The **native output filenames** you need to ship (dll name, exe name, any extra deps)
3.  Whether consumers are expected to build only on Windows currently (likely yes), or whether CI builds should also pack on Linux later.

If you paste (1) and (2) now, I can write the concrete `.targets` with correct `Exec`, generation dir, and output copying behavior immediately.

## Prompt:
I would alo like to publush the test success indicator on github. The library has multiple xunit test projects.

## Response:
You do this with a **GitHub Actions workflow** that runs `dotnet test` and then surfaces the result in three ways:

1.  **Required PR check** (green/red in PR UI)
2.  **Status badge** in README (“tests passing”)
3.  (Optional but very useful) **published test report artifacts** (TRX) and/or an HTML summary

Below is a solid setup for **multiple xUnit test projects**.

* * *

1) Add a “Tests” GitHub Actions workflow
----------------------------------------

Create: `.github/workflows/tests.yml`

Key points:

*   Run on `push` + `pull_request`
*   Use `dotnet test` once at the solution level (it will discover all test csprojs)
*   Produce TRX results (one file per test project)
*   Upload results as artifacts
*   (Optional) publish a nicer summary in the workflow UI

### Minimal and reliable workflow (Windows for now)

```yaml
name: Tests

on:
  push:
    branches: [ main ]
  pull_request:

jobs:
  test:
    runs-on: windows-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          submodules: recursive
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"

      - name: Restore
        run: dotnet restore

      - name: Test (all)
        run: dotnet test -c Release --no-build --logger "trx" --results-directory "artifacts/testresults"
        # If build is needed in CI, remove --no-build and add a build step.

      - name: Upload test results (TRX)
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: testresults-trx
          path: artifacts/testresults/**/*.trx
```

**Notes**

*   Running `dotnet test` at the solution level is the simplest way to include all xUnit projects.
*   `if: always()` ensures you still get the results uploaded even if tests fail.

* * *

2) Add a “Tests” badge to README
--------------------------------

Once the workflow is in place, add this to `README.md` (replace placeholders):

```md
![Tests](https://github.com/<OWNER>/<REPO>/actions/workflows/tests.yml/badge.svg)
```

If you want the badge to link to the workflow page:

```md
[![Tests](https://github.com/<OWNER>/<REPO>/actions/workflows/tests.yml/badge.svg)](https://github.com/<OWNER>/<REPO>/actions/workflows/tests.yml)
```

This gives you the visible “test success indicator” on GitHub.

* * *

3) Make test success a required gate for PRs
--------------------------------------------

In GitHub repo settings:

**Settings → Branches → Branch protection rules → main**

*   Require status checks to pass before merging
*   Select the check: **Tests / test** (name may appear as `Tests` or `test` depending on UI)

Now you get the canonical green/red check on every PR.

* * *

4) Optional: show a test summary in the GitHub Actions UI (nice UX)
-------------------------------------------------------------------

TRX is great for artifacts, but GitHub’s UI can show a summary if you add a reporter action. A common approach is converting to JUnit, or using an action that reads TRX.

One simple option is: **EnricoMi/publish-unit-test-result-action** (supports many formats; TRX support depends on version/features). Another approach is to output a Markdown summary yourself.

A robust “no extra dependencies” approach is: keep TRX artifacts only (already done). If you want a nicer UI, tell me if you prefer:

*   **JUnit-based summary**, or
*   **TRX-native summary**, or
*   **coverage badges** (Coverlet + ReportGenerator)

* * *

5) If you later add Linux x64
-----------------------------

You can switch to a matrix (Windows now + Linux later):

```yaml
strategy:
  fail-fast: false
  matrix:
    os: [windows-latest, ubuntu-latest]
runs-on: ${{ matrix.os }}
```

…and keep the same `dotnet test` command.

* * *

Quick question (so I don’t steer you wrong)
-------------------------------------------

Do your tests require the **native dll + internal native exe** to exist on disk (integration-ish tests), or are they pure managed unit tests?

*   If they need native assets, we’ll add a step before `dotnet test` to build/stage native outputs (or download cached artifacts) so tests are representative and don’t flake.

## Prompt:
Not pure managed, internal tool used heavily during tests.

## Response:
Got it — if tests rely on the **native DLL + internal native tool EXE**, your CI must **build (or fetch) native artifacts first**, then make them discoverable to the test host.

Below is a proven pattern for Windows x64 now, extendable to Linux later.

* * *

1) Decide the “native discovery contract” for tests
---------------------------------------------------

Pick one (I recommend **A**):

### A) Tests find native bits in a known folder via env var (recommended)

*   CI sets e.g. `YOURLIB_NATIVE_DIR=...`
*   Your managed wrapper (and/or tests) resolves:
    *   native dll path
    *   internal tool exe path  
        from that env var (with a sane fallback for local dev)

This avoids brittle “copy next to every test assembly” logic.

### B) Copy native bits next to each test output

*   CI copies native dll + exe into each test project’s `bin\Release\net8.0\`
*   Works, but gets annoying with multiple test projects and TFMs.

If you already have “tool is located next to the dll” expectations, A still works: you simply stage both in the same folder.

* * *

2) CI workflow: build native → build managed → test → publish indicator
-----------------------------------------------------------------------

Create `.github/workflows/tests.yml`:

```yaml
name: Tests

on:
  push:
    branches: [ main ]
  pull_request:

jobs:
  test:
    runs-on: windows-latest

    env:
      CONFIG: Release
      RID: win-x64
      # where we will stage native outputs for tests
      YOURLIB_NATIVE_DIR: ${{ github.workspace }}\artifacts\native\win-x64

    steps:
      - name: Checkout (with submodules)
        uses: actions/checkout@v4
        with:
          submodules: recursive
          fetch-depth: 0

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"

      - name: Setup CMake
        uses: lukka/get-cmake@latest

      # Optional but recommended: cache native build dir for faster PRs
      - name: Cache native build
        uses: actions/cache@v4
        with:
          path: |
            artifacts\cmake\${{ env.RID }}
          key: native-${{ runner.os }}-${{ env.RID }}-${{ hashFiles('native/**', '**/CMakeLists.txt', '.gitmodules') }}

      - name: Build native (CMake)
        shell: pwsh
        run: |
          # Example structure; adapt paths to your repo.
          $src = Join-Path $env:GITHUB_WORKSPACE "native"
          $bld = Join-Path $env:GITHUB_WORKSPACE "artifacts\cmake\$env:RID"
          $out = $env:YOURLIB_NATIVE_DIR

          New-Item -ItemType Directory -Force -Path $bld | Out-Null
          New-Item -ItemType Directory -Force -Path $out | Out-Null

          cmake -S $src -B $bld -DCMAKE_BUILD_TYPE=$env:CONFIG
          cmake --build $bld --config $env:CONFIG

          # Copy the native dll + internal tool exe into a single staging folder.
          # TODO: replace filenames with your actual outputs.
          Copy-Item -Force "$bld\$env:CONFIG\yourNative.dll" $out
          Copy-Item -Force "$bld\$env:CONFIG\internal_codegen_tool.exe" $out

          # If you have extra deps, copy them too.

      - name: Restore
        run: dotnet restore

      - name: Build managed
        run: dotnet build -c %CONFIG% --no-restore

      - name: Test (all test projects)
        shell: pwsh
        run: |
          # Ensure the internal tool can be found if your code uses PATH probing:
          $env:PATH = "$env:YOURLIB_NATIVE_DIR;$env:PATH"

          dotnet test -c $env:CONFIG --no-build `
            --logger "trx" `
            --results-directory "artifacts/testresults"

      - name: Upload test results (TRX)
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: testresults-trx
          path: artifacts/testresults/**/*.trx

      - name: Upload staged native (debugging aid)
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: native-staging
          path: artifacts/native/win-x64/**
```

### Why this works

*   Native build happens first.
*   Both `yourNative.dll` and `internal_codegen_tool.exe` are staged into one folder (matching your “next to dll” expectation).
*   Tests can resolve them via:
    *   `YOURLIB_NATIVE_DIR` (recommended), and/or
    *   `PATH` (added during test step)

* * *

3) Show the “test success indicator” on GitHub
----------------------------------------------

### A) README badge

Add:

```md
[![Tests](https://github.com/<OWNER>/<REPO>/actions/workflows/tests.yml/badge.svg)](https://github.com/<OWNER>/<REPO>/actions/workflows/tests.yml)
```

### B) Required PR check

Enable branch protection:

*   Settings → Branches → protect `main`
*   Require status checks → select **Tests / test** (the job name appears in the UI)

This gives you the standard green checkmark gating merges.

* * *

4) Make the test host actually use `YOURLIB_NATIVE_DIR` (recommended)
---------------------------------------------------------------------

In your wrapper library (or a small helper used by tests), resolve paths like:

*   If `YOURLIB_NATIVE_DIR` set:
    *   `dllPath = Path.Combine(dir, "yourNative.dll")`
    *   `toolPath = Path.Combine(dir, "internal_codegen_tool.exe")`
*   Else fallback to:
    *   `AppContext.BaseDirectory` (bin folder) or package layout if you have one

This keeps:

*   local dev simple (native bits in output)
*   CI deterministic (explicit staging folder)

* * *

5) Extending to Linux later
---------------------------

When you add Linux:

*   Add another job or matrix:
    *   `ubuntu-latest`, `RID=linux-x64`
*   Build native with the same steps
*   Stage `libyourNative.so` + `internal_codegen_tool` (chmod +x)
*   In the test step:
    *   `export LD_LIBRARY_PATH=$YOURLIB_NATIVE_DIR:$LD_LIBRARY_PATH`
    *   run `dotnet test`

* * *


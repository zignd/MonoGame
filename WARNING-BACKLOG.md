# Warning backlog inventory

This file records the remaining warning-suppression and analyzer-cleanup backlog in this fork.
It is a snapshot, not a policy document. Counts can change as .NET SDK analyzers, target frameworks,
and project references change.

## Snapshot

Date: 2026-07-29

Commands used from the Trace Arena repo root:

```bash
rg -n --hidden \
  --glob '!**/bin/**' \
  --glob '!**/obj/**' \
  --glob '!**/Artifacts/**' \
  --glob '!artifacts/**' \
  --glob '!**/.git/**' \
  --glob '!third_party/MonoGame/native/monogame/external/**' \
  'NoWarn|WarningsNotAsErrors|RunAnalyzers|EnableNETAnalyzers|DisableSpecificWarnings|UnconditionalSuppressMessage|#pragma[[:space:]]+warning[[:space:]]+disable' \
  third_party/MonoGame

dotnet build third_party/MonoGame/MonoGame.Framework.Content.Pipeline/MonoGame.Framework.Content.Pipeline.csproj \
  -p:DisableNativeBuild=True \
  -p:NoWarn= \
  -p:EnableNETAnalyzers=true \
  -p:AnalysisLevel=latest \
  -v:minimal
```

The focused analyzer build clears project `NoWarn`, enables SDK analyzers, and disables the native
pipeline build. It measures the content pipeline project plus its referenced DesktopGL framework
project; it is not a full-fork, every-platform measurement. MSBuild prints each diagnostic once
during compilation and again in its final warning summary, so warning-line counts are twice the
reported MSBuild warning total.

## Headline size

| Area | Current measured size | Notes |
| --- | ---: | --- |
| Explicit suppression lines | 2 | One documented trim boundary and one serializer-test namespace pragma. |
| Focused analyzer warning lines | 0 | Content pipeline project with `NoWarn` cleared and analyzers enabled, plus referenced DesktopGL framework project. |
| Content pipeline share | 0 | Warning lines attributed to `MonoGame.Framework.Content.Pipeline.csproj`. |
| DesktopGL framework share | 0 | Warning lines attributed to `MonoGame.Framework.DesktopGL.csproj` through the project reference. |
| Unique diagnostic IDs in focused build | 0 | The focused production slice is clean. |

## Cleanup checklist

Use this checklist as the live tracker for warning cleanup work. Check an item only when the
suppression or warning family is removed, the corresponding validation has passed, and any package
or platform behavior affected by the change has been inspected.

### Baseline and measurement

- [x] Capture the current explicit suppression inventory.
- [x] Capture a focused no-suppression analyzer build for the content pipeline slice.
- [x] Add a repeatable script or Make target for regenerating this inventory.
- [x] Add a machine-readable warning-count artifact for before/after comparison.

### Package and project suppressions

- [x] Fix or document `NU5128` in `src/NuGetPackages/Directory.Build.props`.
- [x] Fix or document `NU5100` in `MonoGame.Framework.Content.Pipeline.csproj`.
- [x] Migrate off prerelease `System.CommandLine` to stable 2.0.10 and update the parser to its supported API (`NU5104`).
- [x] Decide whether `CS1591` belongs at project scope for the content pipeline package.
- [x] Fix or document `NETSDK1202` in `MonoGame.Content.Builder.Editor.Mac.csproj`.
- [x] Re-enable analyzers for `Tests/Interactive/TestRunners/DesktopGL/MonoGame.DesktopGL.TestRunner.csproj`, or replace the wholesale disable with a narrow documented exception.

### Simple source suppressions

- [x] Remove `CS0649` from `Tools/MonoGame.Content.Builder.Editor/MainWindow.cs`.
- [x] Remove `CA1050` from `Tools/MonoGame.Tools.Tests/AssetTestClasses.cs`, or document why serializer test assets must stay in the global namespace.
- [x] Remove generated parser `CS0168` from `Tools/MonoGame.Effect.Compiler/Effect/TPGParser/Parser.cs` by regenerating or fixing the generator/template.
- [x] Clear `CA2022` inexact reads and `CA2014` loop-local stack allocations.
- [x] Clear `CS0169` dead fields while preserving required FreeType interop layout fields.
- [x] Clear `CS0672`/unsuppressed `CS0618` by aligning obsolete contracts and migrating content-pipeline call sites.
- [x] Re-run `MonoGame.Tools.Tests` after simple source cleanup (319 passed, 3 platform-dependent tests skipped).

### XML documentation

- [x] Decide policy for copied compatibility files under `MonoGame.Framework/Utilities/System.Numerics.Vectors/`.
- [x] Remove file-scope `CS1591` pragmas from copied numerics files, or document copied-source treatment.
- [x] Reduce content pipeline `CS1591` warnings from 370 to 0, or intentionally move any remaining exceptions to a narrow documented scope.
- [x] Verify generated XML documentation still packs correctly for public NuGet packages.

### Globalization analyzers

- [x] Audit `CA1304` warnings and classify each site as invariant, ordinal, or user-culture-sensitive.
- [x] Audit `CA1305` warnings and classify each parse/format site as invariant content format, developer log, or user-visible text.
- [x] Fix invariant content-format sites with explicit `CultureInfo.InvariantCulture`.
- [x] Keep user-visible culture behavior intentional and documented where applicable.
- [x] Clear `CA1304`/`CA1305` in the MGCB editor Linux warning-as-error build.
- [x] Clear `CA1304`/`CA1305` in the effect compiler warning-as-error build.
- [x] Re-run the focused analyzer build and confirm `CA1304`/`CA1305` are cleared or narrowly justified.

Serialized content, command arguments, external-tool protocols, cache data, and machine-readable
paths use invariant or ordinal behavior. User-facing editor and console messages retain the current
culture where values are intended for people rather than round-tripping.

### Nullable annotations

Nullable contracts preserve public signatures where practical, annotate genuinely optional values,
use semantic defaults only when empty is valid, and fail explicitly at required serialization,
reflection, importer, processor, and shader boundaries.

- [x] Establish nullable cleanup rules for public API compatibility, serialization contracts, and content pipeline reflection paths.
- [x] Clean `CS8618` non-nullable initialization warnings.
- [x] Clean `CS8600`, `CS8601`, `CS8602`, `CS8603`, `CS8604`, and `CS8605` flow warnings.
- [x] Clean `CS8620`, `CS8621`, `CS8625`, and `CS8629` nullable argument/value warnings.
- [x] Clean `CS8765`, `CS8767`, `CS8769`, and `CS8714` override/interface/generic nullability mismatches.
- [x] Add or adjust tests for changed null-handling behavior in content pipeline serialization/import paths.

### Trim and AOT annotations

- [x] Decide whether reflection-heavy content loading APIs are supported under trimming/AOT, explicitly unsupported, or supported through annotations/preservation contracts.
- [x] Remove `IL2067` suppressions from design-time type converters, or isolate them behind a documented non-trimmable design-time boundary.
- [x] Remove `IL2072` from `ReflectiveReader<T>` by adding correct annotations or restructuring reflection use.
- [x] Remove `IL2057` from `ContentTypeReaderManager` by replacing or annotating dynamic type lookup.
- [x] Remove `IL2060`/`IL3050` from `ContentManager`, `MultiArrayReader`, and `ReflectionHelpers`, or replace with narrow documented exceptions.
- [x] Re-run the focused analyzer build and confirm `IL20xx`/`IL211x`/`IL3050` warnings are cleared or intentionally scoped.

Content loading now exposes truthful `RequiresUnreferencedCode`/`RequiresDynamicCode` boundaries,
while registered reader factories remain the trimmed-build path. External references use a loader
delegate created inside that boundary. Arbitrary design-time `IPackedVector` conversion remains
supported through one method-level `IL2067` suppression because `TypeConverter.ConvertTo` cannot
annotate its override-supplied destination `Type` with the required constructor preservation.

### Obsolete and platform compatibility

- [x] Replace obsolete ETC1 enum references in `DefaultTextureProfile` with an explicit compatibility value.
- [x] Add analyzer-recognized platform guards for `FontDescriptionProcessor` (`CA1416`).
- [x] Remove ignored Android bitmap options from `Texture2D.OpenGL.cs` (`CA1422`, `CS0618`).
- [x] Remove the ignored Android surface-type call from `MonoGameAndroidGameView.cs`.
- [x] Validate touched platform paths with the relevant backend/platform build or smoke test.

### Final gates

- [x] Focused content-pipeline analyzer build passes with `-p:NoWarn=` and SDK analyzers enabled.
- [x] `MonoGame.Tools.Tests` passes (322 total: 319 passed, 3 skipped).
- [x] The Trace downstream solution builds cleanly and all 914 tests pass against the fork.
- [x] The `netstandard2.1` native framework target builds with the trim/AOT contracts and exact-read behavior intact.
- [x] Touched NuGet packages pack without warnings; content-pipeline XML docs and dependency metadata were inspected.
- [x] Runtime/platform cleanup has backend-specific validation results recorded.
- [x] The Trace `native-runtime-metal-sdl3` prerequisite packs mgfxc, generates Vulkan shaders, and links the native runtime without warnings.
- [x] Re-run the suppression inventory and update the headline counts in this document.

## Focused analyzer diagnostic breakdown

No diagnostics remain in the focused analyzer build. XML documentation, nullable, and trim/AOT
diagnostics are all zero.

The DesktopGL content/editor builds and the `netstandard2.1` native framework build pass, and the
downstream Trace solution builds and passes all tests. The unsuppressed macOS editor build passes
for both `osx-x64` and `osx-arm64`, and the generated Apple Silicon app launches with a populated
MGCB Editor window. The unsuppressed `net10.0-android` framework build passes with zero warnings
using Android API 36 and the workload-provisioned Microsoft OpenJDK 17. A device or emulator was
not required for this compile-time warning validation. The Trace `native-runtime-metal-sdl3`
prerequisite also passes without warnings, including Cake startup, mgfxc pack/publish, Vulkan
shader generation, Premake generation, and the modern macOS linker.

## Top warning hotspots in focused build

There are no warning hotspots in the focused analyzer build.

## Explicit suppression inventory

### Package-level suppressions

| File | Suppression | Notes |
| --- | --- | --- |
| `src/NuGetPackages/Directory.Build.props` | `NU5128` | Fixed: package-only wrappers now pack a conventional `lib/netstandard2.1/_._` placeholder instead of suppressing the warning. |
| `MonoGame.Framework.Content.Pipeline/MonoGame.Framework.Content.Pipeline.csproj` | `CS1591` | Fixed: package-restored `MonoGame.Tool.*` binaries are removed from package content before nuspec generation, so tool payloads such as `dxcompiler.dll` stay package dependencies instead of becoming `contentFiles`; `CS1591` still hides public XML-doc warnings. |
| `Tools/MonoGame.Content.Builder.Editor/MonoGame.Content.Builder.Editor.Mac.csproj` | `NETSDK1202` | Fixed: removed the suppression, raised the deployment floor to the .NET 10 workload minimum of macOS 12.0, and disabled trim analysis for the intentionally reflection-driven, untrimmed editor. The unsuppressed dual-RID build and Apple Silicon launch smoke test pass. |

### Analyzer-disabled project

| File | Suppression | Notes |
| --- | --- | --- |
| `Tests/Interactive/TestRunners/DesktopGL/MonoGame.DesktopGL.TestRunner.csproj` | `RunAnalyzersDuringBuild=false`, `RunAnalyzersDuringLiveAnalysis=false`, `EnableNETAnalyzers=false` | Fixed: analyzers are re-enabled after clearing the runner/shared interactive-test warnings. |

### XML documentation suppressions

| File(s) | Suppression | Notes |
| --- | --- | --- |
| `MonoGame.Framework/Utilities/System.Numerics.Vectors/*.cs` | `CS1591` | Six copied/compatibility numerics files suppress missing XML docs at file scope. Decide whether to keep copied-source treatment or add docs. |
| `MonoGame.Framework.Content.Pipeline/MonoGame.Framework.Content.Pipeline.csproj` | `CS1591` | Clearing this exposed 370 XML-doc warnings in the focused analyzer run. |

### Trim/AOT reflection suppressions

| File(s) | Suppression | Notes |
| --- | --- | --- |
| `MonoGame.Framework/Design/VectorConversion.cs` | `IL2067` | One method-level boundary preserves arbitrary user-defined `IPackedVector` activation; the base `TypeConverter.ConvertTo` contract cannot carry the required destination-type annotation. |
| `MonoGame.Framework/Utilities/ReflectionHelpers.cs` | `IL3050` | Runtime reflection/AOT path. Needs API annotation or non-AOT boundary decision. |
| `MonoGame.Framework/Content/ContentReaders/ReflectiveReader.cs` | `IL2072` | Reflective content reader. High-risk area: fixing may require type annotations or design changes. |
| `MonoGame.Framework/Content/ContentReaders/MultiArrayReader.cs` | `IL3050` | AOT/reflection warning around array creation or generic content reading. |
| `MonoGame.Framework/Content/ContentTypeReaderManager.cs` | `IL2057` | Dynamic type lookup. Needs content pipeline/runtime preservation strategy. |
| `MonoGame.Framework/Content/ContentManager.cs` | `IL2060;IL3050` | Reflection activation / AOT warning. Needs trimming contract decision. |

### Obsolete and platform compatibility suppressions

| File(s) | Suppression | Notes |
| --- | --- | --- |
| `MonoGame.Framework.Content.Pipeline/Graphics/DefaultTextureProfile.cs` | `CS0618` | Fixed: legacy ETC1 input is represented by its stable serialized compatibility value without obsolete member references. |
| `MonoGame.Framework.Content.Pipeline/Processors/FontDescriptionProcessor.cs` | `CA1416` | Fixed: Windows registry and font-directory paths use analyzer-recognized host OS guards. |
| `MonoGame.Framework/Platform/Graphics/Texture2D.OpenGL.cs` | `CA1422;CS0618` | Fixed: removed bitmap decode options that are defaulted or ignored on the supported Android API range. |
| `MonoGame.Framework/Platform/Android/MonoGameAndroidGameView.cs` | `CS0618` | Fixed: removed `SurfaceHolder.SetType`, which is ignored on the supported Android API range. |

### Generated, legacy, or test-only suppressions

| File | Suppression | Notes |
| --- | --- | --- |
| `Tools/MonoGame.Effect.Compiler/Effect/TPGParser/Parser.cs` | `CS0168` | Fixed: removed the broad generated-file pragma and the unused generated `ParseNode n` declarations it was hiding. |
| `Tools/MonoGame.Content.Builder.Editor/MainWindow.cs` | `CS0649` | Fixed: notification fields were converted to events and verified by the Linux editor warning-as-error build. |
| `Tools/MonoGame.Tools.Tests/AssetTestClasses.cs` | `CA1050` | Documented exception: serializer XML fixtures use simple global type names such as `<Asset Type="TheBasics">`; moving helpers into a namespace requires a fixture contract rewrite and verification pass. |

## Practical size estimate

This is not a single refactor. It is several independent cleanup tracks:

| Track | Rough size | Risk |
| --- | --- | --- |
| Package warnings (`NU5128`, `NU5100`, `NETSDK1202`) | Small to medium | Package layout changes can affect NuGet consumers, so validation must include `dotnet pack` and package inspection. |
| Simple source suppressions (`CS0649`, `CA1050`, generated-parser `CS0168`) | Small | Mostly local, except generated parser changes should avoid hand-edit drift. |
| XML docs (`CS1591`) | Medium | Mechanically large. Low runtime risk, but docs should be meaningful for public API. |
| Globalization analyzers (`CA1304`, `CA1305`) | Medium | Many straightforward `InvariantCulture` fixes, but content formats and user-facing culture must be distinguished. |
| Nullable cleanup (`CS86xx`, `CS876x`, `CS8714`) | Large | Around 1,504 warning lines in this slice. Requires API intent decisions, nullable annotations, tests, and avoiding behavior changes. |
| Trim/AOT cleanup (`IL20xx`, `IL3050`, `IL211x`) | Medium to large | Smaller count but higher design risk. Reflection-heavy content/runtime APIs may need annotations, preservation contracts, or explicit non-trimmable boundaries. |
| Obsolete/platform compatibility | Medium | Requires platform-specific replacements and validation on OpenGL, Android, and content-pipeline paths. |

The warning count is large enough that cleanup should be done in topic branches by diagnostic family,
with focused validation per family. The first high-value pass should be package warnings and simple
source suppressions, followed by globalization, then nullable, then trim/AOT.

## Suggested validation gates

For this fork, each cleanup pass should at least run:

```bash
dotnet build MonoGame.Framework.Content.Pipeline/MonoGame.Framework.Content.Pipeline.csproj \
  -p:DisableNativeBuild=True \
  -p:NoWarn= \
  -p:EnableNETAnalyzers=true \
  -p:AnalysisLevel=latest

dotnet test Tools/MonoGame.Tools.Tests/MonoGame.Tools.Tests.csproj
```

Package-layout cleanup should additionally run `dotnet pack` for the touched package projects and
inspect the generated `.nupkg` contents.

Runtime/platform cleanup needs backend-specific validation, not just a content-pipeline build.
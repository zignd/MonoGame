# Removing the Metal Backend's Vulkan SDK Dependency

## Context

The Native Metal backend renders directly to a `CAMetalLayer` through Apple's Metal API. It does not
render through Vulkan or MoltenVK. The Vulkan SDK is currently required when building the backend
because the shader pipeline is shared with the Native Vulkan backend:

- `mgfxc` compiles the stock effects with the Vulkan profile and emits SPIR-V effect blobs.
- The Metal runtime consumes those Vulkan-profile blobs.
- SPIRV-Cross translates SPIR-V to Metal Shading Language (MSL) at runtime.
- The native build gets the SPIRV-Cross headers and static libraries from the Vulkan SDK.
- The macOS `Build Native` workflow also builds `desktopvk`, which needs the SDK's MoltenVK files.

There are therefore two distinct dependencies to address:

1. **Consumer dependency:** an application developer must install the Vulkan SDK before building a
   local Metal runtime.
2. **MonoGame build dependency:** MonoGame's native build agents need the SDK to compile
   `desktopmetal` and `desktopvk`.

Removing the consumer dependency does not necessarily remove the build-infrastructure dependency.

## Glossary

### Graphics APIs and Apple Technologies

- **Graphics API:** The programming interface a renderer uses to submit drawing and compute work to
   a GPU. Metal, Vulkan, and Direct3D 12 are graphics APIs.
- **Metal:** Apple's low-level graphics and compute API for macOS, iOS, and related platforms. The
   `desktopmetal` backend calls Metal directly.
- **Metal Shading Language (MSL):** Apple's C++-based language for writing shader programs consumed
   by Metal. The current backend generates MSL from SPIR-V rather than asking game authors to write it.
- **`CAMetalLayer`:** A macOS/iOS display layer whose textures can be rendered by Metal and presented
   in a window. It is the final display surface used by the direct Metal backend.
- **Metal library (`metallib`):** Apple's compiled container for one or more Metal shaders. It is
   produced by the Xcode `metal` and `metallib` command-line tools and can be loaded without compiling
   MSL source when the game starts.
- **Vulkan:** A cross-platform low-level graphics and compute API. MonoGame's `desktopvk` backend uses
   Vulkan directly on Windows and Linux and through MoltenVK on macOS.
- **MoltenVK:** A compatibility layer that implements Vulkan on top of Metal. It translates Vulkan
   API calls into Metal API calls. The direct `desktopmetal` backend does not use MoltenVK.
- **Direct3D 12 (DX12):** Microsoft's low-level graphics API for Windows and Xbox. It is the standard
   Native backend used by this repository on Windows.
- **Vulkan SDK:** LunarG's development bundle containing Vulkan tools, headers, libraries, MoltenVK,
   and SPIRV-Cross components. Applications do not inherently need this entire SDK to use Metal; the
   current MonoGame source build obtains specific tools and libraries from it.
- **Xcode:** Apple's development environment and SDK bundle. Its command-line tools include the
   offline Metal shader compiler and linker.

### Shaders and Translation

- **Shader:** A small GPU program. Shaders determine operations such as transforming vertices,
   calculating pixel colors, or running general compute work.
- **Shader stage:** The role a shader performs in the graphics pipeline, such as vertex or fragment
   processing. Each stage has its own inputs, outputs, and GPU entry point.
- **HLSL:** Microsoft's High-Level Shading Language. MonoGame effect source files use HLSL-style code
   that `mgfxc` compiles for the selected target profile.
- **SPIR-V:** A binary intermediate representation for GPU programs. It is neither Vulkan commands
   nor native Metal code. Vulkan consumes it directly, while MonoGame's Metal backend translates it
   into MSL.
- **Intermediate representation (IR):** A compiler-friendly format between source code and final
   machine or platform code. SPIR-V is the shared shader IR in the current Vulkan/Metal path.
- **SPIRV-Cross:** A library that reads SPIR-V and emits source code for another shader language. The
   direct Metal backend uses it to produce MSL.
- **Tint:** A shader translation and validation project associated with WebGPU. It can translate
   between selected shader formats, including SPIR-V and MSL in supported configurations.
- **Naga:** The shader translation and validation library used by the `wgpu` ecosystem. It supports
   multiple source and output formats, including MSL.
- **Slang:** A shader language and compiler designed to target multiple graphics APIs and shader
   formats. Adopting it would be a broader compiler/toolchain change than replacing one library.
- **Runtime translation:** Converting SPIR-V to MSL while an application is running. It simplifies
   content sharing but adds runtime work and moves some shader failures to application startup.
- **Offline compilation:** Translating or compiling shaders during the content build rather than when
   the game runs. This improves startup behavior but can add platform-specific build requirements.

### MonoGame Content and Runtime Terms

- **`mgfxc`:** MonoGame's effect compiler. It reads effect source and produces the backend-specific
   data that MonoGame loads at runtime.
- **Effect:** MonoGame's package of shaders, parameters, techniques, passes, and render state used by
   drawing code.
- **Effect profile:** The target selected when compiling an effect, such as Vulkan or DirectX 12. A
   profile controls the generated shader representation and related metadata.
- **Effect blob:** The compiled binary representation of an effect. In the current Metal path, the
   embedded stock-effect blobs contain Vulkan-profile SPIR-V plus MonoGame metadata.
- **Reflection data:** Metadata describing a compiled shader's parameters, textures, samplers, entry
   points, and resource layout. The runtime needs this data to connect MonoGame effect values to GPU
   resources.
- **Resource binding:** The mapping between shader declarations and concrete buffers, textures, and
   samplers supplied by the application. Translators and backends must agree on these mappings.
- **Shader variant:** A separately compiled form of a shader for a different feature combination,
   render mode, or platform condition.
- **Content pipeline:** MonoGame's build system for converting source assets into optimized runtime
   assets. Effects are compiled as part of this process.
- **Stock effects:** Framework-provided effects such as `BasicEffect`, `SpriteEffect`, and
   `SkinnedEffect`. Their compiled headers are embedded directly into `libmgruntime`.
- **Native backend:** The backend implemented by `MonoGame.Framework.Native` and a platform-specific
   `mgruntime` native library. The managed assembly is shared while the native library selects the
   actual graphics and platform implementation.
- **`libmgruntime.dylib`:** The macOS native runtime library containing the platform, graphics, audio,
   and input implementation used by `MonoGame.Framework.Native`.
- **`desktopmetal`:** The Premake project and artifact name for the direct macOS Metal runtime.
- **`desktopvk`:** The Premake project and artifact name for the Vulkan runtime. On macOS it relies on
   MoltenVK to execute Vulkan over Metal.
- **SDL3:** The cross-platform windowing and input library used by the standard Native desktop
   backends. It creates the macOS window and exposes the Metal-compatible display surface.
- **FAudio:** The native audio library used by MonoGame's Native desktop runtime. It is unrelated to
   shader translation but is built into the same `mgruntime` library.

### Build and Distribution Terms

- **Runtime NuGet package:** A .NET package that supplies platform-specific native files during
   restore and build. `MonoGame.Runtime.Mac.Metal` would deliver the prebuilt Metal `mgruntime` so
   application developers would not need to compile it.
- **Source build:** Building the native runtime from repository source instead of consuming a
   published runtime package.
- **Vendoring:** Keeping a copy or Git dependency of third-party source within the project's source
   dependency tree and building it as part of the project.
- **Pinned revision:** A specific third-party commit or release selected to keep builds reproducible.
- **Static library:** A compiled library whose code is copied into another binary at link time.
   SPIRV-Cross is currently linked into `libmgruntime` through static libraries from the Vulkan SDK.
- **Universal macOS binary:** A single Mach-O file containing both Intel `x86_64` and Apple Silicon
   `arm64` code. A runtime NuGet should provide both architectures or clearly separate them by RID.
- **RID:** .NET's Runtime Identifier, such as `osx-arm64`, used to select platform- and
   architecture-specific assets from packages.
- **Premake:** The generator used by MonoGame to create native Makefiles or Visual Studio solutions
   from `premake5.lua`.
- **CI:** Continuous integration automation that builds and tests changes on clean build agents.
   Moving a dependency into CI can remove it from consumer machines without eliminating it entirely.

## How the Current Metal Path Fits Together

```text
Effect source (.fx / HLSL)
            |
            v
mgfxc with the Vulkan effect profile
            |
            v
SPIR-V shader code + reflection metadata in an effect blob
            |
            v
SPIRV-Cross inside libmgruntime, while the game runs
            |
            v
Metal Shading Language source
            |
            v
Apple's Metal runtime compiler
            |
            v
Metal GPU pipeline rendering to a CAMetalLayer
```

The effect compiler produces Vulkan-profile SPIR-V independently of the Metal rendering API. The
verified SDK dependencies are the SPIRV-Cross headers and libraries linked into `desktopmetal` and
the MoltenVK files needed when the same macOS build also produces `desktopvk`. The SDK is not present
between the running direct Metal backend and the Metal API. A prebuilt `MonoGame.Runtime.Mac.Metal`
package can therefore remove the SDK from an application developer's machine without changing this
internal pipeline.

## Alternatives

### 1. Ship `MonoGame.Runtime.Mac.Metal`

Build and publish `libmgruntime.dylib` as a runtime NuGet package, following the existing Windows
DX12 and Linux/macOS Vulkan runtime packages.

**Advantages**

- Removes the Vulkan SDK prerequisite for application developers.
- Provides the same restore-and-run experience as the other Native backends.
- Requires no shader-format or runtime architecture changes.
- Is the lowest-risk path to a usable `mgdesktopmetal` template.

**Disadvantages**

- MonoGame's macOS build agents still require the Vulkan SDK.
- Source builds of the native Metal runtime still require the SDK.
- Package production must support and validate universal macOS binaries.

**Effort:** Low.

### 2. Vendor SPIRV-Cross Source

Pin SPIRV-Cross as a repository dependency and compile the required libraries as part of the native
build instead of consuming them from the Vulkan SDK.

**Advantages**

- Removes the Vulkan SDK dependency from Metal-only source builds.
- Preserves the current SPIR-V effect format and runtime behavior.
- Gives MonoGame explicit control over the translator version.
- Supports both Intel and Apple Silicon from the same source revision.

**Disadvantages**

- MonoGame must maintain updates, patches, build integration, and license notices.
- Runtime SPIR-V translation, shader compilation cost, and related failure modes remain.
- A full macOS Native build still needs MoltenVK for `desktopvk` unless the targets are separated.

**Effort:** Medium.

### 3. Create `MonoGame.Library.SPIRVCross`

Build and distribute pinned SPIRV-Cross static libraries through the same external-library workflow
used by other MonoGame native dependencies.

**Advantages**

- Keeps third-party source and release ownership outside the main repository.
- Provides deterministic, versioned binaries to all build agents.
- Removes the Vulkan SDK dependency from `desktopmetal` while retaining the current shader format.
- Can be reused by other MonoGame tools or platforms.

**Disadvantages**

- Requires a new library repository, CI pipeline, package/version policy, and release process.
- Universal macOS archives and C++ build compatibility must be maintained.
- Debugging prebuilt third-party binaries is less direct than building vendored source.
- The separate `desktopvk` build still needs MoltenVK.

**Effort:** Medium.

### 4. Generate MSL During Content Builds

Add a Metal effect profile to `mgfxc` and translate shaders to MSL while building game content rather
than inside `libmgruntime`.

**Advantages**

- Removes SPIRV-Cross from the shipped runtime.
- Moves translation errors to content-build time.
- Reduces runtime work and allows generated MSL to be cached.
- Establishes Metal as a first-class content target.

**Disadvantages**

- Requires a new effect profile and changes to the effect content format.
- Reflection data, resource binding, shader variants, and compatibility behavior must remain aligned
  with the Vulkan and DX12 profiles.
- MSL source still needs compilation by Metal when the application loads it.
- Projects may need separate Metal content instead of sharing Vulkan-profile content.

**Effort:** High.

### 5. Generate Metal Libraries Offline

Extend the content pipeline to invoke Apple's `metal` and `metallib` tools and package compiled
Metal libraries with game content.

**Advantages**

- Produces the smallest and fastest runtime shader path.
- Moves both translation and Metal compilation failures to build time.
- Uses Apple's native offline shader toolchain.
- Removes SPIRV-Cross from the application runtime.

**Disadvantages**

- Requires Xcode and macOS to build Metal content.
- Makes cross-platform content builds and CI more complicated.
- Requires policies for deployment targets, OS compatibility, shader variants, and cache invalidation.
- Creates the largest divergence from MonoGame's shared desktop content workflow.

**Effort:** High.

### 6. Replace SPIRV-Cross

Use another translator or shader compiler, such as Tint, Naga, or Slang, to produce MSL.

**Advantages**

- May enable a broader, more modern multi-backend shader architecture.
- Could provide better integration with a future intermediate representation or effect compiler.
- Avoids tying the implementation specifically to the Vulkan SDK distribution of SPIRV-Cross.

**Disadvantages**

- Replaces one third-party dependency with another rather than eliminating translation.
- Requires feature, reflection, binding, and generated-code parity work.
- Introduces substantial compatibility and validation risk for existing effects.
- Some candidates bring a significantly larger toolchain or runtime footprint.

**Effort:** High.

### 7. Use MoltenVK Instead of the Direct Metal Backend

Use `desktopvk` on macOS and let MoltenVK translate Vulkan calls to Metal, eliminating the separate
direct Metal graphics implementation.

**Advantages**

- Reuses the Vulkan renderer and existing SPIR-V content path.
- Reduces the amount of backend-specific rendering code to maintain.
- Provides mature Vulkan-to-Metal compatibility.

**Disadvantages**

- Removes the direct Metal backend rather than improving it.
- Retains a MoltenVK dependency and Vulkan abstraction overhead.
- Limits direct access to Metal-specific behavior and diagnostics.
- Conflicts with the goal of making native Metal the standard macOS backend.

**Effort:** Medium.

## Performance Impact

The alternatives affect different parts of the workflow. Packaging and dependency-source choices
change build and distribution cost but do not change the code executed by a game. Moving shader work
offline primarily improves startup time and frame-time consistency; it does not automatically
increase steady-state FPS.

| Alternative | Startup and first use | Steady-state rendering | Build cost | Size and memory |
| --- | --- | --- | --- | --- |
| Runtime NuGet package | Unchanged | Unchanged | Faster consumer builds; package CI does the native work | Adds a package download; shipped runtime is unchanged |
| Pinned SPIRV-Cross source | Unchanged | Unchanged | Slower clean native builds because SPIRV-Cross is compiled locally | Runtime footprint is approximately unchanged |
| `MonoGame.Library.SPIRVCross` | Unchanged | Unchanged | Faster than compiling vendored source; adds library release work | Runtime footprint is approximately unchanged |
| MSL generated during content builds | Faster effect loading because SPIR-V translation is removed | Normally unchanged when generated MSL and compiler options are equivalent | Slower content builds | Smaller native runtime; Metal content may be larger |
| Offline `metallib` generation | Best expected startup and most predictable first use | Normally similar after pipelines exist | Slowest and macOS/Xcode-dependent content builds | Removes runtime translator and MSL source compilation; adds compiled shader assets |
| Replace SPIRV-Cross | Depends on the replacement and where it runs | May improve or regress according to generated MSL quality | Depends on the selected toolchain | Depends on the replacement |
| MoltenVK | Additional Vulkan-to-Metal setup and pipeline work | May add CPU, GPU, synchronization, and memory overhead | Reuses more of the Vulkan build path | Adds the MoltenVK runtime and its state-tracking data |
| `Build Native Metal` task | No runtime effect | No runtime effect | Faster focused source builds because `desktopvk` is skipped | No shipped-size effect |

### Startup and Frame-Time Behavior

The current direct Metal path performs two important operations when an effect or pipeline is first
needed:

1. SPIRV-Cross translates SPIR-V into MSL.
2. Metal compiles the MSL and creates a GPU pipeline state.

Generating MSL during the content build removes the first operation. Shipping a `metallib` removes
both runtime source translation and runtime MSL source compilation, although Metal can still perform
pipeline linking, validation, specialization, and driver-specific setup. Pipeline creation is
therefore not completely free even with precompiled libraries.

These costs matter most when a game loads many effects or shader variants, creates pipelines during
gameplay, or cannot hide pipeline preparation behind a loading screen. A small application such as
the UI catalog creates most of its effects and pipelines near startup, so the visible difference may
be modest. A shader-heavy game can see larger improvements in cold-start time and fewer first-use
frame spikes.

### Steady-State Rendering

After equivalent Metal pipeline states have been created, the runtime package, vendored
SPIRV-Cross, packaged SPIRV-Cross, offline MSL, and offline `metallib` approaches should have similar
steady-state rendering performance. The important factors then become:

- The quality of the generated MSL and Metal compiler optimizations.
- Resource binding and synchronization behavior.
- Pipeline-state caching and reuse.
- CPU submission overhead and GPU workload design.
- Whether shader variants cause additional pipelines to be created during gameplay.

Replacing the translator can affect steady-state performance if it produces materially different
MSL. MoltenVK has a broader performance risk because it translates Vulkan API behavior and tracks
Vulkan state in addition to handling shaders. Its actual cost depends on the workload and must be
measured rather than assumed.

### Expected Ordering

For startup and first-use pipeline preparation, the expected order from least to most runtime work is:

1. Offline `metallib`.
2. Offline MSL.
3. Current direct Metal with runtime SPIRV-Cross.
4. Vulkan through MoltenVK.

The first three may produce nearly identical steady-state FPS. Their clearest differences are likely
to appear in cold-start duration and frame-time spikes when effects or pipelines are first used.

### Benchmark Requirements

Before selecting an offline shader architecture for performance reasons, measure each candidate on
Intel and Apple Silicon using the same effects and render workload:

- Cold application launch to first presented frame.
- Total effect-load and pipeline-creation time.
- First-use frame time for every shader variant.
- Median, 95th-percentile, and 99th-percentile frame time after warm-up.
- CPU render-thread time and GPU execution time.
- Peak memory usage.
- Native runtime size and compiled content size.
- Warm launch behavior with Metal and operating-system caches populated.

Performance should be evaluated alongside portability and maintenance cost. Offline `metallib`
generation offers the lowest expected runtime shader cost, but it also imposes the strongest macOS
and Xcode requirements on content production.

## Professional Engine Precedents

Professional engines generally do not choose exclusively between runtime compilation and offline
compilation. Their development workflows preserve iteration speed and broad shader compatibility,
while production builds move expensive shader work into platform-specific cooking steps and use
pipeline caches or prewarming to avoid first-use stalls.

| Engine | Development workflow | Production Metal workflow | Runtime strategy |
| --- | --- | --- | --- |
| Unity | Supports incremental and lazy shader compilation in the editor | Processes and strips platform shader variants during player builds | Uses shader prewarming and platform pipeline caching to reduce first-use stalls |
| Unreal Engine | Compiles shaders incrementally for editor iteration | Cooks platform-specific shader libraries and pipeline data | Uses Pipeline State Object precaching and caches for predictable runtime behavior |
| Godot | Can retain portable SPIR-V and compile Metal libraries lazily or asynchronously | Can bake Metal shaders into `.metallib` data when the Apple toolchain is available | Supports precompiled libraries plus shader and pipeline caches, with fallback paths |

The exact implementation, configuration, and behavior vary by engine version and target platform.
The common architectural pattern is more important than any one private or version-specific detail:

1. Keep a portable or source-level shader representation for editor iteration and fallback.
2. Discover and strip unused shader variants during platform builds.
3. Produce platform-specific shader artifacts during cooking when possible.
4. Pre-create, prewarm, or cache pipeline states that would otherwise cause gameplay stalls.
5. Retain runtime compilation as a controlled fallback for dynamic or previously unseen variants.

### Unity

Unity's build pipeline selects and processes shader variants for the target platform. Variant
stripping is important because keywords can create very large combinations of shader programs.
Projects can identify important variants and warm them before gameplay, trading loading time and
build size for more predictable frame times.

For MonoGame, the relevant lessons are:

- Compile Metal-specific artifacts during a macOS build rather than on every consumer machine.
- Add explicit variant discovery and stripping before adopting large offline shader libraries.
- Offer pipeline prewarming for effects known to be used in latency-sensitive scenes.

### Unreal Engine

Unreal performs extensive shader work during platform cooking and packages target-specific shader
libraries. Its Pipeline State Object workflows collect, precompile, and cache combinations of
shaders and fixed-function render state. This shifts substantial cost into build and loading stages
to reduce runtime discovery and compilation stalls.

For MonoGame, the relevant lessons are:

- Treat a pipeline as more than a shader: render-target formats, blend state, depth state, vertex
   layout, and other fixed-function choices also determine Metal pipeline creation.
- Record or declare expected pipeline combinations, not only individual effects.
- Version caches by backend, device/OS compatibility, shader content, and pipeline description.

### Godot

Godot's direct Metal backend is the closest public implementation to the proposed MonoGame path. In
the local Godot source tree it:

- Vendors SPIRV-Cross under `thirdparty/spirv-cross`.
- Translates SPIR-V to MSL for the direct Metal backend.
- Supports building `.metallib` binary data when a compatible Apple toolchain is available.
- Supports lazy, asynchronous, and precompiled `MTLLibrary` loading paths.
- Maintains Metal shader and pipeline cache infrastructure.
- Retains Vulkan through MoltenVK as a separate macOS rendering path.

This demonstrates that portable SPIR-V, direct Metal, optional offline `metallib` generation, and
runtime fallback can coexist rather than requiring a single irreversible shader pipeline.

### Recommended Professional-Engine Model for MonoGame

MonoGame should follow a hybrid model closest to Godot's public architecture, supplemented by the
variant management and pipeline prewarming practices common in Unity and Unreal:

1. Vendor or independently package a pinned SPIRV-Cross build.
2. Keep runtime SPIR-V-to-MSL translation as the development and compatibility fallback.
3. Add a Metal effect profile capable of storing MSL or precompiled `metallib` data.
4. Make offline `metallib` generation an opt-in release-build feature when Xcode is available.
5. Add asynchronous Metal library and pipeline creation for artifacts not prepared offline.
6. Add persistent pipeline caches and an API for applications to prewarm known pipelines.
7. Add shader-variant reporting and stripping before encouraging broad offline compilation.
8. Publish `MonoGame.Runtime.Mac.Metal` so consumers receive the runtime without native toolchains.

This model provides fast editor iteration, a source-build fallback, and the lowest practical release
runtime cost without forcing every cross-platform content build to run on macOS.

## Supporting Build Change

Add a focused `Build Native Metal` task that generates Vulkan-profile stock shaders and builds only
`desktopmetal`. The current macOS Native build generates both `desktopmetal` and `desktopvk`, so it
continues to require MoltenVK even if SPIRV-Cross is supplied independently.

This task is useful with either vendored or packaged SPIRV-Cross and should:

- Build `mgfxc` and the Vulkan-profile stock-effect headers.
- Build SDL3 and FAudio for the host architecture.
- Build only the `desktopmetal` Premake target.
- Avoid validating or linking MoltenVK.
- Produce `Artifacts/native/mgruntime/desktopmetal/macosx/Release/libmgruntime.dylib`.

## Recommended Path

### Phase 1: Remove the Consumer Prerequisite

1. Add and publish `MonoGame.Runtime.Mac.Metal`.
2. Add the package to the standard macOS template and catalog.
3. Validate restore, build, launch, and Metal backend detection on Intel and Apple Silicon.

This immediately allows most developers to use Metal without installing the Vulkan SDK.

### Phase 2: Isolate the Metal Source Build

1. Add `Build Native Metal`.
2. Add a pinned SPIRV-Cross submodule or create `MonoGame.Library.SPIRVCross`.
3. Remove Vulkan SDK paths from the `metal()` Premake configuration.
4. Keep the SDK requirement only on the `desktopvk` build path.

Vendoring is simpler for initial development. A dedicated library package is preferable if MonoGame
wants all large native dependencies to use independently versioned build infrastructure.

### Phase 3: Evaluate an Offline Metal Content Profile

Prototype MSL generation in `mgfxc` after the direct Metal backend and runtime package are stable.
Adopt it only if reduced runtime translation cost and first-class Metal content justify maintaining a
separate effect profile. Offline `metallib` generation should remain optional because it requires a
macOS/Xcode content-build environment.

## Decision Summary

The best near-term solution is to publish the Metal runtime package, add a Metal-only native build,
and supply SPIRV-Cross independently of the Vulkan SDK. This removes the SDK from normal application
development without redesigning the effect format. A dedicated MSL content pipeline is a longer-term
architecture investment rather than a prerequisite for shipping the direct Metal backend.
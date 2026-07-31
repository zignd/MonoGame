# MonoGame UI Catalog

A Storybook-style MonoGame application for exploring every concrete public control in `Microsoft.Xna.Framework.UI`.

The left pane searches the live component inventory, the center renders the selected component, and the right pane creates property editors for safe writable values such as booleans, numbers, strings, enums, and colors.

The project references the local Native framework and automatically selects the standard backend for
the current host:

| Host | Graphics backend | Native artifact |
| --- | --- | --- |
| Windows | Direct3D 12 | `windowsdx` |
| Linux | Vulkan | `desktopvk` |
| macOS | Metal | `desktopmetal` |

## Run

Run these commands from the MonoGame repository root. Ensure the native dependencies are initialized:

```sh
git submodule update --init --recursive
```

On macOS, build only the direct Metal runtime:

```sh
dotnet run --project build/Build.csproj -- --target="Build Native Metal"
```

This target compiles the pinned SPIRV-Cross submodule and Vulkan-profile shader content used as
Metal's portable input. It does not build the Vulkan runtime or MoltenVK and does not require the
Vulkan SDK. Xcode and its command-line tools are required for the native Metal build.

On Windows or Linux, build the standard host runtime with `--target="Build Native"` instead.

Then launch the catalog:

```sh
dotnet run --project Tools/MonoGame.UI.Catalog/MonoGame.UI.Catalog.csproj
```

For a bounded, machine-readable shader pipeline capture, provide an output path and frame count:

```sh
dotnet run --project Tools/MonoGame.UI.Catalog/MonoGame.UI.Catalog.csproj -- \
	--metrics Artifacts/shader-pipeline-baseline/catalog.json --frames 120
```

The catalog exits after the requested number of rendered frames. The versioned JSON report records the
host, selected backend, frame count, shader and pipeline creation counts and timings, cache hits and
misses, and Metal runtime translation counts and timings. Use the same frame count and a release Native
runtime when comparing captures.

Pass `--watch-effect <mgfxo>` to dogfood development hot reload with a SpriteEffect-compatible compiled
output. The catalog watches the explicit build output, reads changes off-thread, swaps the effect on the
render thread, retains the last valid effect on failure, and includes reload status in metrics JSON.

After the native runtime has been built, only the catalog command is needed for subsequent runs.
When running from the parent workspace directory, prefix both project paths with `MonoGame/`.

Metal uses the Vulkan effect profile and translates its SPIR-V effects to MSL at runtime. The catalog
build fails with the expected artifact path when the selected runtime has not been built. See
[BACKEND-DEPENDENCIES.md](../../BACKEND-DEPENDENCIES.md) for the backend architecture.
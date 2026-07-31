# Shader Pipeline Benchmark

This Native sample renders 16 `BasicEffect` feature combinations per frame to exercise shader variant
selection, native pipeline creation, warm pipeline-cache reuse, and Metal SPIR-V-to-MSL translation.
It writes a versioned diagnostics report and exits deterministically.

From the repository root, build the Native runtime and run a 120-frame capture:

```sh
dotnet run --project build/Build.csproj -- --target="Build Native"
dotnet run --project Tools/MonoGame.ShaderPipeline.Benchmark/MonoGame.ShaderPipeline.Benchmark.csproj -- \
  --output Artifacts/shader-pipeline-baseline/shader-benchmark.json --frames 120 \
  --pipeline-cache Artifacts/shader-pipeline-baseline/native-pipeline.cache
```

The project selects Direct3D 12 on Windows, Vulkan on Linux, and Metal on macOS. Compare reports only
when the OS, architecture, backend, Native build configuration, workload, and frame count match.
The optional pipeline-cache path is imported before prewarming and atomically replaced after the run.
DX12 and Vulkan persist backend-native data; Metal reports `Unsupported` and continues without a blob.

To dogfood render-thread-safe hot reload, point the benchmark at a SpriteEffect-compatible compiled
MGFX output. The benchmark watches that explicit build output, loads bytes off-thread, swaps the effect
on the render thread, retains the last valid effect after failures, and records reload status in JSON:

```sh
dotnet run --project Tools/MonoGame.Effect.Compiler/MonoGame.Effect.Compiler.csproj -- \
  MonoGame.Framework/Platform/Graphics/Effect/Resources/SpriteEffect.fx \
  Artifacts/shader-pipeline-baseline/SpriteEffect.vk.mgfxo /Profile:Vulkan

dotnet run --project Tools/MonoGame.ShaderPipeline.Benchmark/MonoGame.ShaderPipeline.Benchmark.csproj -- \
  --output Artifacts/shader-pipeline-baseline/hot-reload.json --frames 120 \
  --watch-effect Artifacts/shader-pipeline-baseline/SpriteEffect.vk.mgfxo
```

Add `--reload-count 100` to run a repeated render-loop swap and retirement stress capture. The process
continues past the requested minimum frame count until every requested reload succeeds or fails, and the
JSON report records both totals.
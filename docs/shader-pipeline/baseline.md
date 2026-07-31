# Shader Pipeline Baseline

## Reproduction Contract

The baseline lab uses the current repository revision, a release Native runtime, no debugger, no frame
capture tools, and a fixed frame count. Record the OS, architecture, backend, toolchain revision, and
command with every capture. Run each workload once to warm process-level system caches, then retain the
second run. The JSON files under `Artifacts/shader-pipeline-baseline/` are intentionally ignored build
outputs; CI should publish them as job artifacts.

```sh
dotnet run --project build/Build.csproj -- --target="Build Native"

dotnet run --project Tools/MonoGame.UI.Catalog/MonoGame.UI.Catalog.csproj -- \
  --metrics Artifacts/shader-pipeline-baseline/catalog.json --frames 120

dotnet run --project Tools/MonoGame.ShaderPipeline.Benchmark/MonoGame.ShaderPipeline.Benchmark.csproj -- \
  --output Artifacts/shader-pipeline-baseline/shader-benchmark.json --frames 120 \
  --pipeline-cache Artifacts/shader-pipeline-baseline/native-pipeline.cache
```

The same commands select Direct3D 12 on Windows, Vulkan on Linux, and Metal on macOS. Platform owners
must capture the matching report on a documented lab machine or CI runner before accepting a backend
performance regression threshold.

Compare compatible captures with a relative budget (20 percent by default):

```sh
python3 scripts/check-shader-pipeline-regression.py \
  Artifacts/shader-pipeline-baseline/baseline.json \
  Artifacts/shader-pipeline-baseline/candidate.json \
  --max-regression-percent 20
```

The check rejects reports from different workloads, backends, architectures, or frame counts before
comparing launch, frame percentile, compilation, pipeline, cache, memory, runtime, and content metrics.

## Initial Metal Capture

Captured on 2026-07-30 with macOS 26.5.2 on Arm64 and the Metal Native backend.

| Workload | Frames | Shaders | Shader ms | Pipeline hits/misses | Pipelines | Pipeline ms | Translations | Translation ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| UI catalog | 10 | 32 | 1107.33 | 869 / 1 | 1 | 19.51 | 32 | 7.95 |
| BasicEffect 16-variant matrix | 120 | 30 | 12.33 | 1910 / 10 | 10 | 123.65 | 30 | 8.09 |

A 100-reload Metal stress capture completed in 120 rendered frames with 100 successful swaps, zero
failed swaps, and no runtime or GPU lifetime errors. Reproduce it with:

```sh
dotnet run --project Tools/MonoGame.ShaderPipeline.Benchmark/MonoGame.ShaderPipeline.Benchmark.csproj -- \
  --output Artifacts/shader-pipeline-baseline/hot-reload-stress.json --frames 120 \
  --watch-effect Artifacts/shader-pipeline-baseline/SpriteEffect.vk.mgfxo --reload-count 100
```

The first capture establishes instrumentation and reproducibility, not pass/fail thresholds. Thresholds
belong to the later benchmark workstream after equivalent Windows and Linux captures exist.
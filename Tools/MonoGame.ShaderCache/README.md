# MonoGame Shader Cache Tool

`mgshadercache` operates on the persistent cache produced by `EffectProcessor`. The processor defaults
to `<intermediate>/mgshadercache`; set its `CacheDirectory`, `CacheSizeMegabytes`, or `EnableCache`
processor parameters to override that behavior.

```sh
dotnet run --project Tools/MonoGame.ShaderCache/MonoGame.ShaderCache.csproj -- inspect obj/Content/mgshadercache
dotnet run --project Tools/MonoGame.ShaderCache/MonoGame.ShaderCache.csproj -- inspect obj/Content/mgshadercache --json
dotnet run --project Tools/MonoGame.ShaderCache/MonoGame.ShaderCache.csproj -- validate obj/Content/mgshadercache
dotnet run --project Tools/MonoGame.ShaderCache/MonoGame.ShaderCache.csproj -- trim obj/Content/mgshadercache --max-size-mb 512
dotnet run --project Tools/MonoGame.ShaderCache/MonoGame.ShaderCache.csproj -- clear obj/Content/mgshadercache
dotnet run --project Tools/MonoGame.ShaderCache/MonoGame.ShaderCache.csproj -- manifest validate Artifacts/shader-pipeline-baseline/phase4-recording.pipelines.json
dotnet run --project Tools/MonoGame.ShaderCache/MonoGame.ShaderCache.csproj -- manifest merge merged.pipelines.json first.pipelines.json second.pipelines.json
```

Cache keys include normalized root and transitive include contents, profile, debug mode, defines, and
compiler identity. Entries are integrity checked, written by atomic replacement, and evicted by least
recent access time. A corrupt or incompatible entry becomes a cache miss and is removed automatically.

Pipeline manifests contain stable backend-neutral descriptions, named priority groups, and their
recorded or declared source. Manifest merge is deterministic and rejects unsupported schemas or
descriptions whose computed identity does not match their key.
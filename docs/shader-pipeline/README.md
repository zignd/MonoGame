# Shader Pipeline Contracts

These version-1 schemas define the stable interchange contracts used by the hybrid shader pipeline:

- `shader-artifact.schema.json`: all content and tool inputs to one compiled shader stage.
- `pipeline-description.schema.json`: backend-neutral state required to identify a graphics pipeline.
- `cache-metadata.schema.json`: environment identity required to accept persistent backend cache data.
- `usage-manifest.schema.json`: observed, declared, framework-required, or retained pipeline keys.

The corresponding managed models and canonical SHA-256 key implementation live in
`MonoGame.Framework.Content.Pipeline/Graphics/ShaderPipelineContracts.cs`.

## Canonicalization

- Normalize text to Unicode NFC before hashing.
- Normalize source paths to `/` separators and make them relative to the compilation root.
- Hash normalized source and include contents with SHA-256; timestamps are never correctness inputs.
- Sort source records by normalized path and content hash using ordinal comparison.
- Sort define, shader-stage, and other map keys using ordinal comparison.
- Encode strings as a 32-bit byte length followed by UTF-8 bytes to avoid concatenation ambiguity.
- Include the schema version in every canonical hash.

## Invalidation

| Changed identity | Compiler artifact | Translation | Native library | Pipeline object | Persistent cache | Manifest |
| --- | --- | --- | --- | --- | --- | --- |
| Source, include, define, profile, entry point, compiler, optimization, target | Rebuild | Rebuild | Rebuild | Rebuild | Reject dependent entries | Keep usage; resolve new keys |
| SPIRV-Cross version or MSL options | Keep SPIR-V | Rebuild | Rebuild | Rebuild | Reject dependent entries | Keep usage; resolve new keys |
| `metal`/`metallib` or deployment target | Keep SPIR-V/MSL | Keep MSL | Rebuild | Rebuild | Reject dependent entries | Keep usage; resolve new keys |
| Pipeline state or shader artifact key | Keep | Keep | Keep | Rebuild | Reject dependent entries | Replace pipeline key |
| Backend, device, driver, OS, content set, compiler, schema | Keep portable layers | Backend-specific | Backend-specific | Rebuild | Reject entire incompatible cache | Revalidate keys |

Cache layers are stored independently. Rejecting a native pipeline cache must not erase valid compiler,
translation, or native-library artifacts. Unknown schema versions and unknown required properties fail
closed with an actionable diagnostic; they are never interpreted as version 1.

## Review Rules

Any schema change requires a version increment, stable-hash tests, migration notes, and compatibility
tests against the previous readable artifact version. Additive optional diagnostic fields may remain in
the same schema version only when they do not affect canonical identity or cache acceptance.
#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifact_dir="$repo_root/Artifacts/WarningBaseline"
warning_log="$artifact_dir/focused-build.log"
suppression_log="$artifact_dir/suppressions.txt"
diagnostic_counts="$artifact_dir/diagnostic-counts.txt"
baseline_json="$repo_root/WARNING-BASELINE.json"

mkdir -p "$artifact_dir"

(
  cd "$repo_root"
  rg -n --hidden \
    --glob '!**/bin/**' \
    --glob '!**/obj/**' \
    --glob '!**/Artifacts/**' \
    --glob '!**/.git/**' \
    --glob '!native/monogame/external/**' \
    --glob '!WARNING-BACKLOG.md' \
    --glob '!scripts/measure-warning-baseline.sh' \
    'NoWarn|WarningsNotAsErrors|RunAnalyzers|EnableNETAnalyzers|DisableSpecificWarnings|\[UnconditionalSuppressMessage\(|#pragma[[:space:]]+warning[[:space:]]+disable' \
    .
) > "$suppression_log" || true

if ! dotnet build "$repo_root/MonoGame.Framework.Content.Pipeline/MonoGame.Framework.Content.Pipeline.csproj" \
  -t:Rebuild \
  -p:DisableNativeBuild=True \
  -p:NoWarn= \
  -p:EnableNETAnalyzers=true \
  -p:AnalysisLevel=latest \
  -v:minimal > "$warning_log" 2>&1; then
  cat "$warning_log"
  exit 1
fi

{ rg -o 'warning [A-Z]+[0-9]+' "$warning_log" || true; } \
  | sed 's/warning //' \
  | sort \
  | uniq -c \
  | sort -nr \
  | awk '{ print $1, $2 }' > "$diagnostic_counts"

warning_lines="$(awk '{ total += $1 } END { print total + 0 }' "$diagnostic_counts")"
unique_diagnostics="$(wc -l < "$diagnostic_counts" | tr -d ' ')"
suppression_lines="$(wc -l < "$suppression_log" | tr -d ' ')"
content_pipeline_lines="$(rg -c '\[.*/MonoGame.Framework.Content.Pipeline/MonoGame.Framework.Content.Pipeline.csproj\]$' "$warning_log" || true)"
desktop_gl_lines="$(rg -c '\[.*/MonoGame.Framework/MonoGame.Framework.DesktopGL.csproj\]$' "$warning_log" || true)"
generated_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"

{
  printf '{\n'
  printf '  "generatedAtUtc": "%s",\n' "$generated_at"
  printf '  "explicitSuppressionLines": %s,\n' "$suppression_lines"
  printf '  "focusedBuildWarningLines": %s,\n' "$warning_lines"
  printf '  "contentPipelineWarningLines": %s,\n' "${content_pipeline_lines:-0}"
  printf '  "desktopGLWarningLines": %s,\n' "${desktop_gl_lines:-0}"
  printf '  "uniqueDiagnosticIds": %s,\n' "$unique_diagnostics"
  printf '  "diagnostics": {\n'

  first=true
  while read -r count diagnostic; do
    if [[ "$first" == true ]]; then
      first=false
    else
      printf ',\n'
    fi
    printf '    "%s": %s' "$diagnostic" "$count"
  done < "$diagnostic_counts"

  printf '\n  }\n'
  printf '}\n'
} > "$baseline_json"

printf 'Warning baseline: %s warning lines across %s diagnostics\n' "$warning_lines" "$unique_diagnostics"
printf 'Suppression inventory: %s lines\n' "$suppression_lines"
printf 'JSON summary: %s\n' "$baseline_json"
printf 'Detailed artifacts: %s\n' "$artifact_dir"
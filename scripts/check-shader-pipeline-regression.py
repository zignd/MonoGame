#!/usr/bin/env python3

import argparse
import json
import sys
from pathlib import Path


IDENTITY_FIELDS = ("workload", "backend", "hostArchitecture", "renderedFrames")
DEFAULT_METRICS = (
    "launchToFirstDrawMilliseconds",
    "frameTimeP50Milliseconds",
    "frameTimeP95Milliseconds",
    "frameTimeP99Milliseconds",
    "shaderCreationMilliseconds",
    "pipelineCreationMilliseconds",
    "runtimeTranslationMilliseconds",
    "prewarmMilliseconds",
    "pipelineCacheMisses",
    "peakManagedMemoryBytes",
    "nativeRuntimeBytes",
    "watchedEffectBytes",
)


def evaluate(baseline, candidate, max_regression_percent, metrics=DEFAULT_METRICS):
    errors = []
    results = []
    for field in IDENTITY_FIELDS:
        if baseline.get(field) != candidate.get(field):
            errors.append(
                f"Incompatible reports: {field} is {baseline.get(field)!r} in the baseline "
                f"and {candidate.get(field)!r} in the candidate."
            )

    for metric in metrics:
        baseline_value = baseline.get(metric)
        candidate_value = candidate.get(metric)
        if not isinstance(baseline_value, (int, float)) or not isinstance(candidate_value, (int, float)):
            errors.append(f"Metric {metric} must be numeric in both reports.")
            continue
        limit = baseline_value * (1 + max_regression_percent / 100)
        passed = candidate_value <= limit
        results.append(
            {
                "metric": metric,
                "baseline": baseline_value,
                "candidate": candidate_value,
                "limit": limit,
                "passed": passed,
            }
        )
        if not passed:
            errors.append(
                f"{metric} regressed from {baseline_value:g} to {candidate_value:g}; "
                f"the {max_regression_percent:g}% limit is {limit:g}."
            )

    return {"passed": not errors, "metrics": results, "errors": errors}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Compare compatible shader pipeline benchmark reports.")
    parser.add_argument("baseline", type=Path)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("--max-regression-percent", type=float, default=20.0)
    parser.add_argument("--metric", action="append", dest="metrics")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args(argv)
    if args.max_regression_percent < 0:
        parser.error("--max-regression-percent must be non-negative")

    baseline = json.loads(args.baseline.read_text(encoding="utf-8"))
    candidate = json.loads(args.candidate.read_text(encoding="utf-8"))
    result = evaluate(
        baseline,
        candidate,
        args.max_regression_percent,
        tuple(args.metrics) if args.metrics else DEFAULT_METRICS,
    )
    if args.json:
        print(json.dumps(result, indent=2))
    elif result["passed"]:
        print(f"Shader pipeline regression check passed for {len(result['metrics'])} metrics.")
    else:
        for error in result["errors"]:
            print(f"error: {error}", file=sys.stderr)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

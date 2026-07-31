import importlib.util
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "check-shader-pipeline-regression.py"
SPEC = importlib.util.spec_from_file_location("check_shader_pipeline_regression", SCRIPT_PATH)
REGRESSION = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(REGRESSION)


class ShaderPipelineRegressionTests(unittest.TestCase):
    def report(self, value=10.0, backend="Metal"):
        return {
            "workload": "BasicEffect-16-variant-matrix",
            "backend": backend,
            "hostArchitecture": "Arm64",
            "renderedFrames": 120,
            "launchToFirstDrawMilliseconds": value,
            "frameTimeP50Milliseconds": value,
            "frameTimeP95Milliseconds": value,
            "frameTimeP99Milliseconds": value,
            "shaderCreationMilliseconds": value,
            "pipelineCreationMilliseconds": value,
            "runtimeTranslationMilliseconds": value,
            "prewarmMilliseconds": value,
            "pipelineCacheMisses": value,
            "peakManagedMemoryBytes": value,
            "nativeRuntimeBytes": value,
            "watchedEffectBytes": value,
        }

    def test_accepts_metrics_within_relative_budget(self):
        result = REGRESSION.evaluate(self.report(), self.report(12.0), 20.0)

        self.assertTrue(result["passed"])
        self.assertEqual([], result["errors"])

    def test_rejects_metric_over_relative_budget(self):
        result = REGRESSION.evaluate(self.report(), self.report(12.1), 20.0)

        self.assertFalse(result["passed"])
        self.assertTrue(any("shaderCreationMilliseconds regressed" in error for error in result["errors"]))

    def test_rejects_incompatible_reports(self):
        result = REGRESSION.evaluate(self.report(), self.report(backend="Vulkan"), 20.0)

        self.assertFalse(result["passed"])
        self.assertIn("backend", result["errors"][0])


if __name__ == "__main__":
    unittest.main()

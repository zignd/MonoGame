using MonoGame.ShaderPipeline.Benchmark;

var options = BenchmarkOptions.Parse(args);
using var game = new ShaderPipelineBenchmarkGame(options);
game.Run();
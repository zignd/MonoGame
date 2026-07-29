using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content.Pipeline;
using NUnit.Framework;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using System.IO;
using MonoGame.Effect;
#if DIRECTX
using TwoMGFX;
#endif

namespace MonoGame.Tests.ContentPipeline
{
    [Category("Effects")]
    class EffectProcessorTests
    {
        class ImporterContext : ContentImporterContext
        {
            public override string IntermediateDirectory
            {
                get { throw new NotImplementedException(); }
            }

            public override ContentBuildLogger Logger
            {
                get { throw new NotImplementedException(); }
            }

            public override string OutputDirectory
            {
                get { throw new NotImplementedException(); }
            }

            public override void AddDependency(string filename)
            {
                throw new NotImplementedException();
            }
        }

#if DIRECTX
        [Test]
        public void TestPreprocessor()
        {
            var effectFile = "Assets/Effects/PreprocessorTest.fx";
            var effectCode = File.ReadAllText(effectFile);
            var fullPath = Path.GetFullPath(effectFile);

            // Preprocess.
            var mgDependencies = new List<string>();
            var mgPreprocessed = Preprocessor.Preprocess(effectCode, fullPath, new Dictionary<string, string>
            {
                { "TEST2", "1" }
            }, mgDependencies, new TestEffectCompilerOutput());

            Assert.That(mgDependencies, Has.Count.EqualTo(1));
            Assert.That(Path.GetFileName(mgDependencies[0]), Is.EqualTo("PreprocessorInclude.fxh"));

            Assert.That(mgPreprocessed, Does.Not.Contain("Foo"));
            Assert.That(mgPreprocessed, Does.Contain("Bar"));
            Assert.That(mgPreprocessed, Does.Not.Contain("Baz"));

            Assert.That(mgPreprocessed, Does.Contain("FOO"));
            Assert.That(mgPreprocessed, Does.Not.Contain("BAR"));

            // Check that we can actually compile this file.
            BuildEffect(effectFile, TargetPlatform.Windows);
        }
#endif

        [Test]
        public void PreprocessorResolvesIncludesAndTracksDependencies()
        {
            using var tempDirectory = new TemporaryDirectory();
            var includePath = tempDirectory.WriteFile("Common.fxh", "#define INCLUDED_VALUE 42\nfloat Included() { return INCLUDED_VALUE; }\n");
            var effectPath = tempDirectory.WriteFile("Test.fx", "#include \"Common.fxh\"\nfloat Main() { return Included(); }\n");

            var dependencies = new List<string>();
            var result = PreprocessFile(effectPath, new Dictionary<string, string>(), dependencies);

            Assert.That(result, Does.Contain("float Included() { return 42; }"));
            Assert.That(result, Does.Contain("float Main() { return Included(); }"));
            Assert.That(dependencies, Is.EquivalentTo(new[] { Path.GetFullPath(includePath) }));
        }

        [Test]
        public void PreprocessorAppliesObjectFunctionAndTokenPasteMacros()
        {
            using var tempDirectory = new TemporaryDirectory();
            var effectPath = tempDirectory.WriteFile("Test.fx", @"
#define VALUE 7
#define MAKE_SAMPLER(Name, index) sampler Name##Sampler : register(s##index)
#define SAMPLE(Name, uv) Name.Sample(Name##Sampler, uv)
MAKE_SAMPLER(Diffuse, 3);
float4 Main(float2 uv) { return SAMPLE(Diffuse, uv) + VALUE; }
");

            var result = PreprocessFile(effectPath);

            Assert.That(result, Does.Contain("sampler DiffuseSampler : register(s3);"));
            Assert.That(result, Does.Contain("Diffuse.Sample(DiffuseSampler, uv)"));
            Assert.That(result, Does.Contain("+ 7"));
            Assert.That(result, Does.Not.Contain("MAKE_SAMPLER"));
            Assert.That(result, Does.Not.Contain("SAMPLE("));
        }

        [Test]
        public void PreprocessorEvaluatesConditionalDirectivesAndDefinedExpressions()
        {
            using var tempDirectory = new TemporaryDirectory();
            var effectPath = tempDirectory.WriteFile("Test.fx", @"
#if defined(USE_FIRST)
float First;
#elif SELECTED == 2
float Second;
#else
float Fallback;
#endif

#ifdef ENABLE_EXTRA
float Extra;
#endif
");

            var result = PreprocessFile(effectPath, new Dictionary<string, string>
            {
                { "SELECTED", "2" },
                { "ENABLE_EXTRA", "1" }
            });

            Assert.That(result, Does.Not.Contain("float First;"));
            Assert.That(result, Does.Contain("float Second;"));
            Assert.That(result, Does.Not.Contain("float Fallback;"));
            Assert.That(result, Does.Contain("float Extra;"));
        }

        [Test]
        public void PreprocessorPreservesLinePragmaAndCommentLineBreaks()
        {
            using var tempDirectory = new TemporaryDirectory();
            var effectPath = tempDirectory.WriteFile("Test.fx", "float A;\n/* one\n two\n three */\n#line 100 \"custom.fx\"\n#pragma pack_matrix(row_major)\nfloat B;\n");

            var result = PreprocessFile(effectPath);

            Assert.That(result, Does.Contain("#line 100 \"custom.fx\""));
            Assert.That(result, Does.Contain("#pragma pack_matrix(row_major)"));
            Assert.That(result, Does.Contain("float A;\n\n\n"));
            Assert.That(result, Does.Contain("float B;"));
        }

        private class TestEffectCompilerOutput : IEffectCompilerOutput
        {
            public void WriteWarning(string file, int line, int column, string message)
            {
                Console.WriteLine("Warning: {0}({1},{2}): {3}", file, line, column, message);
            }

            public void WriteError(string file, int line, int column, string message)
            {
                Console.WriteLine("Error: {0}({1},{2}): {3}", file, line, column, message);
            }
        }

        private static string PreprocessFile(string path)
        {
            return PreprocessFile(path, new Dictionary<string, string>(), new List<string>());
        }

        private static string PreprocessFile(string path, IDictionary<string, string> defines)
        {
            return PreprocessFile(path, defines, new List<string>());
        }

        private static string PreprocessFile(string path, IDictionary<string, string> defines, List<string> dependencies)
        {
            var fullPath = Path.GetFullPath(path);
            return Preprocessor.Preprocess(File.ReadAllText(fullPath), fullPath, defines, dependencies, new TestEffectCompilerOutput());
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            private readonly string _path;

            public TemporaryDirectory()
            {
                _path = Path.Combine(Path.GetTempPath(), "MonoGameEffectPreprocessorTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_path);
            }

            public string WriteFile(string fileName, string contents)
            {
                var path = Path.Combine(_path, fileName);
                File.WriteAllText(path, contents.Replace("\r\n", "\n"));
                return path;
            }

            public void Dispose()
            {
                if (Directory.Exists(_path))
                    Directory.Delete(_path, true);
            }
        }

        [Test]
        [TestCase("Assets/Effects/ParserTest.fx")]
        public void TestParser(string effectFile)
        {
            BuildEffect(effectFile, TargetPlatform.DesktopGL);
        }

        [Test]
        public void TestDefines()
        {
            Assert.DoesNotThrow(() => BuildEffect("Assets/Effects/DefinesTest.fx", TargetPlatform.DesktopGL, "MACRO_DEFINE_TEST=3"));
            Assert.Throws<InvalidContentException>(() =>
                BuildEffect("Assets/Effects/DefinesTest.fx", TargetPlatform.DesktopGL, "MACRO_DEFINE_TEST=4"));
            Assert.Throws<InvalidContentException>(() =>
                BuildEffect("Assets/Effects/DefinesTest.fx", TargetPlatform.DesktopGL));
            Assert.Throws<InvalidContentException>(() =>
                BuildEffect("Assets/Effects/DefinesTest.fx", TargetPlatform.DesktopGL, "INVALID_SYNTAX;ANOTHER_MACRO;MACRO_DEFINE_TEST=3"));
        }

        [Test]
        [TestCase("Assets/Effects/Stock/AlphaTestEffect.fx")]
        [TestCase("Assets/Effects/Stock/BasicEffect.fx")]
        [TestCase("Assets/Effects/Stock/DualTextureEffect.fx")]
        [TestCase("Assets/Effects/Stock/EnvironmentMapEffect.fx")]
        [TestCase("Assets/Effects/Stock/SkinnedEffect.fx")]
        [TestCase("Assets/Effects/Stock/SpriteEffect.fx")]
        public void BuildStockEffect(string effectFile)
        {
#if DIRECTX
            BuildEffect(effectFile, TargetPlatform.Windows);
            BuildEffect(effectFile, TargetPlatform.WindowsGDK);
#endif
            BuildEffect(effectFile, TargetPlatform.DesktopGL);
            BuildEffect(effectFile, TargetPlatform.DesktopVK);
        }

        private void BuildEffect(string effectFile, TargetPlatform targetPlatform, string defines = null)
        {
            var importerContext = new ImporterContext();
            var importer = new EffectImporter();
            var input = importer.Import(effectFile, importerContext);

            Assert.NotNull(input);

            var processorContext = new TestProcessorContext(targetPlatform, Path.ChangeExtension(effectFile, ".xnb"));
            var processor = new EffectProcessor { Defines = defines };
            var output = processor.Process(input, processorContext);

            Assert.NotNull(output);

            // TODO: Should we test the writer?
        }
    }
}

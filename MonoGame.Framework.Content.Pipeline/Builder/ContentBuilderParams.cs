// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Framework.Content.Pipeline.Builder.Server;

namespace MonoGame.Framework.Content.Pipeline.Builder;

/// <summary>
/// A list of arguments used by <see cref="ContentBuilder"/>.
/// <para>Use <see cref="ContentBuilderParams.Parse"/> to acquire the arguments from the passed cli args.</para>
/// </summary>
public class ContentBuilderParams
{
    class RootOptions
    {
        private readonly ContentBuilderParams _defaultValues = new();
        private readonly Option<string> _workingDirectory;
        private readonly Option<string> _sourceDirectory;
        private readonly Option<string> _outputDirectory;
        private readonly Option<string> _intermediateDirectory;
        private readonly Option<TargetPlatform> _platform;
        private readonly Option<GraphicsProfile> _graphicsProfile;
        private readonly Option<bool> _compressContent;
        private readonly Option<LogLevel> _logLevel;

        public RootOptions(RootCommand rootCommand)
        {
            _workingDirectory = AddOption(rootCommand, "--workingDir", "The working directory of the content builder.", _defaultValues.WorkingDirectory);
            _sourceDirectory = AddOption(rootCommand, "--src", "The source asset directory.", _defaultValues.SourceDirectory, "-s");
            _outputDirectory = AddOption(rootCommand, "--output", "The output content directory.", _defaultValues.OutputDirectory, "-o");
            _intermediateDirectory = AddOption(rootCommand, "--intermediate", "The intermediate content directory.", _defaultValues.IntermediateDirectory, "-i");
            _platform = AddOption(rootCommand, "--platform", "The content target platform.", _defaultValues.Platform, "-p");
            _graphicsProfile = AddOption(rootCommand, "--graphics-profile", "The content graphics profile.", _defaultValues.GraphicsProfile, "-g");
            _compressContent = AddOption(rootCommand, "--compress", "Compress the build content files.", _defaultValues.CompressContent);
            _logLevel = AddOption(rootCommand, "--loglevel", "The log level of messages that get outputed to the console.", _defaultValues.LogLevel, "-l");
        }

        public ContentBuilderParams Apply(ParseResult parseResult)
        {
            var workingDirectory = parseResult.GetValue(_workingDirectory) ?? _defaultValues.WorkingDirectory;
            return new ContentBuilderParams
            {
                WorkingDirectory = workingDirectory,
                SourceDirectory = MakeRelative(workingDirectory, parseResult.GetValue(_sourceDirectory) ?? _defaultValues.SourceDirectory),
                OutputDirectory = MakeRelative(workingDirectory, parseResult.GetValue(_outputDirectory) ?? _defaultValues.OutputDirectory),
                IntermediateDirectory = MakeRelative(workingDirectory, parseResult.GetValue(_intermediateDirectory) ?? _defaultValues.IntermediateDirectory),
                Platform = parseResult.GetValue(_platform),
                GraphicsProfile = parseResult.GetValue(_graphicsProfile),
                CompressContent = parseResult.GetValue(_compressContent),
                LogLevel = parseResult.GetValue(_logLevel)
            };
        }

        private static Option<T> AddOption<T>(RootCommand rootCommand, string name, string description, T defaultValue, params string[] aliases)
        {
            var option = new Option<T>(name, aliases)
            {
                Description = description,
                Recursive = true,
                DefaultValueFactory = _ => defaultValue
            };
            rootCommand.Add(option);
            return option;
        }
    }

    class ServerOptions
    {
        private readonly List<ContentServer> _contentServers = [];
        private readonly List<(Type, PropertyInfo, Option)> _options = [];

        public ServerOptions(Command command)
        {
            foreach (var serverType in ContentBuilderHelper.GetServerTypes())
            {
                var contentServer = (ContentServer)Activator.CreateInstance(serverType)!;
                foreach (var (attribute, propertyInfo) in ContentBuilderHelper.GetServerProperties(serverType))
                {
                    var optionType = typeof(Option<>).MakeGenericType(propertyInfo.PropertyType);
                    var option = (Option)Activator.CreateInstance(optionType, new object[] {
                        "--" + attribute.Name,
                        Array.Empty<string>()
                    })!;
                    option.Description = attribute.Description;
                    command.Add(option);

                    _options.Add((serverType, propertyInfo, option));
                }
                _contentServers.Add(contentServer);
            }
        }

        public List<ContentServer> Apply(ParseResult parseResult)
        {
            foreach (var (type, propertyInfo, option) in _options)
            {
                var optionResult = parseResult.GetResult(option);
                if (optionResult == null)
                    continue;

                var getValue = typeof(OptionResult)
                    .GetMethod(nameof(OptionResult.GetValueOrDefault), Type.EmptyTypes)!
                    .MakeGenericMethod(propertyInfo.PropertyType);
                var value = getValue.Invoke(optionResult, null);
                var server = _contentServers.Find(server => server.GetType() == type);
                propertyInfo.SetValue(server, value);
            }

            return _contentServers;
        }
    }

    /// <summary>
    /// Set the mode in which the content builder is run in. See <see cref="ContentBuilderMode"/> for available modes.
    /// </summary>
    /// <value><see cref="ContentBuilderMode.None"/> by default.</value>
    public ContentBuilderMode Mode { get; set; } = ContentBuilderMode.None;

    /// <summary>
    /// Gets or sets the working directory of the <see cref="ContentBuilder"/>.
    /// </summary>
    /// <value><see cref="Directory.GetCurrentDirectory"/> by default.</value>
    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// Gets or sets the location of the content relative to the <see cref="WorkingDirectory"/>.
    /// </summary>
    /// <value><c>Content</c> by default.</value>
    public string SourceDirectory { get; set; } = "Content";

    /// <summary>
    /// Gets the rooted location of <see cref="SourceDirectory"/>.
    /// </summary>
    public string RootedSourceDirectory => MakeRooted(SourceDirectory);

    /// <summary>
    /// Gets or sets the location for the content output relative to the <see cref="WorkingDirectory"/>.
    /// </summary>
    /// <value><c>bin/Content</c> by default.</value>
    public string OutputDirectory { get; set; } = "bin/Content";

    /// <summary>
    /// Gets the rooted location of <see cref="OutputDirectory"/>.
    /// </summary>
    public string RootedOutputDirectory => MakeRooted(OutputDirectory);

    /// <summary>
    /// Gets or sets the location for the intermediate files for content build relative to the <see cref="WorkingDirectory"/>.
    /// </summary>
    /// <value><c>obj/Content</c> by default.</value>
    public string IntermediateDirectory { get; set; } = "obj/Content";

    /// <summary>
    /// Gets the rooted location of <see cref="IntermediateDirectory"/>.
    /// </summary>
    public string RootedIntermediateDirectory => MakeRooted(IntermediateDirectory);

    /// <summary>
    /// Gets or sets the desired platform for <see cref="ContentBuilder"/> to build the content for.
    /// </summary>
    /// <value><see cref="TargetPlatform.DesktopGL"/> by default.</value>
    public TargetPlatform Platform { get; set; } = TargetPlatform.DesktopGL;

    /// <summary>
    /// Gets or sets the desired graphics profile for <see cref="ContentBuilder"/> to build the content for.
    /// </summary>
    /// <value><see cref="GraphicsProfile.HiDef"/> by default.</value>
    public GraphicsProfile GraphicsProfile { get; set; } = GraphicsProfile.HiDef;

    /// <summary>
    /// Gets or sets if <see cref="ContentBuilder"/> should compress each built content file.
    /// </summary>
    /// <value><c>false</c> by default.</value>
    public bool CompressContent { get; set; } = false;

    /// <summary>
    /// Gets or sets the logging level of information that <see cref="ContentBuilder"/> will display to console.
    /// </summary>
    /// <value><see cref="LogLevel.Info"/> by default.</value>
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Should the <see cref="ContentBuilder"/> rebuild all the assets and ignore the content cache.
    /// </summary>
    /// <value><c>false</c> by default.</value>
    public bool Rebuild { get; set; } = false;

    /// <summary>
    /// Should the <see cref="ContentBuilder"/> skip cleaning up old content cache data after the build is finished in <see cref="ContentBuilderMode.Builder"/> mode.
    /// </summary>
    /// <value><c>false</c> by default.</value>
    public bool SkipClean { get; set; } = false;

    /// <summary>
    /// A list of servers to start up when the <see cref="Mode"/> is set to <see cref="ContentBuilderMode.Server"/>.
    /// </summary>
    /// <value>A collection of <see cref="ContentServer"/> classes found by scaning all referenced assemblies.</value>
    public List<ContentServer> Servers { get; set; } = []; // TODO: Fix command line display

    /// <summary>
    /// Parses out the main entry point args into a <see cref="ContentBuilderParams"/> to be used by <see cref="ContentBuilder"/>.
    /// </summary>
    /// <param name="args">Arguments passed to the main entry point of the app.</param>
    /// <returns>
    /// <see cref="ContentBuilderParams"/> containing the parsed arguments, or an empty <see cref="ContentBuilderParams"/> if no arguments were passed.
    /// </returns>
    public static ContentBuilderParams Parse(params string[] args)
    {
        var ret = new ContentBuilderParams();
        if (args == null || args.Length == 0 || (args.Length == 1 && string.IsNullOrEmpty(args[0])))
            return ret;

        var defaultValues = new ContentBuilderParams();
        var rootCommand = new RootCommand("Content builder and conntent server for MonoGame.");
        var rootOptions = new RootOptions(rootCommand);

        var buildCommand = new Command("build", "Build all the content.");
        var rebuildOption = new Option<bool>("--rebuild")
        {
            Description = "Should the builder rebuild all the assets and ignore the content cache.",
            DefaultValueFactory = _ => defaultValues.Rebuild
        };
        buildCommand.Add(rebuildOption);
        var skipCleanOption = new Option<bool>("--skip-clean")
        {
            Description = "Should the builder skip cleaning up old content cache data after the build is finished.",
            DefaultValueFactory = _ => defaultValues.SkipClean
        };
        buildCommand.Add(skipCleanOption);
        buildCommand.SetAction(
            parseResult =>
            {
                ret = rootOptions.Apply(parseResult);
                ret.Mode = ContentBuilderMode.Builder;
                ret.Rebuild = parseResult.GetValue(rebuildOption);
                ret.SkipClean = parseResult.GetValue(skipCleanOption);
            });
        rootCommand.Add(buildCommand);

        var serverCommand = new Command("server", "Start a content server.");
        var serverOptions = new ServerOptions(serverCommand);
        serverCommand.SetAction(
            parseResult =>
            {
                ret = rootOptions.Apply(parseResult);
                ret.Mode = ContentBuilderMode.Server;
                ret.Servers = serverOptions.Apply(parseResult);
            });
        rootCommand.Add(serverCommand);

        rootCommand.Parse(args).Invoke();

        return ret;
    }

    private string MakeRooted(string path)
    {
        if (!Path.IsPathRooted(path))
            path = Path.Combine(WorkingDirectory, path);

        return Path.GetFullPath(path);
    }

    private static string MakeRelative(string workingDir, string path)
    {
        if (!Path.IsPathRooted(path))
            return FileHelper.NormalizeDirectorySeparators(path);

        // Note this may still return an absolute path in the case
        // that these directories are on different drives.
        return Path.GetRelativePath(workingDir, path);
    }
}

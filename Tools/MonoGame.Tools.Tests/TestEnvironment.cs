using System;
using System.IO;

namespace MonoGame.Tests;

internal static class TestEnvironment
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string GetRepositoryPath(params string[] segments)
    {
        var path = RepositoryRoot;
        foreach (var segment in segments)
            path = Path.Combine(path, segment);
        return path;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Build.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the MonoGame repository root.");
    }
}
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content.Pipeline;

namespace MonoGame.Tests.ContentPipeline
{
    class TestContentBuildLogger : ContentBuildLogger
    {
        public List<string> Messages { get; } = new();

        public override void Log(LogLevel level, string message)
        {
            Messages.Add(message);
        }
    }
}

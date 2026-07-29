// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Diagnostics;

namespace MonoGame.Effect.Compiler.Effect.Spirv
{
    // https://registry.khronos.org/SPIR-V/specs/unified1/SPIRV.html#OpTypeRuntimeArray
    internal class SpirvTypeRuntimeArray : SpirvTypeBase
    {
        public override SpirvType Type => SpirvType.RuntimeArray;
        public SpirvTypeBase ElementType { get; private set; } = null!;

        protected override void ParseArgs(string[] args, SpirvReflectionInfo.SpirvParseContext context)
        {
            if (!context.Types.TryGetValue(args[0], out SpirvTypeBase? type))
                throw new InvalidOperationException($"OpTypeRuntimeArray {Name ?? Id} referenced unknown element type '{args[0]}'.");

            ElementType = type;
        }
    }
}

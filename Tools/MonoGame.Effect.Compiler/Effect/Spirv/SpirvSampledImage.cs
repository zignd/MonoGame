// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using MonoGame.Effect.Compiler.Effect.Spirv.Types;
using System.Diagnostics;

namespace MonoGame.Effect.Compiler.Effect.Spirv
{
    internal class SpirvSampledImage
    {
        public string Id { get; }
        public SpirvLoad LoadedSampler { get; }
        public SpirvLoad LoadedImage { get; }
        public SpirvTypeSampledImage SampleType { get; }

        private SpirvSampledImage(string id, SpirvLoad loadedSampler, SpirvLoad loadedImage, SpirvTypeSampledImage sampleType)
        {
            Id = id;
            LoadedSampler = loadedSampler;
            LoadedImage = loadedImage;
            SampleType = sampleType;
        }

        internal static SpirvSampledImage? ParseSampledImage(string[] parts, SpirvReflectionInfo.SpirvParseContext context)
        {
            string id = parts[0];

            if (!context.Types.TryGetValue(parts[3], out SpirvTypeBase? spirvTypeBase))
            {
                Debug.WriteLine($"OpSampledImage {id} referenced unparsed type {parts[3]}");
                return null;
            }
            else if (spirvTypeBase is not SpirvTypeSampledImage)
            {
                Debug.WriteLine($"OpSampledImage {id} referenced type {spirvTypeBase.Name ?? spirvTypeBase.Id} that is not a SampledImage type");
                return null;
            }

            if (!context.Loads.TryGetValue(parts[4], out SpirvLoad? imageLoad))
            {
                Debug.WriteLine($"OpSampledImage {id} referenced unparsed image load {parts[4]}");
                return null;
            }

            if (!context.Loads.TryGetValue(parts[5], out SpirvLoad? sampledLoad))
            {
                Debug.WriteLine($"OpSampledImage {id} referenced unparsed sampler load {parts[5]}");
                return null;
            }

            return new SpirvSampledImage(id, sampledLoad, imageLoad, (SpirvTypeSampledImage)spirvTypeBase);
        }
    }
}

// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;

namespace Microsoft.Xna.Framework.Graphics
{

    // TODO: We should convert the types below
    // into the start of a Shader reflection API.

    internal enum SamplerType
    {
        Sampler2D = 0,
        SamplerCube = 1,
        SamplerVolume = 2,
        Sampler1D = 3,
    }

    internal struct SamplerInfo
    {
        public SamplerType type;
        public int textureSlot;
        public int samplerSlot;
        public string name;
		public SamplerState state;

        // TODO: This should be moved to EffectPass.
        public int parameter;
    }

    internal struct VertexAttribute
    {
        public VertexElementUsage usage;
        public int index;

        // These should be obsolete once we move away from
        // the old C# graphics backends to the native ones.
        public string name;
        public int location;

        public string ToShaderSemantic()
        {
            switch (usage)
            {
                case VertexElementUsage.Position:
                    return "POSITION" + index;
                case VertexElementUsage.Color:
                    return "COLOR" + index;
                case VertexElementUsage.Normal:
                    return "NORMAL" + index;
                case VertexElementUsage.TextureCoordinate:
                    return "TEXCOORD" + index;
                case VertexElementUsage.BlendIndices:
                    return "BLENDINDICES" + index;
                case VertexElementUsage.BlendWeight:
                    return "BLENDWEIGHT" + index;
                case VertexElementUsage.Binormal:
                    return "BINORMAL" + index;
                case VertexElementUsage.Tangent:
                    return "TANGENT" + index;
                case VertexElementUsage.PointSize:
                    return "PSIZE" + index;
                case VertexElementUsage.Depth:
                    return "DEPTH" + index;
                case VertexElementUsage.Fog:
                    return "FOG" + index;
                case VertexElementUsage.Sample: // Huh?  What is this?
                    return "SAMPLE" + index;
                case VertexElementUsage.TessellateFactor:
                    return "TESSELLATEFACTOR" + index;
                default:
                    throw new NotSupportedException("Unknown vertex element usage!");
            }
        }
    }

    internal partial class Shader : GraphicsResource
	{
        /// <summary>
        /// Returns the platform specific shader profile identifier.
        /// </summary>
        public static int Profile { get { return PlatformProfile(); } }

        /// <summary>
        /// A hash value which can be used to compare shaders.
        /// </summary>
        internal int HashKey { get; private set; }

        internal string ArtifactKey { get; private set; }

        public SamplerInfo[] Samplers { get; private set; }

	    public int[] CBuffers { get; private set; }

        public ShaderStage Stage { get; private set; }

        public VertexAttribute[] Attributes { get; private set; }

        public string Entrypoint { get; private set; }

        public string SourceFile { get; private set; }

        internal Shader(GraphicsDevice device, int version, BinaryReader reader)
        {
            GraphicsDevice = device;

            var isVertexShader = reader.ReadBoolean();
            Stage = isVertexShader ? ShaderStage.Vertex : ShaderStage.Pixel;

            if (version > 10)
            {
                // We don't really need these at runtime unless there is an
                // error with the shader and this information is very useful.
                SourceFile = reader.ReadString();
                Entrypoint = reader.ReadString();
            }
            else
            {
                SourceFile = "<unknown>";
                Entrypoint = "<unknown>";
            }

            var shaderLength = reader.ReadInt32();
            var shaderBytecode = reader.ReadBytes(shaderLength);
            ArtifactKey = ComputeArtifactKey(Stage, shaderBytecode);

            var samplerCount = (int)reader.ReadByte();
            Samplers = new SamplerInfo[samplerCount];
            for (var s = 0; s < samplerCount; s++)
            {
                Samplers[s].type = (SamplerType)reader.ReadByte();
                Samplers[s].textureSlot = reader.ReadByte();
                Samplers[s].samplerSlot = reader.ReadByte();

				if (reader.ReadBoolean())
				{
					Samplers[s].state = new SamplerState();
					Samplers[s].state.AddressU = (TextureAddressMode)reader.ReadByte();
					Samplers[s].state.AddressV = (TextureAddressMode)reader.ReadByte();
					Samplers[s].state.AddressW = (TextureAddressMode)reader.ReadByte();
                    Samplers[s].state.BorderColor = new Color(
                        reader.ReadByte(),
                        reader.ReadByte(),
                        reader.ReadByte(),
                        reader.ReadByte());
					Samplers[s].state.Filter = (TextureFilter)reader.ReadByte();
					Samplers[s].state.MaxAnisotropy = reader.ReadInt32();
					Samplers[s].state.MaxMipLevel = reader.ReadInt32();
					Samplers[s].state.MipMapLevelOfDetailBias = reader.ReadSingle();
				}

                Samplers[s].name = reader.ReadString();
                Samplers[s].parameter = reader.ReadByte();
            }

            var cbufferCount = (int)reader.ReadByte();
            CBuffers = new int[cbufferCount];
            for (var c = 0; c < cbufferCount; c++)
                CBuffers[c] = reader.ReadByte();

            var attributeCount = (int)reader.ReadByte();
            Attributes = new VertexAttribute[attributeCount];
            for (var a = 0; a < attributeCount; a++)
            {
                Attributes[a].name = reader.ReadString();
                Attributes[a].usage = (VertexElementUsage)reader.ReadByte();
                Attributes[a].index = reader.ReadByte();
                Attributes[a].location = reader.ReadInt16();
            }

            PlatformConstruct(Stage, shaderBytecode);
        }

        internal static string ComputeArtifactKey(ShaderStage stage, byte[] shaderBytecode)
        {
            const uint preparedMetalMagic = 0x4C4D474D;
            const uint preparedMetalVersion = 1;
            const int footerSize = 28;
            var identityLength = shaderBytecode.Length;
            if (shaderBytecode.Length >= footerSize)
            {
                var footer = shaderBytecode.AsSpan(shaderBytecode.Length - footerSize);
                var libraryLength = BinaryPrimitives.ReadInt32LittleEndian(footer[16..]);
                var entryPointLength = BinaryPrimitives.ReadInt32LittleEndian(footer[20..]);
                var metadataLength = BinaryPrimitives.ReadInt32LittleEndian(footer[24..]);
                var extensionLength = (long)libraryLength + entryPointLength + metadataLength + footerSize;
                if (BinaryPrimitives.ReadUInt32LittleEndian(footer) == preparedMetalMagic &&
                    BinaryPrimitives.ReadUInt32LittleEndian(footer[4..]) == preparedMetalVersion &&
                    libraryLength > 0 && entryPointLength > 0 && metadataLength >= 0 &&
                    extensionLength < shaderBytecode.Length)
                    identityLength -= checked((int)extensionLength);
            }

            var identityBytes = new byte[identityLength + 1];
            identityBytes[0] = (byte)stage;
            Buffer.BlockCopy(shaderBytecode, 0, identityBytes, 1, identityLength);
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(identityBytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        internal protected override void GraphicsDeviceResetting()
        {
            PlatformGraphicsDeviceResetting();
        }
	}
}


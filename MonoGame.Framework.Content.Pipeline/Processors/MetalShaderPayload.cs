// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Buffers.Binary;
using System.Text;

namespace Microsoft.Xna.Framework.Content.Pipeline.Processors;

internal static class MetalShaderPayload
{
    internal const uint Magic = 0x4C4D474D;
    internal const uint Version = 1;
    internal const uint AllowRuntimeFallback = 1;
    internal const int FooterSize = 28;

    public static byte[] Append(
        byte[] shaderCode,
        int spirvLength,
        byte[] library,
        string entryPoint,
        string compatibilityMetadata,
        bool allowRuntimeFallback)
    {
        ArgumentNullException.ThrowIfNull(shaderCode);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentException.ThrowIfNullOrEmpty(entryPoint);
        ArgumentNullException.ThrowIfNull(compatibilityMetadata);
        if (spirvLength <= 0 || spirvLength > shaderCode.Length || (spirvLength & 3) != 0)
            throw new ArgumentOutOfRangeException(nameof(spirvLength));
        if (library.Length == 0)
            throw new ArgumentException("The compiled Metal library is empty.", nameof(library));

        var entryPointBytes = Encoding.UTF8.GetBytes(entryPoint);
        var metadataBytes = Encoding.UTF8.GetBytes(compatibilityMetadata);
        var result = new byte[checked(shaderCode.Length + library.Length + entryPointBytes.Length + metadataBytes.Length + FooterSize)];
        var offset = 0;
        shaderCode.CopyTo(result, offset);
        offset += shaderCode.Length;
        library.CopyTo(result, offset);
        offset += library.Length;
        entryPointBytes.CopyTo(result, offset);
        offset += entryPointBytes.Length;
        metadataBytes.CopyTo(result, offset);
        offset += metadataBytes.Length;

        var footer = result.AsSpan(offset, FooterSize);
        BinaryPrimitives.WriteUInt32LittleEndian(footer, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(footer[4..], Version);
        BinaryPrimitives.WriteUInt32LittleEndian(footer[8..], allowRuntimeFallback ? AllowRuntimeFallback : 0);
        BinaryPrimitives.WriteInt32LittleEndian(footer[12..], spirvLength);
        BinaryPrimitives.WriteInt32LittleEndian(footer[16..], library.Length);
        BinaryPrimitives.WriteInt32LittleEndian(footer[20..], entryPointBytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(footer[24..], metadataBytes.Length);
        return result;
    }

    public static bool TryRead(
        byte[] payload,
        out int spirvLength,
        out byte[] library,
        out string entryPoint,
        out string compatibilityMetadata,
        out bool allowRuntimeFallback)
    {
        spirvLength = 0;
        library = [];
        entryPoint = string.Empty;
        compatibilityMetadata = string.Empty;
        allowRuntimeFallback = false;
        if (payload.Length < FooterSize)
            return false;

        var footer = payload.AsSpan(payload.Length - FooterSize);
        if (BinaryPrimitives.ReadUInt32LittleEndian(footer) != Magic ||
            BinaryPrimitives.ReadUInt32LittleEndian(footer[4..]) != Version)
            return false;

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(footer[8..]);
        spirvLength = BinaryPrimitives.ReadInt32LittleEndian(footer[12..]);
        var libraryLength = BinaryPrimitives.ReadInt32LittleEndian(footer[16..]);
        var entryPointLength = BinaryPrimitives.ReadInt32LittleEndian(footer[20..]);
        var metadataLength = BinaryPrimitives.ReadInt32LittleEndian(footer[24..]);
        var contentLength = payload.Length - FooterSize;
        if (spirvLength <= 0 || (spirvLength & 3) != 0 || libraryLength <= 0 || entryPointLength <= 0 || metadataLength < 0 ||
            (long)spirvLength + libraryLength + entryPointLength + metadataLength != contentLength)
            return false;

        library = payload.AsSpan(spirvLength, libraryLength).ToArray();
        entryPoint = Encoding.UTF8.GetString(payload, spirvLength + libraryLength, entryPointLength);
        compatibilityMetadata = Encoding.UTF8.GetString(payload, spirvLength + libraryLength + entryPointLength, metadataLength);
        allowRuntimeFallback = (flags & AllowRuntimeFallback) != 0;
        return true;
    }
}
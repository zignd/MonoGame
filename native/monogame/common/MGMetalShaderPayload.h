// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#pragma once

#include <cstdint>
#include <cstring>

struct MGMetalShaderPayload
{
    const uint8_t* spirv = nullptr;
    uint32_t spirvSize = 0;
    const uint8_t* library = nullptr;
    uint32_t librarySize = 0;
    const char* entryPoint = nullptr;
    uint32_t entryPointSize = 0;
    const char* compatibilityMetadata = nullptr;
    uint32_t compatibilityMetadataSize = 0;
    bool allowRuntimeFallback = false;
};

static inline bool MGG_TryParseMetalShaderPayload(const uint8_t* data, uint32_t size, MGMetalShaderPayload& payload)
{
    constexpr uint32_t Magic = 0x4C4D474D;
    constexpr uint32_t Version = 1;
    constexpr uint32_t AllowRuntimeFallback = 1;
    constexpr uint32_t FooterSize = 28;
    if (data == nullptr || size < FooterSize)
        return false;

    const uint8_t* footer = data + size - FooterSize;
    uint32_t magic;
    uint32_t version;
    uint32_t flags;
    uint32_t spirvSize;
    uint32_t librarySize;
    uint32_t entryPointSize;
    uint32_t metadataSize;
    std::memcpy(&magic, footer, sizeof(magic));
    std::memcpy(&version, footer + 4, sizeof(version));
    std::memcpy(&flags, footer + 8, sizeof(flags));
    std::memcpy(&spirvSize, footer + 12, sizeof(spirvSize));
    std::memcpy(&librarySize, footer + 16, sizeof(librarySize));
    std::memcpy(&entryPointSize, footer + 20, sizeof(entryPointSize));
    std::memcpy(&metadataSize, footer + 24, sizeof(metadataSize));
    if (magic != Magic || version != Version || spirvSize == 0 || (spirvSize & 3) != 0 ||
        librarySize == 0 || entryPointSize == 0 ||
        static_cast<uint64_t>(spirvSize) + librarySize + entryPointSize + metadataSize != size - FooterSize)
        return false;

    payload.spirv = data;
    payload.spirvSize = spirvSize;
    payload.library = data + spirvSize;
    payload.librarySize = librarySize;
    payload.entryPoint = reinterpret_cast<const char*>(payload.library + librarySize);
    payload.entryPointSize = entryPointSize;
    payload.compatibilityMetadata = payload.entryPoint + entryPointSize;
    payload.compatibilityMetadataSize = metadataSize;
    payload.allowRuntimeFallback = (flags & AllowRuntimeFallback) != 0;
    return true;
}
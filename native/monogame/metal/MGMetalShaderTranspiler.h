// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#pragma once

#include <cstddef>
#include <cstdint>
#include <string>

inline constexpr int MG_MTL_CBUFFER_INDEX = 0;
inline constexpr bool MG_MTL_FLIP_VERT_Y = true;
inline constexpr const char* MG_MTL_SPIRV_CROSS_REVISION = "fb0c1a307cca4b4a9d891837bf4c44d17fe2d324";
inline constexpr const char* MG_MTL_LANGUAGE_VERSION = "2.0";

bool MGMTL_SpirvToMsl(
    const uint32_t* code,
    size_t words,
    std::string& outMsl,
    std::string& outEntry,
    std::string* errorMessage = nullptr);
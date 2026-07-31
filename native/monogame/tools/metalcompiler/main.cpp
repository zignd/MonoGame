// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#include "MGMetalShaderTranspiler.h"

#include <fstream>
#include <iostream>
#include <iterator>
#include <string>
#include <vector>

static bool WriteFile(const char* path, const std::string& content)
{
    std::ofstream output(path, std::ios::binary | std::ios::trunc);
    output.write(content.data(), static_cast<std::streamsize>(content.size()));
    return output.good();
}

int main(int argc, char** argv)
{
    if (argc == 2 && std::string(argv[1]) == "--identity")
    {
        std::cout << "spirv-cross=" << MG_MTL_SPIRV_CROSS_REVISION
                  << ";platform=macos;msl=" << MG_MTL_LANGUAGE_VERSION
                  << ";flip-vert-y=" << (MG_MTL_FLIP_VERT_Y ? "1" : "0") << std::endl;
        return 0;
    }
    if (argc != 4)
    {
        std::cerr << "Usage: mgmetalcompiler <input.spv> <output.metal> <output.entry>" << std::endl;
        return 2;
    }

    std::ifstream input(argv[1], std::ios::binary);
    std::vector<char> bytes((std::istreambuf_iterator<char>(input)), std::istreambuf_iterator<char>());
    if (!input.good() && !input.eof())
    {
        std::cerr << "Could not read SPIR-V input: " << argv[1] << std::endl;
        return 3;
    }
    if (bytes.empty() || (bytes.size() & 3) != 0)
    {
        std::cerr << "SPIR-V input must be non-empty and 32-bit aligned." << std::endl;
        return 4;
    }

    std::string msl;
    std::string entryPoint;
    std::string error;
    if (!MGMTL_SpirvToMsl(
        reinterpret_cast<const uint32_t*>(bytes.data()),
        bytes.size() / sizeof(uint32_t),
        msl,
        entryPoint,
        &error))
    {
        std::cerr << "SPIRV-Cross translation failed: " << error << std::endl;
        return 5;
    }
    if (!WriteFile(argv[2], msl) || !WriteFile(argv[3], entryPoint))
    {
        std::cerr << "Could not write translated Metal shader outputs." << std::endl;
        return 6;
    }
    return 0;
}
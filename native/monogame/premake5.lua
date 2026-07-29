-- MonoGame - Copyright (C) MonoGame Foundation, Inc
-- This file is subject to the terms and conditions defined in
-- file 'LICENSE.txt', which is part of this source code package.

local vulkan_sdk = os.getenv("VULKAN_SDK")

if vulkan_sdk == nil and os.target() == "macosx" then
    error("Error: VULKAN_SDK environment variable is not set. Please set it to your Vulkan SDK installation path.")
end

newoption {
    trigger = "arch",
    value = "ARCH",
    description = "Target architecture (x64 or arm64)",
    default = "x64",
    allowed = {
        { "x64", "64-bit x86" },
        { "arm64", "64-bit ARM" }
    }
}

-- Which SDL major version the platform layer (MGP) is built against. Default SDL2 (shipping);
-- SDL3 is opt-in and matures in parallel. The MGP sources are shared and #if MG_SDL2 / MG_SDL3 guarded.
newoption {
    trigger = "sdl",
    value = "VERSION",
    description = "SDL major version for the platform layer (2 or 3)",
    default = "2",
    allowed = {
        { "2", "SDL2 (default, shipping)" },
        { "3", "SDL3 (opt-in)" }
    }
}

function common(project_name)
    -- The SDL3 variant outputs to a distinct "<project>-sdl3" artifacts folder so it coexists on
    -- disk with the default SDL2 build (neither clobbers the other). The Trace Arena backend props
    -- pick the folder via its own -p:Sdl switch.
    local variant = project_name
    if _OPTIONS["sdl"] == "3" then
        variant = project_name .. "-sdl3"
    end
    if os.target() == "windows" then
        filter "platforms:x64"
        architecture "x86_64"
        filter "platforms:arm64"
        architecture "ARM64"
        filter {}
        platform_target_path = "../../Artifacts/native/mgruntime/" .. variant .. "/%{cfg.system}/%{cfg.platform}/%{cfg.buildcfg}"
    else
        local target_arch = _OPTIONS["arch"] or "x64"
        architecture(target_arch == "arm64" and "ARM64" or "x64")
        if os.target() == "macosx" then
            platform_target_path = "../../Artifacts/native/mgruntime/" .. variant .. "/%{cfg.system}/%{cfg.buildcfg}"
        else
            platform_target_path = "../../Artifacts/native/mgruntime/" .. variant .. "/%{cfg.system}/" .. target_arch .. "/%{cfg.buildcfg}"
        end
    end
    kind "SharedLib"
    language "C++"
    filter "system:linux"
    pic "On"
    filter {}
    defines {"DLL_EXPORT"}
    targetdir(platform_target_path)
    -- Per-variant object dir so the SDL2 and SDL3 builds don't share stale .o files (switching
    -- variants would otherwise need a `make clean`).
    objdir("obj/" .. variant)
    targetname "mgruntime"
    cppdialect "C++17"

    files {"include/**.h", "common/**.h", "common/**.cpp"}
    includedirs {"include", "../../external/stb"}
end

-- SDL is supported on all desktop platforms.
function sdl2()
    defines {"MG_SDL2"}

    files {"sdl/**.h", "sdl/**.cpp"}

    includedirs {"external/sdl2/sdl/include"}

    filter {"system:windows"}
    links {"external/sdl2/sdl/build/%{cfg.platform}/Release/SDL2-static.lib", "winmm", "imm32", "user32", "gdi32", "advapi32",
           "setupapi", "ole32", "oleaut32", "version", "shell32"}
    filter {"system:macosx"}
    libdirs {"external/sdl2/sdl/build"}
    linkoptions {"-Wl,-force_load,external/sdl2/sdl/build/libSDL2.a"}
    links {"SDL2"}
    links {"Cocoa.framework", "IOKit.framework", "ForceFeedback.framework", "CoreAudio.framework",
        "AudioToolbox.framework", "CoreGraphics.framework", "CoreFoundation.framework", "Metal.framework",
        "CoreVideo.framework", "GameController.framework", "CoreHaptics.framework", "Carbon.framework", "iconv"}

    filter {"system:linux"}
    linkoptions {"external/sdl2/sdl/build/libSDL2.a"}
    links {"dl", "pthread", "m", "rt"}
    filter {}
end

-- SDL3 (opt-in via --sdl=3). Shares the same MGP sources as sdl2(); the sources are
-- #if MG_SDL2 / MG_SDL3 guarded. SDL3 headers live under external/sdl3/include (SDL3/*.h).
function sdl3()
    -- SDL_ENABLE_OLD_NAMES turns on SDL3's official compat aliases for renamed-but-unchanged
    -- SDL2 symbols, so the shared MGP sources only need #if MG_SDL3 branches at genuine
    -- structural/semantic divergences (events, keysym, return types, surface/display/handle APIs).
    defines {"MG_SDL3", "SDL_ENABLE_OLD_NAMES"}

    files {"sdl/**.h", "sdl/**.cpp"}

    includedirs {"external/sdl3/include"}

    filter {"system:windows"}
    links {"external/sdl3/build/%{cfg.platform}/Release/SDL3-static.lib", "winmm", "imm32", "user32", "gdi32", "advapi32",
           "setupapi", "ole32", "oleaut32", "version", "shell32"}
    filter {"system:macosx"}
    libdirs {"external/sdl3/build"}
    linkoptions {"-Wl,-force_load,external/sdl3/build/libSDL3.a"}
    links {"SDL3"}
    links {"Cocoa.framework", "IOKit.framework", "ForceFeedback.framework", "CoreAudio.framework",
        "AudioToolbox.framework", "CoreGraphics.framework", "CoreFoundation.framework", "Metal.framework",
        "CoreVideo.framework", "GameController.framework", "CoreHaptics.framework", "Carbon.framework",
        "UniformTypeIdentifiers.framework", "AVFoundation.framework", "CoreMedia.framework", "iconv"}

    filter {"system:linux"}
    linkoptions {"external/sdl3/build/libSDL3.a"}
    links {"dl", "pthread", "m", "rt"}
    filter {}
end

-- Selects the platform (MGP) implementation per the --sdl option.
function sdl()
    if _OPTIONS["sdl"] == "3" then
        sdl3()
    else
        sdl2()
    end
end

-- Vulkan is supported for all desktop platforms.
function vulkan()
    defines {"MG_VULKAN"}

    files {"vulkan/**.h", "vulkan/**.cpp"}

    includedirs {"external/vulkan-headers/include", "external/volk", "external/vma/include",
        path.join(vulkan_sdk, "include")}

    filter {"system:macosx"}
    libdirs {path.join(vulkan_sdk, "lib/MoltenVK.xcframework/macos-arm64_x86_64")}
    links {"MoltenVK", "IOSurface.framework", "Foundation.framework", "QuartzCore.framework", "AppKit.framework"}
    filter {}
end

-- DirectX12 is supported on Xbox and Windows.
function directx12()
    defines {"MG_DIRECTX12"}

    files {"directx12/**.h", "directx12/**.cpp"}

    filter {"system:windows"}
    links {"dxguid", "dxgi", "d3d12"}
    filter {}
end

-- Metal is the native macOS/iOS graphics backend. It renders straight to a CAMetalLayer (no MoltenVK),
-- reusing the Vulkan backend's compiled effect headers (vulkan/*.vk.mgfxo.h: SPIR-V + reflection header)
-- and translating SPIR-V -> MSL at runtime via SPIRV-Cross (linked from the Vulkan SDK). Obj-C++ (.mm).
function metal()
    defines {"MG_METAL"}

    files {"metal/**.h", "metal/**.cpp", "metal/**.mm"}

    -- "vulkan" is on the include path so metal/ can #include the shared *.vk.mgfxo.h effect blobs;
    -- the SDK's spirv_cross include dir provides the SPIRV-Cross C++ headers (spirv_msl.hpp, ...),
    -- which include each other by bare name so the directory itself must be on the search path.
    includedirs {"vulkan", path.join(vulkan_sdk, "include"), path.join(vulkan_sdk, "include/spirv_cross")}

    filter {"system:macosx"}
    libdirs {path.join(vulkan_sdk, "lib")}
    -- SPIRV-Cross C++ API for runtime SPIR-V -> MSL. The MSL backend layers on GLSL then core; util
    -- provides helpers. (Universal static libs shipped with the Vulkan SDK.)
    links {"spirv-cross-msl", "spirv-cross-glsl", "spirv-cross-core", "spirv-cross-util"}
    links {"Metal.framework", "MetalKit.framework", "QuartzCore.framework", "Foundation.framework",
        "IOSurface.framework", "AppKit.framework"}
    filter {}

    -- Compile the Obj-C++ backend with ARC so MTL* object lifetime is automatic. Scoped to the
    -- metal .mm files only; the shared C++ TUs (sdl/faudio/common) don't touch Obj-C objects.
    filter {"files:metal/**.mm"}
    buildoptions {"-fobjc-arc"}
    filter {}
end

-- FAudio is supported for all desktop platforms.
function faudio()
    defines {"MG_FAUDIO"}

    files {"faudio/**.h", "faudio/**.cpp"}

    includedirs {"external/faudio/include"}

    -- FAudio uses SDL as its platform layer (threads/audio-device/IO), so it must be built against
    -- the SAME SDL major version we link. The SDL3 variant is built into build-sdl3 (see the SDL3
    -- FAudio build step); the default SDL2 variant stays in build.
    local faudio_build = (_OPTIONS["sdl"] == "3") and "external/faudio/build-sdl3" or "external/faudio/build"

    filter {"system:windows"}
    libdirs {faudio_build .. "/%{cfg.platform}/Release"}
    links {"FAudio.lib"}

    filter {"system:macosx"}
    libdirs {faudio_build}
    linkoptions {
        "-Wl,-force_load," .. faudio_build .. "/libFAudio.a"
    }

    filter {"system:linux"}
    linkoptions {faudio_build .. "/libFAudio.a"}
    filter {}
end

-- Xaudio is supported on Windows and Xbox.
function xaudio()
    defines {"MG_XAUDIO"}

    files {"xaudio/**.h", "xaudio/**.cpp"}
end

function configs()
    filter "configurations:Debug"
    defines {"DEBUG"}
    symbols "On"

    filter "configurations:Release"
    defines {"NDEBUG"}
    optimize "On"

    filter {"system:windows"}
    staticruntime "On"
    filter {"system:windows", "configurations:Debug"}
    runtime "Debug"
    filter {"system:windows", "configurations:Release"}
    runtime "Release"

    filter "system:macosx"
    buildoptions {"-arch x86_64", "-arch arm64"}
    linkoptions {"-arch x86_64", "-arch arm64"}
    filter {}
end

workspace "monogame"
configurations {"Debug", "Release"}
if os.target() == "windows" then
    platforms { "x64", "arm64" }
end

project "desktopvk"
common("desktopvk")
sdl()
vulkan()
faudio()
configs()

if os.target() == "macosx" then
    -- Native Metal head (macOS). Coexists on disk with desktopvk via common()'s per-project artifacts.
    project "desktopmetal"
    common("desktopmetal")
    sdl()
    metal()
    faudio()
    configs()
end

if os.target() == "windows" then
    project "windowsdx"
    common("windowsdx")
    sdl()
    directx12()
    xaudio()
    configs()
end

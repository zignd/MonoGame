// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
//
// Metal backend: pure translations from MonoGame graphics enums to Metal enums.
// No state, no device — just switch tables. Definitions in GraphicsEnums.mm.

#pragma once

#import <Metal/Metal.h>

#include "api_common.h"
#include "api_enums.h"

namespace mgmetal
{
    // Pixel/surface formats. Returns MTLPixelFormatInvalid for unsupported formats.
    MTLPixelFormat        ToMTLPixelFormat(MGSurfaceFormat format);
    // The depth/stencil attachment format for a MonoGame depth format (Invalid for None).
    MTLPixelFormat        ToMTLDepthFormat(MGDepthFormat format);
    // Number of bytes per pixel/block for CPU-side copies of an uncompressed format.
    mgint                 SurfaceFormatBytesPerPixel(MGSurfaceFormat format);
    bool                  SurfaceFormatIsCompressed(MGSurfaceFormat format);

    // Vertex attribute formats (for MTLVertexDescriptor.attributes[i].format).
    MTLVertexFormat       ToMTLVertexFormat(MGVertexElementFormat format);

    // Primitive topology. Strips/lists map to MTLPrimitiveType; the class (for the pipeline
    // topology) is derived separately in PipelineState.
    MTLPrimitiveType      ToMTLPrimitiveType(MGPrimitiveType type);
    MTLPrimitiveTopologyClass ToMTLTopologyClass(MGPrimitiveType type);
    // Index element size -> Metal index type.
    MTLIndexType          ToMTLIndexType(MGIndexElementSize size);

    // Blend.
    MTLBlendFactor        ToMTLBlendFactor(MGBlend blend);
    MTLBlendOperation     ToMTLBlendOperation(MGBlendFunction func);
    MTLColorWriteMask     ToMTLColorWriteMask(MGColorWriteChannels channels);

    // Depth/stencil.
    MTLCompareFunction    ToMTLCompareFunction(MGCompareFunction func);
    MTLStencilOperation   ToMTLStencilOperation(MGStencilOperation op);

    // Rasterizer.
    MTLCullMode           ToMTLCullMode(MGCullMode mode);
    MTLTriangleFillMode   ToMTLFillMode(MGFillMode mode);
    // MonoGame winding is front-CCW by convention (matches HLSL/DX with the Y flip); the front-face
    // winding constant is fixed and lives in PipelineState. Cull direction comes from ToMTLCullMode.

    // Sampler.
    MTLSamplerAddressMode ToMTLAddressMode(MGTextureAddressMode mode);
    // Fills min/mag/mip filters from a MonoGame combined filter.
    void                  ToMTLSamplerFilters(MGTextureFilter filter,
                                              MTLSamplerMinMagFilter& minFilter,
                                              MTLSamplerMinMagFilter& magFilter,
                                              MTLSamplerMipFilter& mipFilter,
                                              bool& anisotropic);
}

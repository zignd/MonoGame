// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
//
// Metal backend: MonoGame graphics enums -> Metal enums. Mirrors the Vulkan backend's ToVkFormat
// tables (see vulkan/MGG_Vulkan.cpp) so behaviour matches across backends.

#include "GraphicsEnums.h"

namespace mgmetal
{
    MTLPixelFormat ToMTLPixelFormat(MGSurfaceFormat format)
    {
        switch (format)
        {
        case MGSurfaceFormat::Color:            return MTLPixelFormatRGBA8Unorm;
        case MGSurfaceFormat::ColorSRgb:        return MTLPixelFormatRGBA8Unorm_sRGB;
        case MGSurfaceFormat::Bgr32:            // no dedicated 24-bit format; use BGRA
        case MGSurfaceFormat::Bgra32:           return MTLPixelFormatBGRA8Unorm;
        case MGSurfaceFormat::Bgr32SRgb:
        case MGSurfaceFormat::Bgra32SRgb:       return MTLPixelFormatBGRA8Unorm_sRGB;
        case MGSurfaceFormat::Bgra5551:         return MTLPixelFormatBGR5A1Unorm;
        case MGSurfaceFormat::Bgr565:           return MTLPixelFormatB5G6R5Unorm;
        case MGSurfaceFormat::Bgra4444:         return MTLPixelFormatABGR4Unorm;
        case MGSurfaceFormat::Alpha8:           return MTLPixelFormatA8Unorm;
        case MGSurfaceFormat::Single:           return MTLPixelFormatR32Float;
        case MGSurfaceFormat::Vector2:          return MTLPixelFormatRG32Float;
        case MGSurfaceFormat::Vector4:          return MTLPixelFormatRGBA32Float;
        case MGSurfaceFormat::HalfSingle:       return MTLPixelFormatR16Float;
        case MGSurfaceFormat::HalfVector2:      return MTLPixelFormatRG16Float;
        case MGSurfaceFormat::HalfVector4:      return MTLPixelFormatRGBA16Float;
        case MGSurfaceFormat::HdrBlendable:     return MTLPixelFormatRGBA16Float;
        case MGSurfaceFormat::Rgba1010102:      return MTLPixelFormatRGB10A2Unorm;
        case MGSurfaceFormat::Rg32:             return MTLPixelFormatRG16Unorm;
        case MGSurfaceFormat::Rgba64:           return MTLPixelFormatRGBA16Unorm;
        case MGSurfaceFormat::NormalizedByte2:  return MTLPixelFormatRG8Snorm;
        case MGSurfaceFormat::NormalizedByte4:  return MTLPixelFormatRGBA8Snorm;
        // BC (DXT) compressed — desktop macOS only.
        case MGSurfaceFormat::Dxt1:
        case MGSurfaceFormat::Dxt1a:            return MTLPixelFormatBC1_RGBA;
        case MGSurfaceFormat::Dxt1SRgb:         return MTLPixelFormatBC1_RGBA_sRGB;
        case MGSurfaceFormat::Dxt3:             return MTLPixelFormatBC2_RGBA;
        case MGSurfaceFormat::Dxt3SRgb:         return MTLPixelFormatBC2_RGBA_sRGB;
        case MGSurfaceFormat::Dxt5:             return MTLPixelFormatBC3_RGBA;
        case MGSurfaceFormat::Dxt5SRgb:         return MTLPixelFormatBC3_RGBA_sRGB;
        default:                                return MTLPixelFormatInvalid;
        }
    }

    MTLPixelFormat ToMTLDepthFormat(MGDepthFormat format)
    {
        switch (format)
        {
        case MGDepthFormat::None:            return MTLPixelFormatInvalid;
        // Metal has no 16-bit combined; Depth16Unorm is fine for Depth16, Depth32Float for 24.
        case MGDepthFormat::Depth16:         return MTLPixelFormatDepth16Unorm;
        case MGDepthFormat::Depth24:         return MTLPixelFormatDepth32Float;
        case MGDepthFormat::Depth24Stencil8: return MTLPixelFormatDepth32Float_Stencil8;
        default:                             return MTLPixelFormatInvalid;
        }
    }

    bool SurfaceFormatIsCompressed(MGSurfaceFormat format)
    {
        switch (format)
        {
        case MGSurfaceFormat::Dxt1:
        case MGSurfaceFormat::Dxt1a:
        case MGSurfaceFormat::Dxt1SRgb:
        case MGSurfaceFormat::Dxt3:
        case MGSurfaceFormat::Dxt3SRgb:
        case MGSurfaceFormat::Dxt5:
        case MGSurfaceFormat::Dxt5SRgb:
            return true;
        default:
            return false;
        }
    }

    mgint SurfaceFormatBytesPerPixel(MGSurfaceFormat format)
    {
        switch (format)
        {
        case MGSurfaceFormat::Alpha8:           return 1;
        case MGSurfaceFormat::Bgr565:
        case MGSurfaceFormat::Bgra5551:
        case MGSurfaceFormat::Bgra4444:
        case MGSurfaceFormat::HalfSingle:
        case MGSurfaceFormat::NormalizedByte2:  return 2;
        case MGSurfaceFormat::Color:
        case MGSurfaceFormat::ColorSRgb:
        case MGSurfaceFormat::Bgr32:
        case MGSurfaceFormat::Bgra32:
        case MGSurfaceFormat::Bgr32SRgb:
        case MGSurfaceFormat::Bgra32SRgb:
        case MGSurfaceFormat::Single:
        case MGSurfaceFormat::HalfVector2:
        case MGSurfaceFormat::Rgba1010102:
        case MGSurfaceFormat::Rg32:
        case MGSurfaceFormat::NormalizedByte4:  return 4;
        case MGSurfaceFormat::Vector2:
        case MGSurfaceFormat::HalfVector4:
        case MGSurfaceFormat::Rgba64:           return 8;
        case MGSurfaceFormat::Vector4:          return 16;
        default:                                return 4;
        }
    }

    MTLVertexFormat ToMTLVertexFormat(MGVertexElementFormat format)
    {
        switch (format)
        {
        case MGVertexElementFormat::Single:            return MTLVertexFormatFloat;
        case MGVertexElementFormat::Vector2:           return MTLVertexFormatFloat2;
        case MGVertexElementFormat::Vector3:           return MTLVertexFormatFloat3;
        case MGVertexElementFormat::Vector4:           return MTLVertexFormatFloat4;
        // MonoGame Color packs RGBA in memory (matches Vulkan R8G8B8A8_UNORM).
        case MGVertexElementFormat::Color:             return MTLVertexFormatUChar4Normalized;
        case MGVertexElementFormat::Byte4:             return MTLVertexFormatUChar4;
        case MGVertexElementFormat::Short2:            return MTLVertexFormatShort2;
        case MGVertexElementFormat::Short4:            return MTLVertexFormatShort4;
        case MGVertexElementFormat::NormalizedShort2:  return MTLVertexFormatShort2Normalized;
        case MGVertexElementFormat::NormalizedShort4:  return MTLVertexFormatShort4Normalized;
        case MGVertexElementFormat::HalfVector2:       return MTLVertexFormatHalf2;
        case MGVertexElementFormat::HalfVector4:       return MTLVertexFormatHalf4;
        default:                                       return MTLVertexFormatInvalid;
        }
    }

    MTLPrimitiveType ToMTLPrimitiveType(MGPrimitiveType type)
    {
        switch (type)
        {
        case MGPrimitiveType::TriangleList:  return MTLPrimitiveTypeTriangle;
        case MGPrimitiveType::TriangleStrip: return MTLPrimitiveTypeTriangleStrip;
        case MGPrimitiveType::LineList:      return MTLPrimitiveTypeLine;
        case MGPrimitiveType::LineStrip:     return MTLPrimitiveTypeLineStrip;
        case MGPrimitiveType::PointList:     return MTLPrimitiveTypePoint;
        default:                             return MTLPrimitiveTypeTriangle;
        }
    }

    MTLPrimitiveTopologyClass ToMTLTopologyClass(MGPrimitiveType type)
    {
        switch (type)
        {
        case MGPrimitiveType::PointList:     return MTLPrimitiveTopologyClassPoint;
        case MGPrimitiveType::LineList:
        case MGPrimitiveType::LineStrip:     return MTLPrimitiveTopologyClassLine;
        default:                             return MTLPrimitiveTopologyClassTriangle;
        }
    }

    MTLIndexType ToMTLIndexType(MGIndexElementSize size)
    {
        return size == MGIndexElementSize::ThirtyTwoBits ? MTLIndexTypeUInt32 : MTLIndexTypeUInt16;
    }

    MTLBlendFactor ToMTLBlendFactor(MGBlend blend)
    {
        switch (blend)
        {
        case MGBlend::One:                      return MTLBlendFactorOne;
        case MGBlend::Zero:                     return MTLBlendFactorZero;
        case MGBlend::SourceColor:              return MTLBlendFactorSourceColor;
        case MGBlend::InverseSourceColor:       return MTLBlendFactorOneMinusSourceColor;
        case MGBlend::SourceAlpha:              return MTLBlendFactorSourceAlpha;
        case MGBlend::InverseSourceAlpha:       return MTLBlendFactorOneMinusSourceAlpha;
        case MGBlend::DestinationColor:         return MTLBlendFactorDestinationColor;
        case MGBlend::InverseDestinationColor:  return MTLBlendFactorOneMinusDestinationColor;
        case MGBlend::DestinationAlpha:         return MTLBlendFactorDestinationAlpha;
        case MGBlend::InverseDestinationAlpha:  return MTLBlendFactorOneMinusDestinationAlpha;
        case MGBlend::BlendFactor:              return MTLBlendFactorBlendColor;
        case MGBlend::InverseBlendFactor:       return MTLBlendFactorOneMinusBlendColor;
        case MGBlend::SourceAlphaSaturation:    return MTLBlendFactorSourceAlphaSaturated;
        default:                                return MTLBlendFactorOne;
        }
    }

    MTLBlendOperation ToMTLBlendOperation(MGBlendFunction func)
    {
        switch (func)
        {
        case MGBlendFunction::Add:              return MTLBlendOperationAdd;
        case MGBlendFunction::Subtract:         return MTLBlendOperationSubtract;
        case MGBlendFunction::ReverseSubtract:  return MTLBlendOperationReverseSubtract;
        case MGBlendFunction::Min:              return MTLBlendOperationMin;
        case MGBlendFunction::Max:              return MTLBlendOperationMax;
        default:                                return MTLBlendOperationAdd;
        }
    }

    MTLColorWriteMask ToMTLColorWriteMask(MGColorWriteChannels channels)
    {
        MTLColorWriteMask mask = MTLColorWriteMaskNone;
        if ((mgint)channels & (mgint)MGColorWriteChannels::Red)   mask |= MTLColorWriteMaskRed;
        if ((mgint)channels & (mgint)MGColorWriteChannels::Green) mask |= MTLColorWriteMaskGreen;
        if ((mgint)channels & (mgint)MGColorWriteChannels::Blue)  mask |= MTLColorWriteMaskBlue;
        if ((mgint)channels & (mgint)MGColorWriteChannels::Alpha) mask |= MTLColorWriteMaskAlpha;
        return mask;
    }

    MTLCompareFunction ToMTLCompareFunction(MGCompareFunction func)
    {
        switch (func)
        {
        case MGCompareFunction::Always:       return MTLCompareFunctionAlways;
        case MGCompareFunction::Never:        return MTLCompareFunctionNever;
        case MGCompareFunction::Less:         return MTLCompareFunctionLess;
        case MGCompareFunction::LessEqual:    return MTLCompareFunctionLessEqual;
        case MGCompareFunction::Equal:        return MTLCompareFunctionEqual;
        case MGCompareFunction::GreaterEqual: return MTLCompareFunctionGreaterEqual;
        case MGCompareFunction::Greater:      return MTLCompareFunctionGreater;
        case MGCompareFunction::NotEqual:     return MTLCompareFunctionNotEqual;
        default:                              return MTLCompareFunctionLessEqual;
        }
    }

    MTLStencilOperation ToMTLStencilOperation(MGStencilOperation op)
    {
        switch (op)
        {
        case MGStencilOperation::Keep:                return MTLStencilOperationKeep;
        case MGStencilOperation::Zero:                return MTLStencilOperationZero;
        case MGStencilOperation::Replace:             return MTLStencilOperationReplace;
        case MGStencilOperation::Increment:           return MTLStencilOperationIncrementWrap;
        case MGStencilOperation::Decrement:           return MTLStencilOperationDecrementWrap;
        case MGStencilOperation::IncrementSaturation: return MTLStencilOperationIncrementClamp;
        case MGStencilOperation::DecrementSaturation: return MTLStencilOperationDecrementClamp;
        case MGStencilOperation::Invert:              return MTLStencilOperationInvert;
        default:                                      return MTLStencilOperationKeep;
        }
    }

    MTLCullMode ToMTLCullMode(MGCullMode mode)
    {
        switch (mode)
        {
        case MGCullMode::None:                     return MTLCullModeNone;
        // MonoGame default winding is clockwise = front; we set frontFacingWinding=CW in the
        // pipeline, so "cull clockwise face" culls the front, "cull ccw" culls the back.
        case MGCullMode::CullClockwiseFace:        return MTLCullModeFront;
        case MGCullMode::CullCounterClockwiseFace: return MTLCullModeBack;
        default:                                   return MTLCullModeNone;
        }
    }

    MTLTriangleFillMode ToMTLFillMode(MGFillMode mode)
    {
        return mode == MGFillMode::WireFrame ? MTLTriangleFillModeLines : MTLTriangleFillModeFill;
    }

    MTLSamplerAddressMode ToMTLAddressMode(MGTextureAddressMode mode)
    {
        switch (mode)
        {
        case MGTextureAddressMode::Wrap:   return MTLSamplerAddressModeRepeat;
        case MGTextureAddressMode::Clamp:  return MTLSamplerAddressModeClampToEdge;
        case MGTextureAddressMode::Mirror: return MTLSamplerAddressModeMirrorRepeat;
        case MGTextureAddressMode::Border: return MTLSamplerAddressModeClampToBorderColor;
        default:                           return MTLSamplerAddressModeClampToEdge;
        }
    }

    void ToMTLSamplerFilters(MGTextureFilter filter,
                             MTLSamplerMinMagFilter& minFilter,
                             MTLSamplerMinMagFilter& magFilter,
                             MTLSamplerMipFilter& mipFilter,
                             bool& anisotropic)
    {
        anisotropic = false;
        // Defaults.
        minFilter = MTLSamplerMinMagFilterLinear;
        magFilter = MTLSamplerMinMagFilterLinear;
        mipFilter = MTLSamplerMipFilterLinear;

        switch (filter)
        {
        case MGTextureFilter::Linear:
            minFilter = magFilter = MTLSamplerMinMagFilterLinear; mipFilter = MTLSamplerMipFilterLinear; break;
        case MGTextureFilter::Point:
            minFilter = magFilter = MTLSamplerMinMagFilterNearest; mipFilter = MTLSamplerMipFilterNearest; break;
        case MGTextureFilter::Anisotropic:
            minFilter = magFilter = MTLSamplerMinMagFilterLinear; mipFilter = MTLSamplerMipFilterLinear; anisotropic = true; break;
        case MGTextureFilter::LinearMipPoint:
            minFilter = magFilter = MTLSamplerMinMagFilterLinear; mipFilter = MTLSamplerMipFilterNearest; break;
        case MGTextureFilter::PointMipLinear:
            minFilter = magFilter = MTLSamplerMinMagFilterNearest; mipFilter = MTLSamplerMipFilterLinear; break;
        case MGTextureFilter::MinLinearMagPointMipLinear:
            minFilter = MTLSamplerMinMagFilterLinear; magFilter = MTLSamplerMinMagFilterNearest; mipFilter = MTLSamplerMipFilterLinear; break;
        case MGTextureFilter::MinLinearMagPointMipPoint:
            minFilter = MTLSamplerMinMagFilterLinear; magFilter = MTLSamplerMinMagFilterNearest; mipFilter = MTLSamplerMipFilterNearest; break;
        case MGTextureFilter::MinPointMagLinearMipLinear:
            minFilter = MTLSamplerMinMagFilterNearest; magFilter = MTLSamplerMinMagFilterLinear; mipFilter = MTLSamplerMipFilterLinear; break;
        case MGTextureFilter::MinPointMagLinearMipPoint:
            minFilter = MTLSamplerMinMagFilterNearest; magFilter = MTLSamplerMinMagFilterLinear; mipFilter = MTLSamplerMipFilterNearest; break;
        default:
            break;
        }
    }
}

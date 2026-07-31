// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#pragma once

#include "directx12.h"

#include "GraphicsEnums.h"

enum class MGPrimitiveType;

namespace Graphics {
class DeviceResources;

struct InputElement {
    int offset;
    MGVertexElementFormat format;
    MGVertexElementUsage usage;
    int usageIndex;
    int inputSlot;
};

class PipelineStateManager {
public:
    PipelineStateManager(DeviceResources* device);
    ~PipelineStateManager();

    void Reset();

    void Prepare();

    void ApplyCurrentPipelineState();

    void GetDiagnostics(MGG_ShaderPipelineDiagnostics& diagnostics) const;
    void ResetDiagnostics();
    MGPipelineCacheStatus ImportPipelineCache(const mgbyte* data, size_t dataBytes);
    size_t GetPipelineCacheDataSize() const;
    bool GetPipelineCacheData(mgbyte* data, size_t dataBytes) const;

    void SetDeviceParameters();
    size_t GetPipelineHash();

    struct InternalData {
        DeviceResources* m_deviceRes;

        D3D12_GRAPHICS_PIPELINE_STATE_DESC m_currentPSODesc;
        std::vector<D3D12_INPUT_ELEMENT_DESC> m_inputElementDescs;

        size_t m_lastPSOHash = 0;
        std::unordered_map<size_t, Microsoft::WRL::ComPtr<ID3D12PipelineState>> m_psoHashMap;
        Microsoft::WRL::ComPtr<ID3D12PipelineLibrary> m_pipelineLibrary;

        uint64_t m_pipelineCacheHits = 0;
        uint64_t m_pipelineCacheMisses = 0;
        uint64_t m_pipelineCreationCount = 0;
        double m_pipelineCreationMilliseconds = 0.0;
    };

    InternalData* impl;
};

}

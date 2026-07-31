// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#include "directx12.h"

#include "PipelineState.h"
#include "DeviceResources.h"
#include "GraphicsEnums.h"
#include "Texture.h"
#include "CommandContext.h"

#include <chrono>

using namespace DirectX;
using namespace DX;
using namespace Graphics;


PipelineStateManager::PipelineStateManager(DeviceResources* device) {
    impl = new InternalData();
    impl->m_deviceRes = device;

    impl->m_currentPSODesc = {};
    impl->m_currentPSODesc.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_DEFAULT);
    impl->m_currentPSODesc.BlendState = CD3DX12_BLEND_DESC(D3D12_DEFAULT);
    impl->m_currentPSODesc.DepthStencilState = CD3DX12_DEPTH_STENCIL_DESC(D3D12_DEFAULT);
    impl->m_currentPSODesc.PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
    ImportPipelineCache(nullptr, 0);
}

PipelineStateManager::~PipelineStateManager() {
    delete impl;
}

void PipelineStateManager::Reset() {
    impl->m_lastPSOHash = 0;
    impl->m_psoHashMap.clear();
}

void PipelineStateManager::SetDeviceParameters() {
    impl->m_deviceRes->GetCommandContext()->SetPSODeviceParameters(impl->m_currentPSODesc);
}

static size_t HashBytes(const void* data, size_t count, size_t hash = 14695981039346656037ull) {
    const auto* bytes = static_cast<const uint8_t*>(data);
    for (size_t index = 0; index < count; index++) {
        hash ^= bytes[index];
        hash *= 1099511628211ull;
    }
    return hash;
}

static size_t HashShader(const D3D12_SHADER_BYTECODE& shader, size_t hash) {
    if (shader.pShaderBytecode == nullptr || shader.BytecodeLength == 0)
        return hash;
    return HashBytes(shader.pShaderBytecode, shader.BytecodeLength, hash);
}

size_t Graphics::PipelineStateManager::GetPipelineHash() {
    auto descriptor = impl->m_currentPSODesc;
    descriptor.pRootSignature = nullptr;
    descriptor.VS = {};
    descriptor.PS = {};
    descriptor.DS = {};
    descriptor.HS = {};
    descriptor.GS = {};
    descriptor.StreamOutput = {};
    descriptor.InputLayout = {};
    descriptor.CachedPSO = {};
    size_t hash = HashBytes(&descriptor, sizeof(descriptor));
    hash = HashShader(impl->m_currentPSODesc.VS, hash);
    hash = HashShader(impl->m_currentPSODesc.PS, hash);
    hash = HashShader(impl->m_currentPSODesc.DS, hash);
    hash = HashShader(impl->m_currentPSODesc.HS, hash);
    hash = HashShader(impl->m_currentPSODesc.GS, hash);
    for (const auto& element : impl->m_inputElementDescs) {
        auto stableElement = element;
        stableElement.SemanticName = nullptr;
        hash = HashBytes(&stableElement, sizeof(stableElement), hash);
        if (element.SemanticName != nullptr)
            hash = HashBytes(element.SemanticName, strlen(element.SemanticName), hash);
    }
    const uint32_t rootSignatureSchema = 1;
    return HashBytes(&rootSignatureSchema, sizeof(rootSignatureSchema), hash);
}

void PipelineStateManager::Prepare() {
    impl->m_lastPSOHash = 0;
}

void PipelineStateManager::GetDiagnostics(MGG_ShaderPipelineDiagnostics& diagnostics) const {
    diagnostics.PipelineCacheHits = impl->m_pipelineCacheHits;
    diagnostics.PipelineCacheMisses = impl->m_pipelineCacheMisses;
    diagnostics.PipelineCreationCount = impl->m_pipelineCreationCount;
    diagnostics.PipelineCreationMilliseconds = impl->m_pipelineCreationMilliseconds;
}

void PipelineStateManager::ResetDiagnostics() {
    impl->m_pipelineCacheHits = 0;
    impl->m_pipelineCacheMisses = 0;
    impl->m_pipelineCreationCount = 0;
    impl->m_pipelineCreationMilliseconds = 0.0;
}

MGPipelineCacheStatus PipelineStateManager::ImportPipelineCache(const mgbyte* data, size_t dataBytes) {
    Microsoft::WRL::ComPtr<ID3D12Device1> device;
    if (FAILED(impl->m_deviceRes->GetD3DDevice()->QueryInterface(IID_PPV_ARGS(&device))))
        return MGPipelineCacheStatus::Unsupported;
    Microsoft::WRL::ComPtr<ID3D12PipelineLibrary> library;
    HRESULT result = device->CreatePipelineLibrary(data, dataBytes, IID_PPV_ARGS(&library));
    if (FAILED(result))
        return result == E_INVALIDARG ? MGPipelineCacheStatus::Incompatible : MGPipelineCacheStatus::Error;
    impl->m_pipelineLibrary = library;
    return dataBytes == 0 ? MGPipelineCacheStatus::Empty : MGPipelineCacheStatus::Success;
}

size_t PipelineStateManager::GetPipelineCacheDataSize() const {
    return impl->m_pipelineLibrary ? impl->m_pipelineLibrary->GetSerializedSize() : 0;
}

bool PipelineStateManager::GetPipelineCacheData(mgbyte* data, size_t dataBytes) const {
    if (!impl->m_pipelineLibrary || data == nullptr || dataBytes < impl->m_pipelineLibrary->GetSerializedSize())
        return false;
    return SUCCEEDED(impl->m_pipelineLibrary->Serialize(data, dataBytes));
}

void PipelineStateManager::ApplyCurrentPipelineState() {
    SetDeviceParameters();
    size_t HashCode = GetPipelineHash();

    if (HashCode != impl->m_lastPSOHash) {
        ID3D12PipelineState* ps = nullptr;
        auto iter = impl->m_psoHashMap.find(HashCode);
        if (iter != impl->m_psoHashMap.end()) {
            impl->m_pipelineCacheHits++;
            ps = iter->second.Get();
        }
        else {
            auto started = std::chrono::steady_clock::now();
            auto pipelineName = L"MonoGame-" + std::to_wstring(HashCode);
            if (impl->m_pipelineLibrary)
                impl->m_pipelineLibrary->LoadGraphicsPipeline(pipelineName.c_str(), &impl->m_currentPSODesc, IID_GRAPHICS_PPV_ARGS(&ps));
            if (ps == nullptr) {
                impl->m_pipelineCacheMisses++;
                DX::ThrowIfFailed(impl->m_deviceRes->GetD3DDevice()->CreateGraphicsPipelineState(&impl->m_currentPSODesc, IID_GRAPHICS_PPV_ARGS(&ps)));
                if (impl->m_pipelineLibrary)
                    impl->m_pipelineLibrary->StorePipeline(pipelineName.c_str(), ps);
                impl->m_pipelineCreationCount++;
                impl->m_pipelineCreationMilliseconds += std::chrono::duration<double, std::milli>(
                    std::chrono::steady_clock::now() - started).count();
            }
            else
                impl->m_pipelineCacheHits++;
            impl->m_psoHashMap[HashCode].Attach(ps);
        }

        impl->m_deviceRes->GetCommandContext()->SetPipelineState(ps);
        impl->m_lastPSOHash = HashCode;
    }
}

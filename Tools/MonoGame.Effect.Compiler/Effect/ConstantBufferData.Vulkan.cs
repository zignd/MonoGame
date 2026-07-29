// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework;
using MonoGame.Effect.Compiler.Effect.Spirv;
using System;
using System.Linq;

namespace MonoGame.Effect
{
    internal partial class ConstantBufferData
    {
        static T RequireType<T>(T? spirvType, string context) where T : class
        {
            return spirvType ?? throw new InvalidOperationException($"Missing SPIR-V type information for {context}.");
        }

        static EffectObject.D3DXPARAMETER_TYPE ToParamType(SpirvTypeBase spirvType)
        {
            if (spirvType is SpirvTypeVector vector)
                return ToParamType(RequireType(vector.ElementType, $"vector '{vector.Id}' element type"));
            else if (spirvType is SpirvTypeMatrix matrix)
                return ToParamType(RequireType(RequireType(matrix.ColumnType, $"matrix '{matrix.Id}' column type").ElementType, $"matrix '{matrix.Id}' column element type"));
            else if (spirvType is SpirvTypeArray array)
                return ToParamType(RequireType(array.ElementType, $"array '{array.Id}' element type"));

            switch (spirvType.Type)
            {
                case SpirvType.Float:
                    return EffectObject.D3DXPARAMETER_TYPE.FLOAT;
                case SpirvType.Int:
                    return EffectObject.D3DXPARAMETER_TYPE.INT;
                case SpirvType.Bool:
                    return EffectObject.D3DXPARAMETER_TYPE.BOOL;
                default:
                    throw new Exception("Unknown data type: " + spirvType);
            }
        }

        static (uint rows, uint columns, EffectObject.D3DXPARAMETER_CLASS paramClass) DimensionsForType(SpirvTypeBase spirvType)
        {
            if (spirvType is SpirvTypeArray array)
                return DimensionsForType(RequireType(array.ElementType, $"array '{array.Id}' element type"));
            else if (spirvType is SpirvTypeVector vector)
                return (1, vector.Dimensions, EffectObject.D3DXPARAMETER_CLASS.VECTOR);
            else if (spirvType is SpirvTypeMatrix matrix)
                return (RequireType(matrix.ColumnType, $"matrix '{matrix.Id}' column type").Dimensions, matrix.Columns, EffectObject.D3DXPARAMETER_CLASS.MATRIX_COLUMNS);
            else
                return (1, 1, EffectObject.D3DXPARAMETER_CLASS.SCALAR);
        }

        // This one calculates how large we need to make the bit array of data for this specific parameter
        static uint DataSizeForMember(SpirvTypeBase type)
        {
            if (type is SpirvTypeScalar svScalar)
                // SPIR-V scalar widths are bit-sized.
                return svScalar.Width / 8;
            else if (type is SpirvTypeVector svVector)
                return DataSizeForMember(RequireType(svVector.ElementType, $"vector '{svVector.Id}' element type")) * svVector.Dimensions;
            else if (type is SpirvTypeMatrix svMatrix)
                return DataSizeForMember(RequireType(svMatrix.ColumnType, $"matrix '{svMatrix.Id}' column type")) * svMatrix.Columns;
            else if (type is SpirvTypeArray svArray)
                return DataSizeForMember(RequireType(svArray.ElementType, $"array '{svArray.Id}' element type"));
            else
                return 4;
        }

        // And this one calculates the size of the parameter with padding
        static uint PaddingSizeForMember(SpirvTypeStructMember member)
        {
            if (member.Type is SpirvTypeScalar svScalar)
                // SPIR-V scalar widths are bit-sized.
                return svScalar.Width / 8;
            else if (member.Type is SpirvTypeVector svVector)
                return svVector.Dimensions * RequireType(svVector.ElementType, $"vector member '{member.Name}' element type").Width / 8;
            else if (member.Type is SpirvTypeMatrix svMatrix)
                return (member.MatrixStride ?? throw new InvalidOperationException($"Matrix member '{member.Name}' is missing MatrixStride.")) * svMatrix.Columns;
            else if (member.Type is SpirvTypeArray svArray)
                return (svArray.ArrayStride ?? throw new InvalidOperationException($"Array member '{member.Name}' is missing ArrayStride.")) * svArray.Length;
            else
                return 4;
        }

        public static ConstantBufferData BuildFromSpirvStruct(SpirvTypeStruct svStruct)
        {
            var cbuffer = new ConstantBufferData(string.IsNullOrEmpty(svStruct.Name) ? svStruct.Id : svStruct.Name);
            var byOffset = svStruct.Members.OrderBy(m => m.Offset ?? throw new InvalidOperationException($"Struct member '{m.Name}' is missing Offset."));

            foreach (var member in byOffset)
            {
                var param = new EffectObject.d3dx_parameter();
                param.name = member.Name;
                param.semantic = string.Empty;
                param.bufferOffset = (int)(member.Offset ?? throw new InvalidOperationException($"Struct member '{member.Name}' is missing Offset."));

                (param.rows, param.columns, param.class_) = DimensionsForType(member.Type);
                param.type = ToParamType(member.Type);
                var dataSize = DataSizeForMember(member.Type);

                if (member.Type is SpirvTypeArray array)
                {
                    param.element_count = array.Length;
                    param.member_handles = new EffectObject.d3dx_parameter[param.element_count];

                    for (uint i = 0; i < array.Length; i++)
                    {
                        var mparam = new EffectObject.d3dx_parameter();

                        mparam.name = string.Empty;
                        mparam.semantic = string.Empty;
                        mparam.type = param.type;
                        mparam.class_ = param.class_;
                        mparam.rows = param.rows;
                        mparam.columns = param.columns;
                        mparam.data = new byte[dataSize];

                        param.member_handles[i] = mparam;
                    }
                }
                else
                {
                    // TODO: Default value?
                    var data = new byte[dataSize];
                    param.data = data;
                }

                cbuffer.Parameters.Add(param);
                cbuffer.ParameterOffset.Add(param.bufferOffset);
            }

            var lastItem = svStruct.Members.MaxBy(mem => mem.Offset ?? throw new InvalidOperationException($"Struct member '{mem.Name}' is missing Offset."))
                ?? throw new InvalidOperationException($"Struct '{svStruct.Name ?? svStruct.Id}' does not contain any members.");
            cbuffer.Size = (int)((lastItem.Offset ?? throw new InvalidOperationException($"Struct member '{lastItem.Name}' is missing Offset.")) + PaddingSizeForMember(lastItem));
            cbuffer.Size = ((cbuffer.Size + 15) / 16) * 16;

            return cbuffer;
        }
    }
}

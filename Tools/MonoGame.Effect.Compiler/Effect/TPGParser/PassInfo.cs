// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Graphics;
using System.Globalization;

namespace MonoGame.Effect
{
    /// <summary>
    /// Stores the parsed shader entry points and render-state assignments for an effect pass.
    /// </summary>
    public class PassInfo
    {
        /// <summary>
        /// The pass name.
        /// </summary>
        public string name = string.Empty;

        /// <summary>
        /// The vertex shader model string.
        /// </summary>
        public string? vsModel;

        /// <summary>
        /// The vertex shader entry-point function name.
        /// </summary>
        public string? vsFunction;

        /// <summary>
        /// The pixel shader model string.
        /// </summary>
        public string? psModel;

        /// <summary>
        /// The pixel shader entry-point function name.
        /// </summary>
        public string? psFunction;

        /// <summary>
        /// The pass blend state, when one is configured.
        /// </summary>
        public BlendState? blendState;

        /// <summary>
        /// The pass rasterizer state, when one is configured.
        /// </summary>
        public RasterizerState? rasterizerState;

        /// <summary>
        /// The pass depth-stencil state, when one is configured.
        /// </summary>
        public DepthStencilState? depthStencilState;

        private static Blend ToAlphaBlend(Blend blend)
        {
            switch (blend)
            {
                case Blend.SourceColor:
                    return Blend.SourceAlpha;
                case Blend.InverseSourceColor:
                    return Blend.InverseSourceAlpha;
                case Blend.DestinationColor:
                    return Blend.DestinationAlpha;
                case Blend.InverseDestinationColor:
                    return Blend.InverseDestinationAlpha;
            }
            return blend;
        }

        /// <summary>
        /// Sets whether alpha blending is enabled for the pass.
        /// </summary>
        public bool AlphaBlendEnable
        {
            set
            {
                if (value)
                {
                    if (blendState == null)
                    {
                        blendState = new BlendState();
                        blendState.ColorSourceBlend = Blend.One;
                        blendState.AlphaSourceBlend = Blend.One;
                        blendState.ColorDestinationBlend = Blend.InverseSourceAlpha;
                        blendState.AlphaDestinationBlend = Blend.InverseSourceAlpha;
                    }
                }
                else if (!value)
                {
                    if (blendState == null)
                        blendState = new BlendState();
                    blendState.ColorSourceBlend = Blend.One;
                    blendState.AlphaSourceBlend = Blend.One;
                    blendState.ColorDestinationBlend = Blend.Zero;
                    blendState.AlphaDestinationBlend = Blend.Zero;
                }
            }
        }

        /// <summary>
        /// Sets the fill mode used by the pass rasterizer state.
        /// </summary>
        public FillMode FillMode
        {
            set
            {
                if (rasterizerState == null)
                    rasterizerState = new RasterizerState();
                rasterizerState.FillMode = value;
            }
        }

        /// <summary>
        /// Sets the face-culling mode used by the pass rasterizer state.
        /// </summary>
        public CullMode CullMode
        {
            set
            {
                if (rasterizerState == null)
                    rasterizerState = new RasterizerState();
                rasterizerState.CullMode = value;
            }
        }

        /// <summary>
        /// Sets whether depth testing is enabled.
        /// </summary>
        public bool ZEnable
        {
            set
            {
                if (depthStencilState == null)
                    depthStencilState = new DepthStencilState();
                depthStencilState.DepthBufferEnable = value;
            }
        }

        /// <summary>
        /// Sets whether depth writes are enabled.
        /// </summary>
        public bool ZWriteEnable
        {
            set
            {
                if (depthStencilState == null)
                    depthStencilState = new DepthStencilState();
                depthStencilState.DepthBufferWriteEnable = value;
            }
        }

        /// <summary>
        /// Sets the depth comparison function.
        /// </summary>
        public CompareFunction DepthBufferFunction
        {
            set
            {
                if (depthStencilState == null)
                    depthStencilState = new DepthStencilState();
                depthStencilState.DepthBufferFunction = value;
            }
        }

        /// <summary>
        /// Sets whether multisample antialiasing is enabled.
        /// </summary>
        public bool MultiSampleAntiAlias
        {
            set
            {
                if (rasterizerState == null)
                    rasterizerState = new RasterizerState();
                rasterizerState.MultiSampleAntiAlias = value;
            }
        }

        /// <summary>
        /// Sets whether scissor testing is enabled.
        /// </summary>
        public bool ScissorTestEnable
        {
            set
            {
                if (rasterizerState == null)
                    rasterizerState = new RasterizerState();
                rasterizerState.ScissorTestEnable = value;
            }
        }

        /// <summary>
        /// Sets whether stencil testing is enabled.
        /// </summary>
        public bool StencilEnable
        {
            set
            {
                if (depthStencilState == null)
                    depthStencilState = new DepthStencilState();
                depthStencilState.StencilEnable = value;
            }
        }

        /// <summary>
        /// Sets the stencil operation to apply when the stencil test fails.
        /// </summary>
        public StencilOperation StencilFail
        {
            set
            {
                if (depthStencilState == null)
                    depthStencilState = new DepthStencilState();
                depthStencilState.StencilFail = value;
            }
        }

        /// <summary>
        /// Sets the stencil comparison function.
        /// </summary>
        public CompareFunction StencilFunc
        {
            set
            {
                if (depthStencilState == null)
                    depthStencilState = new DepthStencilState();
                depthStencilState.StencilFunction = value;
            }
        }

        /// <summary>
        /// Sets the stencil-read mask.
        /// </summary>
        public int StencilMask
        {
            set
            {
                if (depthStencilState == null)
                    depthStencilState = new DepthStencilState();
                depthStencilState.StencilMask = value;
            }
        }

        /// <summary>
        /// Sets the stencil operation to apply when both stencil and depth tests pass.
        /// </summary>
        public StencilOperation StencilPass
        {
            set
            {
                if (depthStencilState == null)
                    depthStencilState = new DepthStencilState();
                depthStencilState.StencilPass = value;
            }
        }

        /// <summary>
        /// Sets the stencil reference value.
        /// </summary>
        public int StencilRef
        {
            set
            {
                if (depthStencilState == null)
                    depthStencilState = new DepthStencilState();
                depthStencilState.ReferenceStencil = value;
            }
        }

        /// <summary>
        /// Sets the stencil-write mask.
        /// </summary>
        public int StencilWriteMask
        {
            set
            {
                if (depthStencilState == null)
                    depthStencilState = new DepthStencilState();
                depthStencilState.StencilWriteMask = value;
            }
        }

        /// <summary>
        /// Sets the stencil operation to apply when the stencil test passes but the depth test fails.
        /// </summary>
        public StencilOperation StencilZFail
        {
            set
            {
                if (depthStencilState == null)
                    depthStencilState = new DepthStencilState();
                depthStencilState.StencilDepthBufferFail = value;
            }
        }

        /// <summary>
        /// Sets the source blend factor.
        /// </summary>
        public Blend SrcBlend
        {
            set
            {
                if (blendState == null)
                    blendState = new BlendState();
                blendState.ColorSourceBlend = value;
                blendState.AlphaSourceBlend = ToAlphaBlend(value);
            }
        }

        /// <summary>
        /// Sets the destination blend factor.
        /// </summary>
        public Blend DestBlend
        {
            set
            {
                if (blendState == null)
                    blendState = new BlendState();
                blendState.ColorDestinationBlend = value;
                blendState.AlphaDestinationBlend = ToAlphaBlend(value);
            }
        }

        /// <summary>
        /// Sets the blend operation.
        /// </summary>
        public BlendFunction BlendOp
        {
            set
            {
                if (blendState == null)
                    blendState = new BlendState();
                blendState.AlphaBlendFunction = value;
            }
        }

        /// <summary>
        /// Sets the enabled color write channels.
        /// </summary>
        public ColorWriteChannels ColorWriteEnable
        {
            set
            {
                if (blendState == null)
                    blendState = new BlendState();
                blendState.ColorWriteChannels = value;
            }
        }

        /// <summary>
        /// Sets the rasterizer depth bias.
        /// </summary>
        public float DepthBias
        {
            set
            {
                if (rasterizerState == null)
                    rasterizerState = new RasterizerState();
                rasterizerState.DepthBias = value;
            }
        }

        /// <summary>
        /// Sets the rasterizer slope-scaled depth bias.
        /// </summary>
        public float SlopeScaleDepthBias
        {
            set
            {
                if (rasterizerState == null)
                    rasterizerState = new RasterizerState();
                rasterizerState.SlopeScaleDepthBias = value;
            }
        }
    }
}

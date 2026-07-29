// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework
{
    /// <summary>
    /// A default implementation of IResumeManager. Loads a user specified
    /// image file (eg png) and draws it the middle of the screen.
    /// 
    /// Example usage in Game.Initialise():
    /// 
    /// #if ANDROID
    ///    this.Window.SetResumer(new ResumeManager(this.Services, 
    ///                                             spriteBatch, 
    ///                                             "UI/ResumingTexture",
    ///                                             1.0f, 0.01f));
    /// #endif                                         
    /// </summary>
    public class ResumeManager : IResumeManager
    {
        ContentManager content;
        GraphicsDevice device;
        SpriteBatch spriteBatch;
        string resumeTextureName;
        Texture2D resumeTexture;
        float rotation;
        float scale;
        float rotateSpeed;

        /// <summary>
        /// Initializes a new resume manager used to draw a resume indicator while content reloads.
        /// </summary>
        /// <param name="services">The game service provider.</param>
        /// <param name="spriteBatch">The sprite batch used for drawing.</param>
        /// <param name="resumeTextureName">The content name for the resume texture.</param>
        /// <param name="scale">The draw scale applied to the resume texture.</param>
        /// <param name="rotateSpeed">The per-frame rotation increment.</param>
        public ResumeManager(IServiceProvider services,
                             SpriteBatch spriteBatch,
                             string resumeTextureName,
                             float scale,
                             float rotateSpeed)
        {
            this.content = new ContentManager(services, "Content");
            this.device = ((IGraphicsDeviceService)services.GetService(typeof(IGraphicsDeviceService))).GraphicsDevice;
            this.spriteBatch = spriteBatch;
            this.resumeTextureName = resumeTextureName;
            this.scale = scale;
            this.rotateSpeed = rotateSpeed;
        }

        /// <summary>
        /// Reloads the configured resume texture from content.
        /// </summary>
        [RequiresUnreferencedCode("Content reload can resolve content readers and asset types through reflection.")]
        [RequiresDynamicCode("Content reload can activate content readers and asset types through reflection.")]
        public virtual void LoadContent()
        {
            content.Unload();
            resumeTexture = content.Load<Texture2D>(resumeTextureName);
        }

        /// <summary>
        /// Draws the resume texture centered on screen with rotation.
        /// </summary>
        public virtual void Draw()
        {
            rotation += rotateSpeed;

            int sw = device.PresentationParameters.BackBufferWidth;
            int sh = device.PresentationParameters.BackBufferHeight;
            int tw = resumeTexture.Width;
            int th = resumeTexture.Height;

            // Draw the resume texture in the middle of the screen and make it spin
            spriteBatch.Begin();
            spriteBatch.Draw(resumeTexture,
                            new Vector2(sw / 2, sh / 2), 
                            null, Color.White, rotation,
                            new Vector2(tw / 2, th / 2),
                            scale, SpriteEffects.None, 0.0f);

            spriteBatch.End();
        }
    }
}
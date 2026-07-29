namespace MonoGame.Framework.Utilities.StbImage
{
#if !STBSHARP_INTERNAL
	public
#else
	internal
#endif
	class AnimatedFrameResult : ImageResult
	{
		public int DelayInMs { get; set; }
	}
}
# Forma migration

The experimental retained-mode UI implementation formerly under
`MonoGame.Framework/UI`, its tests, and the `MonoGame.UI.Catalog` tool have
moved to the standalone [Forma repository](https://github.com/zigrok/Forma).

Applications should reference the `Forma` package together with exactly one
MonoGame backend package. Video-backed controls are available separately from
`Forma.Media`.

The fork-specific shader pipeline and `VideoPlayer.SetPlayPosition` support
remain part of this MonoGame fork; they were not transferred into Forma.
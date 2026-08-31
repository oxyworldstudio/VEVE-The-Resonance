# Render-Pipeline Migration Playbook (URP → HDRP)
**Companion to PipelineCompat (step D of "all the numbers"). The package is intentionally NOT force-installed via manifest because Unity 6.3 ships URP 17.x resolved only through the editor's release registry — public packages.unity.com tops out at 10.10.1.**

## Status now (committed, all green)
- `PipelineCompat` resolves the lit shader by active pipeline family through **asset type-name inspection** (zero compile reference to any SRP assemblies): `Universal Render Pipeline/Lit` when a URP asset is assigned, else `Standard`.
- `ApplySurface` writes additive-safe names for both worlds (`_Glossiness` built-in vs `_Smoothness` SRP, `_BaseColor` URP), so no authored material goes pink on flip.
- All scene-created materials (concrete/wood/steel/fabric) are built through the resolver.
- Built-in path keeps **exactly** today's behaviour until a pipeline asset is assigned (WebGL safety).

## To enable URP (editor session, 4 steps)
1. Package Manager → "Unity Registry" **or** bundled `com.unity.render-pipelines.universal` for 6000.3 (17.x).
2. `Graphics Settings ▸ Scriptable Render Pipeline Settings ▸ + New Universal Render Pipeline Asset (Forward)` + `Universal Renderer`.
3. Assign pipeline + renderer above; `QualitySettings` per level to the assets.
4. Nothing re-authoring needed: re-run `VEVE ▸ Rebuild Milestone Scene` to materialize resolver-built materials.

### Verification gate to run before/after (native, no editor clicks)
`Unity -runTests -testPlatform EditMode` must stay 356/356:
- `PipelineCompatTests.ActiveResolutionIsStableAndNeverThrows` will start asserting Universal family once assigned (no code edits)
- `PlayerGravityRegressionTests` still 9 green in play-scene build (physics pipeline-independent)

## HDRP route (consoles/PC only; WebGL will NOT support it)
Same resolver already returns `HDRP/Lit` when a HighDefinition asset is active; a separate PC/Console build target installs the package + assigns pipeline; `GraphicsSettings.currentRenderPipeline` switches shader selection automatically. Keep a WebGL **Built-in** target branch in CI if HDRP adoption becomes real.

## Post-process handover (next)
`PostProcessProfile` (project custom SO) has no runtime controller yet — URP Volume bridge = a follow-up where `UniversalAdditionalLightData`/`Volume` component mapping consumes existing fields (ACES/Bloom/DOF/Vignette/grain/CA already authored there). With pipeline still assigned: keep legacy `DiegeticReadout`/canvas HUD; after Volume bridge, disable `UrpVolumeBridge.enabled=false` to A/B.

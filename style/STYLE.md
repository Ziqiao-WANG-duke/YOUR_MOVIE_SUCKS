# YOUR MOVIE SUCKS! — Art & Audio Style Guide (Lab C)
**Track:** 3D · **Engine:** Unity 6000.3.8f1 · **Rendering:** URP 17.3.0 · **Platform:** Windows x64

## Creative direction
A first-person typing-based zombie shooter set in an underground strip club, referencing *The Typing of the Dead: Overkill* and the atmosphere of 1990s arcade horror such as *The House of the Dead*. The broader tone combines intentionally exaggerated B-movie performances, dark comedy, neon nightclub lighting and imperfect low-resolution 3D.

**Visual pillars:** (1) readable monsters and words at gameplay distance; (2) low-detail PS1-inspired geometry, distorted textures and controlled vertex snapping; (3) dramatic pink/cyan nightclub lighting, dark interiors and conspicuous practical stage fixtures; (4) lurid grindhouse blood and film aesthetics rather than realistic high-fidelity imagery.

## Asset production
- **Two enemy focus assets:** Bartender Zombie and Bikini Zombie. Gemini generated front/side/back turnaround imagery from references supplied by the creator; Tripo 3D generated the corresponding meshes. A third Gemini/Tripo asset is the pole-dance stage.
- The player is represented by a first-person camera, not a visible character model. The two enemies demonstrate the art pipeline, although this substitutes for the assignment's literal player + enemy specification.
- Generated character textures were either used substantially as produced or touched up in Substance 3D Painter. Some environment textures were processed manually in Photoshop. No Substance Painter before/after screenshot was retained.
- Mixamo auto-rigging and zombie animation assets were used for both enemies. Runtime locomotion and states were integrated in Unity.
- PS1 rendering came from a creator-directed pipeline: study of historical effects, consultation with a technical-art instructor, ChatGPT prompt specification, Astra shader code, and creator review. The first test prioritized vertex jitter/snapping and affine-like texture distortion on world objects, not UI.
- The environment was initially built with a coding agent, then manually adjusted for brightness, lighting, textures, and a more specific stage look.

## UI and VFX
Special Elite typewriter typography; contrasting Agent and Director modes; clear readable typing targets; film-strip health indicators with full/empty variants; camera framing and monochrome film effects in Director mode. AI-assisted UI concepts were cleaned in Photoshop and integrated with a Unity HUD editor. Its preview-to-game canvas scale mismatch required several corrective iterations.

Blood splatter, claw marks and other screen-space feedback support combat readability. The repository describes VFX enable/disable controls; the final game build must still be checked to ensure **three key-moment effects** are switchable.

## Sound and music direction
House music was chosen for the nightclub environment; B-movie cinema supplied the broader dramatic reference. Typing, gunfire and impact sounds were sourced from free-access online resources rather than AI generation. Exact original download URLs and licenses have not been recovered. The current soundtrack is a complete commercially released song, approximately four minutes long, not a cleared 30–90-second gameplay loop. Its use and public redistribution have not been licensed or verified; the creator has chosen to retain it in this interim coursework build. This is a **known unresolved issue**, not an assertion of permission.

## Voice direction
A friend volunteered to perform the Agent and Director voices. Agent: blunt, impatient, short phrases. Director: theatrical, self-important, amused by cinematic disasters. Repository documentation identifies thirteen scripted lines integrated with subtitles and ten additional reaction-line files not yet triggered. Actor consent, credit and permission to redistribute recordings should be confirmed.

## Acceptance checks
1. Words and enemy silhouettes remain readable during effects and under strong lighting.
2. World-space distortion never affects UI legibility.
3. Character animations are correctly skinned; no T-pose or disruptive root-motion drift.
4. HUD placement matches actual game resolution; blood overlays do not obscure key typing information.
5. Audio cues and captions match their triggers, with mute control confirmed in the delivered build.
6. Every incorporated third-party or AI asset has source, plan, rights and use status tracked in `ASSET_LOG.md`.

## Production evidence
The creator supplied three Gemini concepts, three Tripo previews, a Mixamo animation screenshot, early/current Unity screenshots, and multiple HUD iterations. The evidence currently resides in the conversation's supplied images; **image files still need copying to the repository's `style/refs/` directory** and attribution records. See `docs/LABC_VERIFIED_PROCESS.md`.

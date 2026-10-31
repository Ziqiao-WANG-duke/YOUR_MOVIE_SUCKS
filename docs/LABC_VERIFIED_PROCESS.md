# Lab C — Verified Production Record
**Project:** YOUR MOVIE SUCKS! · **Track:** 3D · **Updated:** 2026-10-31

This record supersedes conflicting *asset-provenance claims* in earlier drafts of `STAGE_LOG.md` and `PIPELINE_REPORT.md`. It is grounded in the creator's direct testimony, screenshots, and repository code. It does not replace technical build logs.

## Confirmed production workflow

| Group | Inputs, tools, and result | Actual | Author's score | Without AI / alternative |
|---|---|---:|---:|---|
| Concept art | Original *The Typing of the Dead: Overkill* design references → Gemini front/side/back sheets for bartender and bikini zombies; stage concept. The bikini original prompt was: “生成这个比基尼丧尸角色的三视图，三视图比例1920*1080，复古ps1风格”. | 60 min | 4/5 | ~240 min to draw two simple character sheets |
| Modeling | Gemini image references → Tripo 3D image-to-model generation for two zombies and pole stage. Original geometry mostly retained, appropriate to intentionally low-detail visual direction. | 30 min | 4/5 | ~240 min to find/adapt two existing PS1-style character models; stage excluded |
| PS1 shader | Technical-art research and instructor consultation → ChatGPT requirements prompt → Astra shader implementation. Core first-pass requirements: vertex snapping/jitter and affine-like UV distortion, world objects only, adjustable and readable. | 30 min | 5/5 | Creator cannot independently implement equivalent shader at present; **no numeric estimate** |
| Rigging + animation | Two generated enemy characters uploaded to Adobe Mixamo for auto-rigging; Mixamo zombie motions selected. Animation preview screenshot shows bartender model. | 10 min combined | 5/5 | ~10 min using Mixamo regardless of generative AI |
| Environment + lighting | AI coding agent generated initial nightclub setup, Tripo stage integrated, then creator manually adjusted lighting, darkness, and material brightness. | 8 min combined | 4/5 | Creator estimates manual setup would be **less than 8 min and visually better**; precise time unknown |
| UI / HUD | AI-generated UI directions → manual Photoshop clean-up → AI-assisted Unity integration and custom HUD editor. Preview/game canvas size mismatch required repeated rework. | 40 min | 3/5 | ~60 min for a basic hand-adjusted HUD; not functionally equivalent to custom editor |
| Integration / testing | AI coding agent helped integrate Unity functionality and debug; Windows standalone playtests with friends with varied typing skill. Qualitative feedback was positive for impact and play feel. | 180 min | 4/5 | ~180 min by creator estimate |

**Actual total for these seven groups:** 358 minutes (5h 58m). This is not total game development time.

**Comparable partial estimate:** Concept + modeling + Mixamo + basic UI + integration = **320 min actual vs 730 min alternative**, roughly **410 min (6h 50m) difference**. Not whole-project net savings: shader lacks a comparable baseline; scene may have taken longer through AI; manual HUD lacks equivalent tooling; stage modeling excluded.

**Reported spending:** one month Tripo ~RMB 80; Astra shader work ~$20 USD; DeepSeek-powered coding agent ~RMB 80. Report as **RMB 160 + USD 20**, without currency conversion. Other subscription costs and precise tiers are unknown.

## Evidence and limitations
- Gemini and Tripo screenshots, Mixamo animation view, initial vs current scene screenshots, and UI iterations were supplied in the ChatGPT conversation. They must be copied to `style/refs/` in this repository before the image references can be considered present in GitHub.
- The two enemy models are the two shown character hero assets: **no visible player mesh exists** in this first-person typing game. This departs from the assignment's literal “player + one hazard” phrasing and may require instructor approval.
- Screenshots show **combined art-direction changes**, not a controlled shader-on/shader-off comparison.
- No Substance Painter before/after capture was retained. Creator confirms some AI model textures were slightly retouched; many were used largely unchanged.
- The current Windows build is **not** a browser build; two-browser verification has not been conducted.
- Existing repository notes identify 13 scripted voice lines with captions, plus 10 reaction-line files not implemented. Runtime build validation remains necessary.

## Audio sourcing and licensing
The creator selected downloaded sound effects and an existing complete commercial house-music recording instead of generating music or voice with AI. Voice acting was contributed by a friend. The instructor reportedly allowed a reasoned non-AI route in class, although the written assignment asks for AI experimentation at each stage.

The original source and redistribution license for third-party SFX are unknown. The complete commercial track `bgm_show_me_love.mp3` is about four minutes and has **no verified redistribution license**, and therefore does not satisfy the specified 30–90-second looping-track duration. The creator has chosen not to replace it for this interim academic build. Academic purpose alone does not confer permission to redistribute it; the public repository's current and historical audio contents remain a rights issue. Do not describe this as cleared for distribution.

## Outstanding author verifications
- Exact tool plans and dates, plus missing per-stage times or ratings outside the seven confirmed groups
- Asset license/source links; friend voice actor's permission and attribution
- 6+ completed SFX through production stages, 3 switchable VFX, in-game mute and subtitles
- Final video actually comparing Lab B and Lab C
- The exact `lab-c` **lowercase hyphen** tag pointing to the final documentation-inclusive commit

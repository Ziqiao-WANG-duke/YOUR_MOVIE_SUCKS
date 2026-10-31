# YOUR MOVIE SUCKS! — Lab C Stage Log
**Track: 3D** · Author-verified details as of 2026-10-31. **Actual time** below is grouped where the creator estimated steps together; do not add overlapping groups twice. Date/plan fields not supplied are expressly **unverified**.

## 3D · Stage 0 — Art direction
**Tool/plan/date:** creator-led research, technical-art instructor consultation, ChatGPT; exact plan/date unverified.
**Brief/result:** retro arcade horror and *Typing of the Dead: Overkill* B-movie visual identity; nightclub House music and theatrical human voices. **Keep/redo:** retain strong visual style, manually adapt lighting and readability. **Time:** not separately estimated. **Rating:** not separately supplied. **Studio verdict:** AI can translate a precise brief, but artistic judgment remains human.

## 3D · Stage 1 — Concept art and turnarounds (Bartender, Bikini)
**Tool/plan/date:** Gemini; exact tier/date unverified. **Prompt, original (Bikini):** “生成这个比基尼丧尸角色的三视图，三视图比例1920*1080，复古ps1风格”. **Result:** front/side/back character design sheets for two enemy types; separate pole-stage concept.
**Keep/redo:** selected concepts used as Tripo 3D image inputs. **Actual:** 60 min. **Without AI:** ~240 min to draw two simple character sheets. **Rating:** 4/5. **Studio verdict:** rapid concept visualization works, subject to reference provenance and style review.

## 3D · Stage 2 — Modeling (two zombies and pole stage)
**Tool/plan/date:** Tripo AI; reported one-month subscription ~RMB 80; generation date unverified. **Brief/result:** image-to-3D from Gemini character sheets and stage reference; generated textured meshes.
**Keep/redo:** meshes largely retained because intentionally imperfect low-detail geometry fit the PS1 aesthetic; some textures later revised. **Actual:** 30 min. **Without AI:** ~240 min for sourcing/adapting **two character models** (not an estimate for rebuilding the stage). **Rating:** 4/5. **Studio verdict:** effective for stylized prototyping, but verify geometry/rights.

## 3D · Stage 3 — Texturing / retro shader
**Tools:** Substance 3D Painter (minor model texture corrections); Photoshop (environment textures); ChatGPT (technical brief); Astra (shader implementation, reported ~$20). **Brief:** subtle adjustable vertex snapping/jitter and affine-like UV distortion, restricted to world 3D assets and preserving typing readability. **Result:** URP retro environment/character shader pipeline found in `Assets/RetroRendering/`; development evolved beyond the initial two-feature test.
**Keep/redo:** kept integrated shader and mostly pre-existing texture bases; shader errors and iterations handled with AI agent. **Actual:** 30 min for ChatGPT+Astra shader work; independent texture-editing time unknown. **Without AI:** creator says they cannot currently implement this shader independently; **no numerical baseline**. **Rating:** 5/5 for shader work only. **Studio verdict:** a standout example of technical enablement; all results require in-engine review.

## 3D · Stage 4 — Rigging
**Tool:** Adobe Mixamo auto-rig. **Result:** both Tripo characters uploaded, fitted to Mixamo humanoid rig, imported to Unity. **Keep/redo:** animation preview confirms a working bartender character; rig marker placement was not separately captured.
**Time:** part of combined rigging+animation **10 min**, not 10 min per stage. **Without generative AI:** same 10 min using Mixamo. **Rating:** combined 5/5. **Studio verdict:** mature standardized auto-rigging is useful independently of generative AI.

## 3D · Stage 5 — Animation
**Tool:** Mixamo, including zombie motion pack; Unity runtime Animator setup. **Result:** enemy motions applied; screenshot shows a bartender animation preview. **Keep/redo:** reused the library rather than authoring custom keyframes.
**Time:** shared combined 10 min above. **Rating:** combined 5/5. **Studio verdict:** fast for placeholders or stylized production; inspect retargeting, loops and hit timing.

## Shared — Environment art and lighting
**Tool:** AI coding agent for initial nightclub geometry/lights; creator imported generated stage and manually corrected lighting, brightness and texture values. **Result:** neon-lit strip-club arena; screenshots show significant early-to-later visual changes. **Keep/redo:** original setup was substantially adjusted by hand.
**Actual:** 8 min for combined environment+lighting AI-agent work. **Without AI:** creator believes manual setup would take **less than 8 minutes and look better**, no exact estimate. **Rating:** 4/5 reported for this combined group, while creator judges this particular AI contribution low-value. **Studio verdict:** poor fit for simple personally directed staging.

## Shared — UI art and HUD editor
**Tools:** AI concepts, Photoshop cutouts/transparent-background cleaning, AI code agent. **Result:** Agent/Director UI variations, film-strip HP visuals, in-editor controls for position/size/opacity and screen effects. **Keep/redo:** preview resolution/aspect mismatch caused repeated corrections before successful alignment.
**Actual:** 40 min. **Without AI:** ~60 min for a **basic manually laid out HUD**, not equivalent to a custom editor. **Rating:** 3/5. **Studio verdict:** quick first draft, but tooling assumptions must be validated visually.

## Shared — VFX
**Tools:** Unity shaders, UI textures, AI coding agent. **Result:** blood splashes following kills, claw damage overlay, retro/Director film effects and feedback. **Keep/redo:** creator-adjusted layout via HUD editor.
**Time and separate score:** not provided; do not reuse shader/UI group minutes. **Studio verdict:** successful for implementation, with a required final check of three independently switchable gameplay effects.

## Shared — Integration and review
**Tools:** AI coding agent (DeepSeek-powered, reported spend ~RMB 80), Unity test/batch workflow. **Result:** integrated Windows x64 build; friends of varied typing skill reported satisfying impact and play feel. **Keep/redo:** bugs were iteratively fixed, including HUD editor alignment.
**Actual:** 180 min. **Without AI:** ~180 min. **Rating:** 4/5. **Studio verdict:** useful for code iteration and troubleshooting, but no claimed overall time saving.

## Sound effects · Stages 1–5
**1 Spotting:** typing, mode switch, gunfire, damage, plus proposed additional cues. **2 Sourcing:** freely accessible web downloads; sites/authors unknown. **3 Editing/design:** repository describes trimming, short fades and sound pools. **4 Implementation:** `TypingSfxPlayer`, `PlayerHurtSfx`, `DemoAudioDirector`. **5 Mixing:** documented game volume and dialogue ducking.
**AI choice:** creator deliberately preferred sourced audio and reported teacher's in-class allowance to explain non-AI selection; no fabricated AI experiment. **Separate time/rating:** not supplied. **Rights:** not verified; audio file count is not necessarily six distinct key moments. **Studio verdict:** human listening and licensed source selection essential.

## Music · Stages 1–4
**1 Direction:** House music for nightclub. **2 Production:** existing complete commercial recording, not authored or AI-generated. **3 Mixing:** imported/converted music and ducking described in existing sound list. **4 Implementation:** `Assets/Audio/BGM/bgm_show_me_love.mp3`, ~4 minutes.
**Keep/redo:** retained for interim class build at creator's request; licensing unknown and assignment's 30–90-second loop unmet. **Separate time/rating:** not supplied. **Studio verdict:** source and license checks must precede distribution.

## Voice · Stages 1–6
**1 Writing:** B-movie comic Agent/Director dialogue. **2 Casting:** volunteer friend as actor. **3 Scratch:** unknown/not separately evidenced. **4 Final recording:** friend-recorded performances. **5 Editing:** recording processing/import and timing adjustment documented in `docs/voice-script.md`. **6 Implementation+captions:** existing docs show 13 scripted captioned lines; 10 additional files not connected to triggers.
**AI choice:** human performance preferred, without a purported AI-generated voice test. **Separate time/rating:** not supplied. **Studio verdict:** expressive direction and consent/credit are human responsibilities.

## Quantitative summary / honest gaps
Seven creator-confirmed groups totaled **358 min (5 h 58 min)**: concepts 60, models 30, shader 30, Mixamo rig+animation 10, environment+light 8, UI+HUD 40, integration+test 180. Five partially comparable groups: **320 min actual vs ~730 min alternative**, difference **~410 min (6 h 50 min)**. This is **not overall net savings**; shader and environment lack numeric counterfactuals and UI scope differs. **Known expenditure:** ~RMB 160 plus ~$20 USD, other plans unspecified.

Still to verify: third-party rights, actual final runtime quality, dates/plan tiers, per-stage metrics not collected, and the instructor's approval of two enemies instead of player+enemy and Windows build rather than browser version. See `docs/LABC_VERIFIED_PROCESS.md`.

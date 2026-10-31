# YOUR MOVIE SUCKS! — Lab C Pipeline Report
**3D Track · Unity 6 / URP · Author-verified review, October 31, 2026**

## Pipeline assessment
The game extends a typing-shooter prototype into a stylized 3D nightclub zombie encounter. My visual references were *The Typing of the Dead: Overkill*, PS1-era rendering artifacts and exaggerated B-movie filmmaking. I developed two enemy concepts (bartender and bikini zombies) with Gemini, generated 3D models and a pole stage using Tripo, used Mixamo for rigging/animation, and made targeted manual texture, lighting and UI corrections. ChatGPT translated my shader requirements into instructions for Astra; an AI coding agent implemented and integrated systems within Unity.

| Stage / group | AI usefulness (creator score) | Actual time | Traditional alternative |
|---|---:|---:|---|
| Gemini character concepts | 4/5 | 60 min | ~240 min manual drawing |
| Tripo 3D models | 4/5 | 30 min | ~240 min adapting 2 existing models |
| ChatGPT + Astra PS1 shader | 5/5 | 30 min | Not feasible independently at current skill level |
| Mixamo rigging + animation | 5/5 | 10 min | ~10 min with same established tool |
| AI agent environment + lighting | 4/5 | 8 min | Less than 8 min manually, creator estimate |
| AI-assisted UI + HUD editor | 3/5 | 40 min | ~60 min for simpler manual HUD |
| AI-assisted integration + tests | 4/5 | 180 min | ~180 min |
| Other VFX, separate texturing, SFX, music, voice | Not separately rated | Not tracked | Not estimated |

**Recorded time:** 358 minutes across the seven groups above; this is not total production time. Across five partially comparable groups, ~320 minutes with tools versus ~730 minutes estimated alternative: **~410 minutes (6h 50m) difference**, not net whole-project savings. Shader has no numeric baseline, environment may have cost time, and a manual UI lacks equivalent editor functionality. **Reported expenses:** Tripo ~RMB 80 for a month, DeepSeek-powered coding agent ~RMB 80, Astra shader work about USD 20; no exchange-rate conversion or inferred spending for unreported tools.

## Where AI was most valuable
**1. Shader enablement (5/5).** I researched vertex snapping and distorted texture mapping with help from my technical-art instructor, described my goals to ChatGPT, and gave the resulting technical brief to Astra. This provided a functioning PS1-like rendering direction that I could not independently program with my present shader experience. The value was capability, not just speed.

**2. Concept development (4/5).** Gemini produced two usable character turnarounds in one hour rather than my estimated four hours of drawing. The concept sheets provided consistent references for the next stage, although they still required selection and visual review.

**3. Model generation (4/5).** Tripo converted those sheets into assets in about 30 minutes. The intentionally imperfect, low-detail appearance suits this game's retro visual goals. This would not imply equal suitability for clean high-fidelity hero models.

## Where AI helped least
**1. Environment/lighting.** Despite the combined 4/5 score for output usefulness, my simple initial nightclub would likely have been faster and more visually effective to build manually. I refined lighting, values and textures myself.

**2. HUD editor (3/5).** AI generated a useful custom editing interface, but its preview-to-game scale mismatch required multiple rounds of corrective prompting. The modest 20-minute time difference against manual HUD layout does not compare identical features.

**3. Integration/testing time (4/5).** The agent helped solve technical problems and automate work, but I estimate similar total time without it. Mixamo was excellent (5/5) but is an established non-generative animation workflow I would have used anyway, so it is not evidence of savings from generative AI.

## Human decisions, constraints, and producer recommendation
I used web-sourced sound effects and a friend's performances rather than AI-generated audio, because I preferred their character and fit. The instructor reportedly allowed explanation of this choice in class. The repository currently documents 13 integrated captioned dialogue lines and 10 additional reaction recordings not yet triggered. The approximately four-minute soundtrack is a complete commercial recording; permission to distribute it has **not** been verified, and it does not meet the brief's 30–90-second loop. Free-access sound sources also lack recovered licenses. This is an unresolved rights and compliance issue, including for the public GitHub history, not cleared by educational intent.

The game is a Windows standalone build rather than a verified two-browser experience. The two enemy assets replace a visible player asset because gameplay is first-person; teacher acceptance of both deviations should be confirmed. Evidence images and complete asset provenance must still be placed in the repository.

**If I led a five-person studio,** I would use AI for early concepts, low-detail asset prototypes, shader/code drafting, and mechanical integration, with human art direction and in-engine checks. I would not trust AI-generated UI tooling without pixel-accurate validation, nor use unverified audio rights or generated asset provenance without records. My central lesson is that AI is most useful when judged against the actual task, not when its contribution is automatically treated as a time saving.

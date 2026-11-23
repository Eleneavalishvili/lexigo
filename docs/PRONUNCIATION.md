# Local reference pronunciation

Reference words and sentences are generated locally with Piper 1.8.0, then served as static WAV assets. No cloud speech subscription, paid quota or learner recording upload is involved. Reference audio does not itself provide pronunciation assessment or AI conversation.

`config/voice-models.json` pins the official repository revision and SHA-256 of each model and configuration. Preserved source model cards are in `docs/source/pronunciation`. Voices: English LJSpeech (public domain), Spanish Davefx (CC0), Italian Paola (CC0 dataset), German Thorsten (CC0), Russian Denis (CC0). Italian dataset source: https://huggingface.co/datasets/paolapersico1/Voice-Dataset-Italian . The model cards and dataset attribution should remain with distribution. Piper software has its own GPL licensing, separate from voice/data terms; model weights and the generator environment are not bundled in the website.

Generation source: `tools/pronunciation/generate.py`, with pinned environment packages in `requirements.lock`. The public manifest contains text-to-file references, never exercise IDs or private answer-key mappings. Files have restricted hashed basenames. Source content is already public teaching material. Unanswered API exercise DTOs remain private-key-free.

```powershell
./work/tts-venv/Scripts/python.exe tools/pronunciation/generate.py
```

The generator verifies downloaded model checksums and reuses existing WAVs. The browser prefers course-language clips and falls back to English for English content. It stops previous playback, handles blocked/missing audio honestly, respects sound preferences for automatic selection playback and supports an explicit Listen action. Speech animation follows actual audio playing/ended events; it is not phoneme-level lip synchronization.

Listen controls cover teaching cards, explanatory examples, readings and revealed corrections. Selecting choices or sentence tiles can play the selected reference. Pip the fox accompanies Spanish/German and Sol the otter Italian/Russian. Reduced motion remains supported. Initial service tests verify overlap/stale events, language fallback and rejecting unsafe asset paths. Real-device listening and independent pronunciation quality review remain required.

## Learning pace update

Reference playback defaults to0.8x with a0.65x Slowly replay; pitch is preserved. Protected lesson recordings also default to0.8x. This addresses the user’s speed complaint; it does not establish independent linguistic accuracy. Frontend219tests and productionbuild pass.

## Latest pace preference — 2026-11-02

User found the slowed default excessive. Normal playback is now1x; reference Slowly is0.85x. Protected recordings also default1x, with slower manual options. This supersedes the earlier0.8x/0.65x defaults. Recordings/voices have not been replaced or independently validated; no100%accuracy claim. Target-script teaching and live transcript display now accompany playback.231frontendtests/buildpass.

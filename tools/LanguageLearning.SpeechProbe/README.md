# Local spoken-word acceptance probe

This tool checks local Vosk recognition against explicit test fixtures. It does not score pronunciation or grant lesson completion/XP. It never calls a cloud recognizer. Only test transcripts are printed.

From the repository root:

1. Run `./scripts/Initialize-LocalSpeech.ps1` to install the four pinned free models.
2. With Python available, install test-only Ogg decoding into the ignored workspace: `python -m pip install --target work/speech-test-python soundfile==0.13.1`.
3. Run `python tools/LanguageLearning.SpeechProbe/prepare_human_fixtures.py`. This downloads eight openly licensed public recordings with pinned hashes, resamples them and records attribution/modification receipts under `work/speech-human`. It respects HTTP 429 backoff and refuses changed files. The fixture decoder is not a production audio-upload decoder.
4. Run `dotnet run --project tools/LanguageLearning.SpeechProbe -- <absolute-work/speech-models-path> <absolute-work/speech-human-path>`.

Expected output: four correct phrases recognized, four different words rejected and four noise clips rejected. Exit 0 means these specific checks passed; any failure returns 1. This small sample is not a broad accuracy benchmark. No expected-answer grammar is supplied to the recognizer. Public sample authors/licenses are in `config/speech-test-fixtures.json`; converted fixtures retain their source licenses and are not published as reviewed LexiGo curriculum.

`cases.json` can supply explicit known target phrases for the local test fixture set. User recordings, model paths and expected answers must never enter this console tool through a public API. Runtime lesson integration needs ownership checks, bounded requests, server-derived targets, idempotent writes, explicit recording consent and approved retry/scoring policy.

# Free local speech recognition trial

Checkpoint: 2026-10-28. Subsequent human-recording acceptance checks pass in all four languages. This is an internal implementation/quality record, not a completed pronunciation feature.

## User decision and limits

The user requires zero spending and authorized testing local speech recognition. No Azure account, cloud speech API, paid trial, subscription or external audio transfer is used. Vosk recognizes words; it does not establish phoneme-level pronunciation accuracy. Testing word recognition does not remove the original Speaking self-assessment requirement or the requested in-lesson character/speech interaction. Neither a recognized transcript nor a microphone-completion Boolean is a pronunciation-quality score.

## Installed foundation

Vosk .NET 0.3.38 is explicitly pinned, with backend dependency lockfiles. Four small Apache-2.0 models are listed with official HTTPS URLs, versions, sizes and observed SHA-256 hashes in `config/speech-models.json`. The initial downloads were made from the official model site; these hashes pin those downloaded artifacts and are not an independently signed upstream attestation.

Run `./scripts/Initialize-LocalSpeech.ps1` from the project root. Models and downloads stay in ignored `work/`. Setup refuses links/path traversal/foreign hosts/redirects, checks archive hashes, bounds downloads and expanded data, refuses ZIP symlinks and unsafe entries and never overwrites an existing model. Repeated setup verified matching receipts and preserved all four existing models. Receipts establish archive provenance at setup; they do not continually verify model files against local tampering. Only trusted operators may change installed models. Failed temporary downloads are retained in ignored work for inspection and are never treated as installed models.

`LocalSpeechRecognizer` accepts canonical mono 16-bit PCM WAV at 16 kHz, at most 30 seconds. Existing WaveAudioInspector validates bytes and removes metadata. Silent clips do not become correct answers. Supported language codes map to fixed directory names; no supplied model URL/path is accepted. One recognition runs at a time with no waiting queue, and only one language model is cached. Cancellation is checked between audio blocks. Input/output remain in memory; the service does not log or save learner audio/transcripts and contains no cloud fallback. It is disabled by default and is not registered in the public lesson flow.

## Native recognition experiment

The separate `tools/LanguageLearning.SpeechProbe` console tool runs explicit synthetic fixture WAVs. Run with two absolute paths: the installed model root and fixture directory. It expects `es-16k.wav`, `it-16k.wav`, `de-16k.wav`, `ru-16k.wav`. Its JSON output deliberately identifies synthetic transcripts. It exits nonzero if any expected phrase fails, rather than reporting model loading as learning-quality success.

Fixtures were generated using eSpeak-ng 1.51 in a disposable Debian bookworm-slim container (image digest sha256:99c0b25c46d0214d5a2091092e7908907dcaecd6a962cde0ac700acee222b6f3), then converted from 22.05 kHz to mono 16-kHz PCM using Python 3.12 audioop. These are machine-generated smoke inputs, not licensed real course recordings or a human/accent accuracy benchmark. They are kept only under ignored `work/speech-fixtures`.

| Language | Expected synthetic phrase | Actual first transcript | Exact phrase match |
| --- | --- | --- | --- |
| Spanish | hola buenos días | hola buenos días | Yes |
| Italian | ciao buongiorno | ciao buon giorno | No: segmentation difference |
| German | guten morgen | du zur morgen | No |
| Russian | привет доброе утро | по имеет потом и принтера | No |

All four real native models loaded and returned text. The expected unrelated phrase did not match any transcript. The recognizer received no expected-answer grammar/hints: restricting it to the correct phrase would risk manufacturing a match. Do not alter matching tolerances or expected fixtures merely to hide these results. Legitimate written alternatives need reviewed language content; German/Russian errors still require investigation with real voices.

## Checks and next gate

Fifteen LocalSpeechRecognizerTests cover fixed mappings/path substitution, disabled/unavailable behavior, accepted audio profile bounds, silence and cancellation. The full backend suite passed 265 tests with zero failures/skips. The separate native speech-quality experiment failed phrase acceptance for three of four synthetic cases; this is a distinct unresolved quality gate, not a passing feature test.

Required next steps: test consented real microphone recordings across all four languages, identify synthesis versus recognizer limitations, validate short-word/accent/noise/silence/wrong-phrase behavior, and compare another genuinely local engine if accuracy remains inadequate. Only after that wire owned session uploads, strict audio/request limits, server-derived expected phrases, idempotent trusted answer writes, retries/heart/XP policies, browser PCM capture, visible listening/processing/feedback character states and device checks. Preserve old ungraded Speaking history. No UI control, API endpoint, active lesson grading or public deployment was added by this trial. Native dependency/source-security review and isolated worker resource/timeout containment remain release gates.

Sources: [official model catalogue and licenses](https://alphacephei.com/vosk/models), [official install documentation](https://alphacephei.com/vosk/install), [official C# example](https://github.com/alphacep/vosk-api/blob/master/csharp/demo/VoskDemo.cs), [NuGet package](https://www.nuget.org/packages/Vosk/0.3.38).

## Second local comparison

Whisper.net/Whisper.net.Runtime1.9.1 and multilingual ggml-tiny were tested only inside ignored work/whisper-probe, not added as production dependencies. Model source: ggerganov/whisper.cpp on Hugging Face, ggml-tiny.bin, observed SHA-256 be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21, 77,691,713bytes. The fixed-language probe used the same synthetic WAVs and no expected-answer prompt/grammar. It returned Spanish “¡Oga, venos!”, Italian “Ciao buon giorno!”, German “Guten Morgen.” and Russian “Время и оттворена янтера.” German improved, other exact targets did not match. Vosk's Russian output also varied between runs. Neither engine is accepted for lesson grading from this trial; synthetic results cannot establish human recognition quality or isolate synthesis/model failures. No audio left the computer. Comparison logs: work/whisper-comparison.log and work/speech-native-probe-final.log. The Vosk quality probe returns exit1 rather than hiding failed matches.


## Human speech follow-up: a viable free word-checking solution

Replaced reliance on synthetic voices with eight openly licensed human pronunciation files from Wikimedia Commons. Exact source URLs, source SHA256, authors where stated, uploader credits, licenses and conversion notices are retained in config/speech-test-fixtures.json. Human test recordings are not public course content or independent curriculum approval. The initial synthetic failures remain documented above; the successful human cases are additional evidence, not a claim that every synthetic failure's cause is known.

Vosk correctly recognized hola, ciao, guten morgen and привет without expected-answer hints. It also transcribed and rejected adiós, grazie, danke and спасибо when checked against those different target phrases. Four seeded noise clips returned no recognized speech. All12acceptance checks passed, exit0 (work/speech-human-reproducible.log). Repeat preparation uses tools/LanguageLearning.SpeechProbe/prepare_human_fixtures.py and pinned source hashes. HTTP429 during initial sample retrieval was respected with backoff/spaced requests; no account or payment was used. The same four positive human samples also produced correct words through Whisper tiny, with punctuation/capitalization differences. Vosk remains the prepared backend choice; Whisper stays an isolated comparison.

Added browser prepareSpeechWave/encodeSpeechWave: bounded recorded blob decoding through OfflineAudioContext at16kHz, mono/stereo mixing, finite-value validation, 0.1–30second bounds and canonical16bitlittle-endian metadata-free WAV. It opens no microphone or audio output and performs no upload. Abort checks prevent late decoded results being used after cancellation. Six meaningful WAV/conversion cases plus prior tests pass:131frontend tests/34files, production build and Prettier. Existing265backend tests remain valid; no backend business rule changed during this follow-up.

Remaining: consented real browser microphone/accents/device/noise testing, session-owned uploads, private server-derived target phrases, trusted idempotent success writes and character listening/checking/feedback integration. A question is pending about free retries versus heart loss; no dependent scoring policy was silently selected. Precise pronunciation scoring is still not implemented or claimed. Twelve checks are a small feasibility set, not broad accuracy proof or a production release gate waiver.

## In-lesson integration — October28–29

The retry rule was resolved by the user: first mismatch free, subsequent mismatches cost1heart, technical/no-speech checks uncharged. The new SpeakingCheck format, owned multipart endpoint, atomic persisted evidence/replay/heart decisions, server-derived private targets, explicit recorder submission and Pip/Sol activity states are now implemented. Local development models are enabled through User Secrets; other environments remain disabled by default. See SPOKEN_WORD_CHECKS.md for exact behavior, setup, privacy and open device/curriculum/production gates. Earlier experiment results above remain historical evidence; their statements that integration was pending describe those earlier trials. Precise pronunciation scoring is still not claimed.

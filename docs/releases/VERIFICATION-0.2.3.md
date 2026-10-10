# 0.2.3 verification

- Core: 57 tests passed. Includes near-square dimension policy, truncated inputs, source candidate filtering, automatic-cover metadata preservation and persisted manual-cover protection.
- Windows: Release publish passed; WPF regression passed for long-title fixed width / no horizontal editor scroll and dark→light→dark clean state, plus prior tag save, selection, settings routing and scrollbar regressions. Rendered screenshots reviewed in artifacts/qa-0.2.3-final.
- Android: assembleDebug, assembleDebugAndroidTest and 15 JVM tests passed. Connected device instrumentation passed: TagEditorUiTest and ArtworkPolicyTest (2 tests). Artwork test decodes actual PNG images at 233×217, 475×500 and 1280×720.
- Live unauthenticated yt-dlp lookup of ENDROLL -HaThA- (_m8VuLJqGxA) returned Video unavailable. A live cover for that particular track was not verified; no claim that every track can be matched.
- MusicBrainz matching/fallback validated with deterministic HTTP responses. No claim of a complete fresh network download of every supported format in this UI/artwork release.
- Final Android build has the existing experimental path-check warning. The earlier manifest extractNativeLibs warning is unchanged.

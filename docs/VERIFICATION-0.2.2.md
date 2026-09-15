# 0.2.2 verification

- Windows Release build and portable publish succeeded.
- WPF regression passed: real MP3 tag save, invalid-title guard, optional rename, return to the same tag editor, filtered select-all / clear, conditional song checkboxes, equal-width summary, multi-row library selection.
- Scrollbar tested with 24 imported fixture songs: visible hit-testable thumb and routed drag events changed the song ScrollViewer offset. This is a control-level regression, not a physical mouse automation claim.
- Rendered Windows screenshots reviewed for library and full-width settings.
- Android assembleDebug / assembleDebugAndroidTest / JVM unit tests passed (15 tests, no failures).
- Connected Android device: TagEditorUiTest passed (1 test), including row-click selection and actual metadata save without rename.
- Existing Android manifest extractNativeLibs / experimental path-check build warnings remain; no Kotlin compiler warnings in final build.
- Native downloader/network behavior was not changed and was not re-tested end to end in this release.

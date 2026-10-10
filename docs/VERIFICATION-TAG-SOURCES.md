# Saved tag-editor sources — 2026-10-10

The tag editor restores all saved file/folder sources plus completed audio in download history. Copies under separate Desktop and Downloads paths are distinct files; importing a new folder does not replace previously remembered sources.

Windows now lists persisted full paths in **Manage added sources** and lets users forget a source without deleting files or history. The manager count and visible songs refresh immediately; the removal survives navigation and reopening the store.

Verification used synthetic MP3 fixtures in isolated Desktop/Downloads directories, never personal music. Four focused Core tests passed. A real WPF regression checked both source paths, selective removal, immediate count/row updates, navigation persistence, unchanged download history, and unchanged audio SHA-256 values. Rendered layouts were inspected at 1440×940 and 1050-wide.

```powershell
dotnet test tests/Core.Tests --filter FullyQualifiedName~TagLibraryTests
dotnet run --project tests/Windows.Smoke -- artifacts/qa-source-management artifacts/windows-win-x64 --source-management-regression
```

This records a local development change, not a public release or an Android change.

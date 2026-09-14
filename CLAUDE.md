# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

`normalize-text` is a Windows CLI utility (C# / .NET 10) that normalizes text files: validating/ensuring UTF-8 encoding, stripping the BOM, and enforcing LF line endings. It's invoked with one or more explicit file/folder paths as CLI arguments, or via files/folders dropped onto its executable from Windows Explorer.

## CLI behavior

Default (no flags) processing, per file:
1. Verify the file is valid UTF-8 text (BOM-having or not). Binary/non-text files (`DetectedEncodingKind.Binary`) are skipped silently — no per-file warning, since it's pointless noise when binaries are mixed in with text files being cleaned up. Text in another encoding (UTF-16/32 BOM, or invalid-as-UTF-8 bytes with no BOM — likely legacy ANSI/Windows-1252) is skipped *with* a warning naming the file (path relative to whatever target produced it, not absolute — see below) and its detected encoding (`DetectedEncodingKind.ToDisplayName()`), since that's actionable. Either way, one bad file never aborts the run, and both kinds still count toward `Files skipped` in the summary.
2. Strip the BOM.
3. Normalize line endings to LF.

Arguments:
- One or more positional file/folder targets. Folders recurse via a hand-rolled walk (`FileProcessor.EnumerateFilesExcludingVcsDirectories`) rather than `Directory.EnumerateFiles(..., AllDirectories)`, specifically so `.git` and `.svn` directories can be excluded — always, unconditionally, no flag to turn it off. Each skip is logged (`Skipping version-control directory: '<path>'.`, suppressed under `--silent`) and counted in `DirectoriesSkipped`.
- **All paths in warning/error messages are relative to whichever target produced that file/directory** (`FileProcessor.RelativeToRoot`, reusing the exact same `ResolvedFile.Root` already computed for backup/output placement) — never the full absolute path, even when the target itself was passed as an absolute path. Drop a folder at `C:\...\FooBar` and a bad file inside it reads as `Sub\Example.cs`, not the full path repeated on every line. For a directly-named single-file target, `Root` is that file's own parent directory, so its relative path is just the bare filename — this generalizes uniformly rather than special-casing "was a folder targeted." The one exception: an OS-level exception's own `.Message` (e.g. `IOException`/`UnauthorizedAccessException` text) still contains whatever path .NET put there, untouched — only the `'<path>'` file identifier we construct ourselves is relativized.
- `--eol <ignore|lf|crlf>` — default `lf`.
- `--bom <ignore|strip|force>` — default `strip`.
- `--output <path>` — single arg, interpreted contextually by `FileProcessor`:
  - One resolved input file: `--output` may be an existing directory (write under the original filename) or an exact destination file path.
  - Multiple resolved input files: `--output` must be a directory (created if missing). If it's an existing file, that's a **misuse error** — the run fails fast with a message naming the conflict, without processing any file.
  - Omitted: normalize in place — but only for files that actually need a change. A file already conforming to the target BOM/EOL settings is left completely untouched (no rewrite, no mtime change, no backup) and still counts toward `Files processed`, just not `Files modified`. A file that does need changing is backed up first (see Backups below) unless `--no-backups` is set, then overwritten.
- `--no-backups` — disable the automatic backup described below.
- `--silent` / `--quiet` — suppress per-file skip warnings, the backup-location line, and the end-of-run summary.
- `--help` / `-h` / `/?`.
- `--version` / `-v` — prints `<Version>[+<git-sha>] (built <exe-mtime-UTC>)`, e.g. `0.1.0+31d68a7… (built 2026-09-12 09:25:46 UTC)`. See Versioning below.

### Versioning

`<Version>` in `NormalizeText.csproj` is the only thing manually maintained (currently `1.0.0`) — everything else is free. The .NET SDK automatically appends `+<full-commit-SHA>` to `AssemblyInformationalVersionAttribute` at build time whenever the project sits inside a git repo with at least one commit; no SourceLink package, no code, nothing to configure. With zero commits, it falls back to plain `0.1.0`, no `+` suffix. `Program.cs`'s `BuildVersionString()` reads that attribute via reflection and appends the *running exe's own file mtime* (not a compile-time constant), which answers "is this actually the copy that was just published/deployed" for a self-contained single-file exe — exactly the ambiguity `DeployToLocalPrograms` could otherwise leave you second-guessing.

This means a plain local build/clone never shows a fake `0.0.0` (the csproj's committed `<Version>` is the floor), and never claims to *be* an official release either — the appended commit SHA makes clear it's "whatever commit was checked out," not "release 0.1.0."

`.github/workflows/release.yml` handles tagged releases: pushing a tag matching `v*.*.*` (e.g. `v1.0.0`) triggers a workflow that runs the test suite, strips the leading `v` from the tag, publishes with that exact version as a one-off `-p:Version=X.Y.Z` override (a build-time argument, not a file CI writes back to the repo), and uploads the resulting `normalize-text-<version>-win-x64.exe` to a GitHub Release created from that tag via `gh release create --generate-notes`. A release build's version therefore always matches its tag exactly, with no `+<git-sha>` suffix (the working tree at a tag *is* that exact commit, so the suffix would be redundant) — that suffix only ever appears on a plain local build/clone, which is exactly the case it exists to identify.

**The workflow does not write the version back to the repo** — bump `<Version>` in `NormalizeText.csproj` yourself, in the same commit that precedes the tag push, whenever a new release is about to go out. Skipping this doesn't break the release itself (the tag always wins for the published binary), but it leaves local/dev builds reporting a stale version until the csproj catches up.

### Backups

Whenever `--output` is omitted — a single file, multiple files, or a folder, doesn't matter — `FileProcessor` backs up each **modified** file's original bytes before overwriting it (an already-conformant file is never backed up, since it's never overwritten — see the `changed` check in `FileProcessor.Run`), under `%LocalAppData%\Strange\NormalizeText\Backup\<yyyy-MM-dd HHmmss-fff>\`. The `Strange\` prefix matches the same `Company` used in assembly metadata and other tools' own local-data folders — unlike the PATH-deploy target (`Programs\NormalizeText\`, no company prefix, since `Programs` itself is already Windows' namespacing convention for per-user installed exes), a bare data-storage folder directly under `LocalAppData` benefits from the extra namespace level to avoid colliding with unrelated software. A folder target's subfolder structure is preserved relative to that folder (drop `bar` containing `bar\baz\x.txt` → backed up at `<session>\baz\x.txt`); direct file targets land flat under the session folder. The millisecond in the folder name is there so two runs never collide — there's deliberately no exists-check/retry loop. The session folder is created lazily (only once a file that actually needs backing up is hit), so an all-skipped run or an empty folder never leaves a stray empty backup folder. `FileProcessor`'s `backupRootDirectory`/`clock` constructor params exist purely so tests can redirect backups away from the real `LocalAppData` and pin the timestamp — don't remove them for being "unused" in production code.

Fatal errors (bad args, a target that doesn't exist, the `--output` misuse case above) always print to stderr and set exit code 1, **even under `--silent`** — silence only suppresses the informational warnings/summary for an otherwise-successful run, never the reason a run didn't happen.

Per-file I/O failures (permission denied, file locked, etc. — `IOException`/`UnauthorizedAccessException`) are caught around both the read and the backup+write per file in `FileProcessor.Run`, so one inaccessible file never crashes the whole batch. Each is recorded in `FailedFiles`/`FilesFailed` and printed to stderr (`Error: failed to process '<relative-path>': <message>`) **unconditionally, even under `--silent`** — unlike encoding-skips, an unexpected I/O failure is treated as error-tier output, not routine noise. If backup already succeeded for that file before the overwrite failed, `FilesBackedUp` still counts it (the backup is real and independent of whether the in-place write later failed). A run with any failed file exits 1 even though `outcome.Success` is `true` (that flag means "setup was valid enough to attempt the batch," not "every file succeeded").

**`FilesProcessed` vs `FilesModified` vs `FilesBackedUp`** — these are deliberately three separate counters, not aliases or a strict "processed minus skipped = modified" relationship:
- `FilesProcessed` — every file actually examined (i.e. resolved and encoding-checked), regardless of outcome. Includes skipped and failed files; excludes only files inside a skipped `.git`/`.svn` directory (those were never looked at, so they were never "processed" either).
- `FilesModified` — files whose content actually changed (`result.BomChanged || result.EolChanged`) and were successfully written, wherever they were written to (in-place or `--output`).
- `FilesBackedUp` — files copied to the backup session directory. Only ever non-zero when `--output` is omitted (backups don't apply to `--output` at all) *and* the file was actually modified — an already-conformant file is never backed up, because it's never touched.
- The remainder — `Processed - Skipped - Failed - Modified` — is valid UTF-8 text that already conformed and needed no write at all. There's no dedicated counter for this bucket; it's implicit.

Under `--output`, an already-conformant file is still copied to the destination (that's the point of `--output` — producing output at a new location) but does **not** count toward `FilesModified`, since its bytes didn't change, only its location did.

Unless silent, the whole informational block (target echo through the summary) is bracketed by a leading and trailing blank line — separating it from the invoking command and the next shell prompt, matching how a real terminal session reads. This bracketing lives outside `PrintSummary` (in `Program.cs`'s main flow), so it's skipped entirely under `--silent` rather than leaving two stray blank lines with nothing between them.

Unless silent, the leading blank line is immediately followed by `PrintTargets` echoing back `options.Targets` — the exact strings passed on the command line, unresolved — as `Processing: <target>` for one target, or `Processing:` plus one indented line per target for several. In an interactive terminal this looks redundant (the invoking command is right there in scrollback), but it's the only context a freshly opened console window has: Explorer drag-drop/double-click opens straight into this output with no scrollback above it, so without this line a summary reading just "Files processed ... 1" gives no clue which file that was.

Unless silent, the run ends with a summary printed by `PrintSummary` in `Program.cs`. It's not a flat list — it visually derives values from each other via indentation, with a dot leader instead of a plain gap or colon:
```
Summary:
  Files processed ....... 110
  UTF-8 text files ...... 105
    Modified .............. 5
      BOMs fixed .......... 5
      EOLs fixed .......... 5
    Backed up ............. 5
  Skipped ................. 5
  Directories skipped ..... 1
```
- `UTF-8 text files` = `Processed - Skipped` — deliberately named "UTF-8" explicitly, not just "text files", since `Skipped` already covers *both* truly-binary files *and* non-UTF-8 text (UTF-16/32 BOM'd, or legacy-encoded like Windows-1252); calling it plain "text files" would wrongly suggest it includes the latter. Shown *only* when `FilesSkipped > 0` — this is an explicit per-line `Show` condition, deliberately **not** folded into the generic "hide if value is zero" rule the other lines use, because `UTF-8 text files` is almost always non-zero even when nothing was skipped (it'd equal `Processed`), which would make it reappear as a redundant restatement of `Processed` if it shared that generic rule. (This was a real bug caught during manual testing — worth remembering if this logic is ever refactored.)
- `Modified` and `Backed up` are indented one level, as (usually-identical, but logically distinct — see above) subsets of `UTF-8 text files`. Unlike every other subordinate line, `Modified` is **always shown, even as `Modified ... 0`** — every line but `Files processed` hiding at once (nothing skipped, nothing failed, no directories skipped) would otherwise leave a bare `Files processed ... N` indistinguishable from a run that silently did nothing useful. An explicit `Modified ... 0` answers "did anything change" in the negative instead of just not answering it, without needing a separate sentence — this was tried first as a standalone `No changes necessary.` line, then replaced with this once it became clear an always-shown `Modified` line already covers both the single-conformant-file case *and* the "processed a folder, skipped some files, but the rest were already conformant" case in one consistent mechanism.
- `BOMs fixed`/`EOLs fixed`/`Backed up` stay hidden at zero (unlike `Modified`) — when `Modified` is `0` they're always `0` too, and repeating that under an already-explicit `Modified ... 0` would just be noise.
- `BOMs fixed`/`EOLs fixed` are indented a second level under `Modified`, since `Modified` = `BomChanged || EolChanged` — they're the two non-exclusive reasons a file counts as modified.
- Every other line stays at the base level, hidden when its own value is zero.
- Formatting: each line is `<indented label> <dot leader, min. 3 dots> <value>` with exactly one space on each side of the dot leader — the value is **never** padded/right-justified itself; instead the dot leader absorbs both the label-length AND the value-width differences: `dots = (maxLabelLength - thisLabelLength) + (maxValueLength - thisValueLength) + 3`. That guarantees every value's right edge lands in the same column (even mixing 1-digit and 3-digit values) while every value still has exactly one space before it. No colon.
- Color, summary block: labels and values are wrapped in SGR 1 (bold), the dot leader in SGR 2 (dim) — both *intensity* modulations of whatever foreground color the terminal already uses, never a hardcoded color (e.g. explicit white), so it can't fight a light-theme terminal. Reset via SGR 22 (intensity-only reset) rather than SGR 0, in case this ever nests inside other styling later.
- Color, warnings/errors: `Skipping version-control directory: ...` and `Skipping '<file>' — detected encoding: ...` lines are both wrapped in SGR 33 (yellow) — deliberately worded to match each other (`Skipping ...`, no `Warning:` prefix), since the `Error:` prefix is reserved for the red, higher-severity category. Every `Error: ...` line (fatal setup errors, CLI-parse errors, per-file failures) is wrapped in SGR 31 (red). Unlike Bold/Dim, these *do* set an explicit color — but the standard 8-color slot (31/33), not the "bright" 90s variant, since the standard slots are what terminal themes reliably remap to a readable value for their own background (a well-designed light theme's "yellow" is a darker gold, not literal `#FFFF00`). Reset via SGR 39 (default-foreground-only), not SGR 0. `Program.cs` computes color eligibility **per stream** — `useColorOut` (stdout: warnings, backup line, summary) and `useColorErr` (stderr: all `Error:` lines) — since a caller can redirect one stream without the other (e.g. `2> log.txt` while stdout stays live); each checks `Console.IsOutputRedirected`/`IsErrorRedirected` independently. Both are gated the same way otherwise: off if `--no-color` is in the raw args (checked directly, not via `options.NoColor`, so it also suppresses color on a CLI-parse-error message printed before a `CliOptions` even exists) or if the `NO_COLOR` env var is set to anything (the https://no-color.org convention — verified locally against a real `NO_COLOR=1` already set in the dev shell).
- A stream is also only color-eligible if `TryEnableAnsiColor` (`Program.cs`) can turn on `ENABLE_VIRTUAL_TERMINAL_PROCESSING` for its console handle via `GetConsoleMode`/`SetConsoleMode` (`NativeMethods`, `kernel32.dll`, classic `DllImport` rather than `LibraryImport` — see Console window management below for why). A console inherited from an already-running terminal (Windows Terminal, or any conhost with VT already on) already has this bit set, so the call is a no-op there. A freshly allocated console (Explorer drag-drop/double-click, a shortcut, Task Scheduler) starts with it off and needs the explicit `SetConsoleMode` call — and if the user has classic conhost's legacy console mode enabled (a per-user Windows setting outside this app's control), `SetConsoleMode` fails outright, so `TryEnableAnsiColor` returns `false` and output falls back to plain uncolored text rather than leaking raw `\x1b[1m`-style escape sequences. This was confirmed empirically: before the VT-enable call was added, a fresh conhost window rendered the literal escape bytes instead of interpreting them; after adding it, that same window fell back to clean monochrome text once legacy console mode was found to be the actual blocker, and rendered in color once the user disabled that Windows setting.
- Deliberately no Unicode box-drawing glyphs (`└─`/`├─`) for the hierarchy — those render unpredictably depending on console codepage/font, which would be a bad look for a tool whose whole job is encoding correctness. Plain indentation conveys the same hierarchy without that risk.

## Console window management

Dragging files/folders onto the exe (or double-clicking it, or a Task Scheduler run) makes Explorer allocate a brand-new console for the process; that console closes itself the instant the process exits, taking the just-printed summary with it before anyone can read it. `Program.cs` addresses this with two `kernel32.dll` P/Invokes in a `NativeMethods` class (classic `DllImport`, not `LibraryImport` — the source-generated marshaller needs `<AllowUnsafeBlocks>` for the `uint[]` array parameter `GetConsoleProcessList` takes, which isn't otherwise needed anywhere in this project):
- `ShouldPauseBeforeExit` calls `GetConsoleProcessList` to tell a freshly allocated, solely-owned console (return value `1`) apart from one inherited from an already-running process — an interactive shell, a `.bat` script — that will keep the window open on its own once this process exits (return value `> 1`). Only the former pauses with `Press any key to continue . . .` before returning; the latter would make every single terminal invocation require an extra keypress for nothing. `--silent`, `Console.IsOutputRedirected`, and `Console.IsInputRedirected` all skip the pause too — there's either no summary to protect or no live console to read a keypress from.
- `TryEnableAnsiColor` calls `GetConsoleMode`/`SetConsoleMode` per output stream to opt that stream's console into `ENABLE_VIRTUAL_TERMINAL_PROCESSING` — see the color-eligibility bullet above for why a freshly allocated console needs this explicitly. Both native calls degrade to a safe default (no pause; no color) on any failure rather than throwing, matching how every other color/pause eligibility check here treats a cosmetic feature as never worth risking a crash over.

A freshly opened console this pause protects has no invoking-command scrollback above it either, which is what `PrintTargets`'s `Processing: <target>` line (see above) exists to compensate for.

## Architecture

`NormalizeText.Core` holds everything except the process entry point, so the test project references only `Core` — never the console app:
- `TextEncodingDetector` — sniffs BOMs (UTF-8/16/32, either endianness) and, absent one, strict-decodes as UTF-8 (`UTF8Encoding(throwOnInvalidBytes: true)`) to tell real UTF-8 from binary (null-byte heuristic) from other/invalid encodings. Returns a `DetectedEncodingKind`.
- `TextNormalizer.Normalize(byte[], NormalizationOptions) -> NormalizationResult` — the pure core: decodes (stripping any BOM), rewrites line endings, re-encodes, re-attaches a BOM per `BomHandling`. Skips (no output bytes) whenever the detected kind isn't UTF-8. This is the unit-test target for encoding/BOM/EOL logic.
- `CliOptionsParser.Parse(string[]) -> CliParseResult` — hand-rolled arg parsing (no library), returns Ok/Help/Version/Error.
- `FileProcessor.Run(targets, outputPath, disableBackups) -> FileRunOutcome` — resolves targets to files (tracking each file's "root" target for relative-path placement under `--output` and under backups), validates the output path, then normalizes each file and either writes it back in place (backing it up first, if applicable) or to the computed `--output` destination, accumulating `RunStats`.
- `BackupPaths.BuildSessionDirectoryName(DateTime) -> string` — the pure `yyyy-MM-dd HHmmss-fff` formatting, split out from `FileProcessor` so the format is unit-testable without touching the filesystem or the clock.

`src/NormalizeText/Program.cs` (top-level statements) is intentionally thin: parse → run → print help/errors/warnings/summary. Any new behavior belongs in `Core` unless it's genuinely just console I/O.

## Solution layout

- `NormalizeText.slnx` — .NET 10 defaults to the new XML-free `.slnx` solution format (not `.sln`); use `dotnet sln NormalizeText.slnx add/remove ...` to manage projects in it.
- `src/NormalizeText/` — console app. `AssemblyName` is `normalize-text` (project/namespace stay `NormalizeText`); `Company` is `Strange`. Its `.csproj` has a `DeployToLocalPrograms` target (`AfterTargets="Publish"`) that copies the published exe to `%LocalAppData%\Programs\NormalizeText\` (`$(LocalAppData)` resolves from the `LOCALAPPDATA` env var per-machine, so nothing user-specific is hardcoded). Add that folder to `PATH` once and `dotnet publish src\NormalizeText -c Release` refreshes the on-PATH binary every time.
- `src/NormalizeText.Core/` — normalization/CLI logic, pure and unit-testable.
- `tests/NormalizeText.Tests/` — NUnit, references only `NormalizeText.Core`.

## Build & test

```
dotnet build NormalizeText.slnx
dotnet test NormalizeText.slnx
```
Run a single test:
```
dotnet test --filter "FullyQualifiedName~TextNormalizerTests.Normalize_BomForce_AddsBomWhenMissing"
```
Run the CLI locally without a separate build step:
```
dotnet run --project src\NormalizeText -- <target> [options]
```
Publish a real single-file exe (also triggers the `DeployToLocalPrograms` copy):
```
dotnet publish src\NormalizeText -c Release -r win-x64 --self-contained
```
`PublishSingleFile`/`PublishTrimmed` are already set in the csproj, so this one command yields a ~12MB trimmed exe instead of the ~70MB an untrimmed self-contained single-file publish would produce — safe here specifically because `NormalizeText`/`NormalizeText.Core` have zero external package references and no reflection-based CLI framework (a reflection-heavy dependency, e.g. one with attribute-driven command routing, would typically need explicit `TrimmerRootAssembly` hints to trim safely — this project has none of those). If a future dependency needs reflection at runtime, re-check trimming still works before assuming it's free.

## Repo conventions

- Target framework: .NET 10, C#, nullable + implicit usings enabled everywhere.
- `.gitignore` was generated via `dotnet new gitignore`; its default JetBrains block excludes `.idea/` — that line has been removed/commented per developer instruction (Rider's own `.idea/.gitignore` governs what's excluded there). Don't re-add a blanket `.idea/` ignore.
- Public repo: `README.md` and `LICENSE` (MIT) are both present. Keep this file free of local absolute paths, references to other unrelated repos, or any other detail that only makes sense on one specific machine — anyone cloning this repo should be able to follow it as-is.

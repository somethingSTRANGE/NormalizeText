# NormalizeText

`normalize-text` is a Windows CLI utility (C# / .NET 10) that normalizes UTF-8 text files, with configurable BOM and line-ending handling — stripping the BOM and enforcing LF by default. Run it against a single file, a folder (recursively), or drop files/folders onto the executable from Windows Explorer.

## Features

- Validates that a file is actually UTF-8 text before touching it — binary files and files in other encodings (UTF-16/32, legacy ANSI/Windows-1252, etc.) are safely skipped, never mangled
- Strips a BOM by default (or force-adds one, or leaves it alone — your choice)
- Normalizes line endings to LF by default (or CRLF, or leave them alone)
- Recursively processes folders, always skipping `.git`/`.svn` directories
- Backs up any file before it's overwritten in place, so a run is never a one-way trip
- Clear, colorized console output: what was skipped and why, what changed, and a hierarchical summary

## Installation

Download the latest release executable and put it somewhere on your `PATH`. It's a self-contained single-file `.exe` — no .NET runtime install required.

Alternatively, build from source (requires the [.NET 10 SDK](https://dotnet.microsoft.com/)):

```powershell
dotnet publish src\NormalizeText -c Release -r win-x64 --self-contained
```

The published exe lands in `src\NormalizeText\bin\Release\net10.0\win-x64\publish\normalize-text.exe`.

## Usage

```
normalize-text <target> [<target> ...] [options]
```

`<target>` is one or more file or folder paths — exactly what you'd get from selecting files/folders and dragging them onto the executable.

### Options

| Option | Description |
|---|---|
| `--eol <ignore\|lf\|crlf>` | Line ending handling. Default: `lf`. |
| `--bom <ignore\|strip\|force>` | BOM handling. Default: `strip`. |
| `--output <path>` | Write output here instead of in place. A file path for a single input file, or a directory when normalizing multiple files. |
| `--no-backups` | Disable the automatic backup (see below). |
| `--no-color` | Disable ANSI color in the summary. Also honors the `NO_COLOR` environment variable; color is off automatically when output isn't a terminal. |
| `--silent`, `--quiet` | Suppress warnings and the summary. |
| `--help`, `-h` | Show help. |
| `--version`, `-v` | Show version information. |

### Examples

```powershell
# Normalize a single file in place
normalize-text notes.txt

# Normalize an entire folder, recursively
normalize-text C:\path\to\project

# Force CRLF instead of the default LF
normalize-text notes.txt --eol crlf

# Write the normalized result somewhere else, leaving the original untouched
normalize-text notes.txt --output out\notes.txt

# Run quietly (e.g. from a script), suppressing warnings and the summary
normalize-text C:\path\to\project --silent
```

## What gets skipped

Non-text (binary) files are skipped silently — no point flagging every binary file mixed in with a folder of text files. Text files in an encoding other than UTF-8 are skipped *with* a warning naming the file and its detected encoding, since that's something you can act on. Either way, skipped files are always left untouched, and one bad file never stops the rest of the run.

A file that can't be read or written (e.g. access denied, locked by another process) is reported as an error and left untouched; the run continues with the remaining files, but the process exits with a non-zero exit code.

## Backups

Whenever `--output` isn't specified, `normalize-text` backs up each file it's about to modify — before overwriting it — under:

```
%LocalAppData%\Strange\NormalizeText\Backup\<yyyy-MM-dd HHmmss-fff>\
```

A dropped folder's subfolder structure is preserved under that timestamped backup folder. Files that are already conformant (no BOM, already LF) are left completely untouched — not rewritten, not backed up. Use `--no-backups` to skip this entirely.

### Example output

```
Processing: C:\path\to\target
Skipping version-control directory: '.git'.
Backup created at: %LocalAppData%\Strange\NormalizeText\Backup\2026-09-13 182717-759

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

When there are no changes, the output looks like this:

```
Processing: C:\path\to\notes.txt

Summary:
  Files processed ... 1
    Modified ........ 0
```

## Drag-and-drop support

Dragging files or folders onto the executable in Explorer (or a desktop shortcut) opens a standalone console window showing the results. The window will remain open until dismissed.

## Building & testing

Requires the .NET 10 SDK.

```powershell
dotnet build NormalizeText.slnx
dotnet test NormalizeText.slnx
```

## License

[MIT](LICENSE)

// -------------------------------------------------------------------------------------
// <copyright file="Program.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

using System.Reflection;

using NormalizeText.Core;

var parseResult = CliOptionsParser.Parse(args);

// Color eligibility is checked per-stream (stdout for warnings/summary, stderr for errors) since
// a caller can redirect one without the other (e.g. `2> log.txt` while stdout stays a live
// terminal). Checking the raw args for "--no-color" (rather than options.NoColor) means this
// also applies to CLI-parse-error output, printed before parsing produces a CliOptions.
var colorAllowed = !args.Contains("--no-color") && Environment.GetEnvironmentVariable("NO_COLOR") is null;
var useColorOut = colorAllowed && !Console.IsOutputRedirected;
var useColorErr = colorAllowed && !Console.IsErrorRedirected;

switch (parseResult.Kind)
{
    case CliParseResultKind.Help:
        PrintHelp(Console.Out);
        return 0;

    case CliParseResultKind.Version:
        Console.WriteLine(BuildVersionString());
        return 0;

    case CliParseResultKind.Error:
        Console.Error.WriteLine(Red($"Error: {parseResult.Error}", useColorErr));
        Console.Error.WriteLine();
        PrintHelp(Console.Error);
        return 1;
}

var options = parseResult.Options!;
var normalizationOptions = new NormalizationOptions(options.BomHandling, options.EolHandling);
var processor = new FileProcessor(normalizationOptions);
var outcome = processor.Run(options.Targets, options.OutputPath, options.NoBackups);

if (!outcome.Success)
{
    Console.Error.WriteLine(Red($"Error: {outcome.Error}", useColorErr));
    return 1;
}

foreach (var failed in outcome.FailedFiles)
{
    Console.Error.WriteLine(Red($"Error: failed to process '{failed.Path}': {failed.Reason}", useColorErr));
}

if (!options.Silent)
{
    Console.WriteLine();

    foreach (var skippedDirectory in outcome.SkippedDirectories)
    {
        Console.WriteLine(Yellow($"Skipping version-control directory: '{skippedDirectory}'.", useColorOut));
    }

    foreach (var skipped in outcome.SkippedFiles)
    {
        if (skipped.DetectedEncoding == DetectedEncodingKind.Binary)
        {
            continue;
        }

        Console.WriteLine(
            Yellow(
                $"Skipping '{skipped.Path}' — detected encoding: {skipped.DetectedEncoding.ToDisplayName()}.",
                useColorOut));
    }

    if (outcome.BackupDirectory is not null)
    {
        Console.WriteLine($"Backup created at: {outcome.BackupDirectory}");
    }

    PrintSummary(outcome.Stats, useColorOut);

    Console.WriteLine();
}

return outcome.Stats.FilesFailed > 0 ? 1 : 0;

static void PrintSummary(RunStats stats, bool useColor)
{
    const int MinDotLeaderLength = 3;

    var textFiles = stats.FilesProcessed - stats.FilesSkipped;

    // Indent conveys the derivation, not just visual nesting: BOMs/EOLs fixed are the two
    // (non-exclusive) reasons a file counts as "modified"; "modified" and "backed up" are each a
    // subset of "UTF-8 text files" (which is itself "processed" minus "skipped"). That subtotal
    // is only shown when it actually differs from "processed" - otherwise it's a redundant
    // restatement.
    // Each line's Show condition is explicit and independent - no generic "value != 0" fallback,
    // since that would make "UTF-8 text files" reappear whenever it happens to be non-zero (i.e.,
    // almost always) even when nothing was actually skipped, defeating the point of hiding it as
    // a redundant restatement of "Files processed".
    var lines = new List<(int Indent, string Label, int Value, bool Show)>
        {
            (0, "Files processed", stats.FilesProcessed, true),
            (0, "UTF-8 text files", textFiles, stats.FilesSkipped > 0),
            (1, "Modified", stats.FilesModified, stats.FilesModified != 0),
            (2, "BOMs fixed", stats.BomsChanged, stats.BomsChanged != 0),
            (2, "EOLs fixed", stats.EolsChanged, stats.EolsChanged != 0),
            (1, "Backed up", stats.FilesBackedUp, stats.FilesBackedUp != 0),
            (0, "Skipped", stats.FilesSkipped, stats.FilesSkipped != 0),
            (0, "Failed", stats.FilesFailed, stats.FilesFailed != 0),
            (0, "Directories skipped", stats.DirectoriesSkipped, stats.DirectoriesSkipped != 0),
        };

    var shown = lines.Where(line => line.Show).ToArray();

    Console.WriteLine();
    Console.WriteLine("Summary:");
    if (shown.Length == 0)
    {
        Console.WriteLine("  (nothing to report)");
        return;
    }

    var indentedLabels = shown.Select(line => new string(' ', 2 + (line.Indent * 2)) + line.Label).ToArray();
    var values = shown.Select(line => line.Value.ToString()).ToArray();
    var maxLabelLength = indentedLabels.Max(label => label.Length);
    var maxValueLength = values.Max(value => value.Length);

    for (var i = 0; i < shown.Length; i++)
    {
        // Right-justify the value using only the dot leader's length, never by padding the value
        // itself - that way there's always exactly one space before the value, regardless of how
        // many digits it has.
        var dotsLength = (maxLabelLength - indentedLabels[i].Length)
            + (maxValueLength - values[i].Length)
            + MinDotLeaderLength;
        var dots = new string('.', dotsLength);
        Console.WriteLine($"{Bold(indentedLabels[i], useColor)} {Dim(dots, useColor)} {Bold(values[i], useColor)}");
    }
}

// SGR 1/2 (bold/dim) modulate the intensity of whatever foreground color the terminal is already
// using, rather than setting an explicit color - so this never fights a light-theme terminal the
// way hardcoding e.g. "white" would. SGR 22 resets intensity only, not other attributes, in case
// this is ever nested inside some other styling later.
static string Bold(string text, bool useColor)
{
    return useColor ? $"\x1b[1m{text}\x1b[22m" : text;
}

static string Dim(string text, bool useColor)
{
    return useColor ? $"\x1b[2m{text}\x1b[22m" : text;
}

// Unlike Bold/Dim, these DO set an explicit color (SGR 31/33 - the standard, theme-remappable
// 8-color set, not the "bright" 91/93 variants some terminals treat as a separate hardcoded
// palette). Reset via SGR 39 (default foreground only), not SGR 0, so this composes cleanly if
// ever nested inside Bold/Dim.
static string Red(string text, bool useColor)
{
    return useColor ? $"\x1b[31m{text}\x1b[39m" : text;
}

static string Yellow(string text, bool useColor)
{
    return useColor ? $"\x1b[33m{text}\x1b[39m" : text;
}

static void PrintHelp(TextWriter writer)
{
    writer.WriteLine("normalize-text — normalize text files to UTF-8 (no BOM by default) with LF line endings.");
    writer.WriteLine();
    writer.WriteLine("Usage:");
    writer.WriteLine("  normalize-text <target> [<target> ...] [options]");
    writer.WriteLine();
    writer.WriteLine("Targets:");
    writer.WriteLine("  One or more file or folder paths. Folders are processed recursively.");
    writer.WriteLine("  .git and .svn directories are always skipped during recursion.");
    writer.WriteLine();
    writer.WriteLine("Options:");
    writer.WriteLine("  --eol <ignore|lf|crlf>      Line ending handling. Default: lf.");
    writer.WriteLine("  --bom <ignore|strip|force>  BOM handling. Default: strip.");
    writer.WriteLine("  --output <path>             Write output here instead of in-place.");
    writer.WriteLine("                              A file path for a single input file, or a");
    writer.WriteLine("                              directory when normalizing multiple files.");
    writer.WriteLine("  --no-backups                Disable the automatic backup described below.");
    writer.WriteLine("  --no-color                  Disable ANSI color in the summary. Also honors");
    writer.WriteLine("                              the NO_COLOR env var; color is off automatically");
    writer.WriteLine("                              when output isn't a terminal.");
    writer.WriteLine("  --silent, --quiet           Suppress warnings and the summary.");
    writer.WriteLine("  --help, -h                  Show this help.");
    writer.WriteLine("  --version, -v               Show version information.");
    writer.WriteLine();
    writer.WriteLine("Non-text (binary) files are skipped silently. Text files in an encoding other");
    writer.WriteLine("than UTF-8 are skipped with a warning naming the file and its detected");
    writer.WriteLine("encoding. Skipped files are always left untouched.");
    writer.WriteLine();
    writer.WriteLine("A file that can't be read or written (e.g. access denied) is reported as an");
    writer.WriteLine("error and left untouched; the run continues with the remaining files, but");
    writer.WriteLine("exits with a non-zero exit code.");
    writer.WriteLine();
    writer.WriteLine("When --output is not specified, original files are backed up before being");
    writer.WriteLine("overwritten in place, under:");
    writer.WriteLine("  %LocalAppData%\\NormalizeText\\Backup\\<yyyy-MM-dd HHmmss-fff>\\");
    writer.WriteLine("A dropped folder's subfolder structure is preserved under that timestamped");
    writer.WriteLine("backup folder. Use --no-backups to skip this.");
}

// AssemblyInformationalVersionAttribute already reads "<Version>+<git-sha>" here with zero extra
// setup - the .NET SDK appends the commit SHA automatically whenever the project builds inside a
// git repo with at least one commit. Appending the running exe's own last-write time (not a
// compile-time constant) answers a specific question for a self-contained single-file exe: is
// this actually the copy that was just published? Its write time reflects when this specific file
// landed here, not when the bytes were compiled.
static string BuildVersionString()
{
    var assembly = Assembly.GetExecutingAssembly();
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly.GetName().Version?.ToString() ?? "unknown";

    var processPath = Environment.ProcessPath;
    var builtAt = processPath is not null && File.Exists(processPath)
        ? File.GetLastWriteTimeUtc(processPath).ToString("yyyy-MM-dd HH:mm:ss") + " UTC"
        : "unknown";

    return $"{version} (built {builtAt})";
}

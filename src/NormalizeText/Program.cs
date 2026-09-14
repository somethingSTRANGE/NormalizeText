// -------------------------------------------------------------------------------------
// <copyright file="Program.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText;

using System.Reflection;
using System.Runtime.InteropServices;

using NormalizeText.Core;

internal static class Program
{
    /// <summary>Wraps <paramref name="text"/> in SGR bold, unless <paramref name="useColor"/> is
    ///     false.</summary>
    /// <returns>The wrapped text, or <paramref name="text"/> unchanged if color is disabled.</returns>
    /// <remarks>SGR 1 (bold) modulates the intensity of whatever foreground color the terminal
    ///     is already using, rather than setting an explicit color - so this never fights a
    ///     light-theme terminal the way hardcoding e.g. "white" would. Reset via SGR 22
    ///     (intensity-only), not SGR 0, in case this is ever nested inside some other styling
    ///     later.</remarks>
    private static string Bold(string text, bool useColor)
    {
        return useColor ? $"\x1b[1m{text}\x1b[22m" : text;
    }

    /// <summary>Builds the string printed by <c>--version</c>.</summary>
    /// <returns>The informational version, optionally suffixed with the git commit SHA, followed
    ///     by the running exe's build timestamp in parentheses - e.g.
    ///     <c>0.1.0+31d68a7… (built 2026-09-12 09:25:46 UTC)</c>.</returns>
    /// <remarks><see cref="AssemblyInformationalVersionAttribute"/> already includes the git SHA
    ///     suffix with zero extra setup - the .NET SDK appends the commit SHA automatically
    ///     whenever the project builds inside a git repo with at least one commit. Appending the
    ///     running exe's own last-write time (not a compile-time constant) answers a specific
    ///     question for a self-contained single-file exe: is this actually the copy that was
    ///     just published? That timestamp reflects when this specific file landed here, not when
    ///     the bytes were compiled.</remarks>
    private static string BuildVersionString()
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

    /// <summary>Wraps <paramref name="text"/> in SGR dim, unless <paramref name="useColor"/> is
    ///     false.</summary>
    /// <returns>The wrapped text, or <paramref name="text"/> unchanged if color is disabled.</returns>
    /// <remarks>SGR 2 (dim) is <see cref="Bold"/>'s intensity-only counterpart - see its
    ///     remarks.</remarks>
    private static string Dim(string text, bool useColor)
    {
        return useColor ? $"\x1b[2m{text}\x1b[22m" : text;
    }

    private static int Main(string[] args)
    {
        var parseResult = CliOptionsParser.Parse(args);

        // Checked per-stream since a caller can redirect one without the other (e.g. `2> log.txt` while
        // stdout stays live). The raw args (not options.NoColor) are checked directly, so this also covers
        // CLI-parse-error output, printed before a CliOptions exists.
        var colorAllowed = !args.Contains("--no-color") && Environment.GetEnvironmentVariable("NO_COLOR") is null;
        var useColorOut =
            colorAllowed && !Console.IsOutputRedirected && TryEnableAnsiColor(NativeMethods.StdOutputHandle);
        var useColorErr =
            colorAllowed && !Console.IsErrorRedirected && TryEnableAnsiColor(NativeMethods.StdErrorHandle);

        var exitCode = RunApplication(parseResult, useColorOut, useColorErr);

        if (ShouldPauseBeforeExit(parseResult.Options?.Silent ?? false))
        {
            Console.Out.WriteLine();
            Console.Out.Write("Press any key to continue . . . ");
            Console.ReadKey(intercept: true);
        }

        return exitCode;
    }

    /// <summary>Prints full CLI usage/help text to <paramref name="writer"/>.</summary>
    private static void PrintHelp(TextWriter writer)
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
        writer.WriteLine("  --silent, --quiet           Suppress warnings, the summary, and the");
        writer.WriteLine("                              press-any-key pause described below.");
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
        writer.WriteLine("  %LocalAppData%\\Strange\\NormalizeText\\Backup\\<yyyy-MM-dd HHmmss-fff>\\");
        writer.WriteLine("A dropped folder's subfolder structure is preserved under that timestamped");
        writer.WriteLine("backup folder. Use --no-backups to skip this.");
        writer.WriteLine();
        writer.WriteLine("When run from a freshly opened console (e.g. dragging files onto the exe in");
        writer.WriteLine("Explorer), the window waits for a keypress before closing so the summary above");
        writer.WriteLine("can actually be read. Running from an existing terminal, or with output/input");
        writer.WriteLine("redirected, skips this since the window isn't about to disappear.");
    }

    /// <summary>Prints the end-of-run summary: a dot-leader table of <paramref name="stats"/>,
    ///     indented to show which counts are derived from which.</summary>
    /// <param name="stats">The run's accumulated statistics.</param>
    /// <param name="useColor">Whether ANSI color is enabled for this output.</param>
    private static void PrintSummary(RunStats stats, bool useColor)
    {
        const int MinDotLeaderLength = 3;

        var textFiles = stats.FilesProcessed - stats.FilesSkipped;

        // Indent conveys the derivation, not just visual nesting: BOMs/EOLs fixed are the two
        // (non-exclusive) reasons a file counts as "modified"; "modified" and "backed up" are each a
        // subset of "UTF-8 text files" (which is itself "processed" minus "skipped"). That subtotal
        // is only shown when it actually differs from "processed" - otherwise it's a redundant
        // restatement.
        //
        // "Modified" always shows, even as "Modified ... 0" - unlike its own BOMs/EOLs/Backed-up
        // children, it's the line that answers "did anything actually change", so hiding it at zero
        // would leave that question unanswered instead of answered-in-the-negative, indistinguishable
        // from a run that silently did nothing useful.
        //
        // Every other line's Show condition is explicit and independent - no generic "value != 0"
        // fallback. That would make "UTF-8 text files" reappear whenever it happens to be
        // non-zero (i.e., almost always) even when nothing was actually skipped, defeating the point
        // of hiding it as a redundant restatement of "Files processed".
        var lines = new List<(int Indent, string Label, int Value, bool Show)>
            {
                (0, "Files processed", stats.FilesProcessed, true),
                (0, "UTF-8 text files", textFiles, stats.FilesSkipped > 0),
                (1, "Modified", stats.FilesModified, true),
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

    /// <summary>Prints the target(s) being processed: <c>Processing: </c> followed by the target
    ///     for one target, or a <c>Processing:</c> heading plus one indented line per target for
    ///     several.</summary>
    /// <param name="targets">The raw, unresolved target strings from the command line.</param>
    private static void PrintTargets(IReadOnlyList<string> targets)
    {
        if (targets.Count == 1)
        {
            Console.WriteLine($"Processing: {targets[0]}");
            return;
        }

        Console.WriteLine("Processing:");
        foreach (var target in targets)
        {
            Console.WriteLine($"  {target}");
        }
    }

    /// <summary>Prints the target echo, skip warnings, backup line, and summary for a completed
    ///     run.</summary>
    /// <param name="targets">The raw, unresolved target strings from the command line.</param>
    /// <param name="outcome">The completed run's outcome.</param>
    /// <param name="useColorOut">Whether ANSI color is enabled for stdout.</param>
    private static void PrintWarningsAndSummary(IReadOnlyList<string> targets, FileRunOutcome outcome, bool useColorOut)
    {
        Console.WriteLine();
        PrintTargets(targets);

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

    /// <summary>Wraps <paramref name="text"/> in SGR red, unless <paramref name="useColor"/> is
    ///     false.</summary>
    /// <returns>The wrapped text, or <paramref name="text"/> unchanged if color is disabled.</returns>
    /// <remarks>Unlike <see cref="Bold"/>/<see cref="Dim"/>, this DOES set an explicit color
    ///     (SGR 31 - the standard, theme-remappable 8-color slot, not the "bright" 91 variant
    ///     some terminals treat as a separate hardcoded palette). Reset via SGR 39 (default
    ///     foreground only), not SGR 0, so this composes cleanly if ever nested inside Bold/Dim.</remarks>
    private static string Red(string text, bool useColor)
    {
        return useColor ? $"\x1b[31m{text}\x1b[39m" : text;
    }

    /// <summary>Runs the parsed command: prints help/version/a parse error, or runs the
    ///     normalization batch and prints its warnings and summary.</summary>
    /// <param name="parseResult">The result of <see cref="CliOptionsParser.Parse"/>.</param>
    /// <param name="useColorOut">Whether ANSI color is enabled for stdout.</param>
    /// <param name="useColorErr">Whether ANSI color is enabled for stderr.</param>
    /// <returns>The process exit code.</returns>
    private static int RunApplication(CliParseResult parseResult, bool useColorOut, bool useColorErr)
    {
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

            default:
                return RunNormalization(parseResult.Options!, useColorOut, useColorErr);
        }
    }

    /// <summary>Runs the normalization batch for <paramref name="options"/> and prints its
    ///     errors, warnings, and summary.</summary>
    /// <param name="options">The parsed CLI options.</param>
    /// <param name="useColorOut">Whether ANSI color is enabled for stdout.</param>
    /// <param name="useColorErr">Whether ANSI color is enabled for stderr.</param>
    /// <returns>The process exit code.</returns>
    private static int RunNormalization(CliOptions options, bool useColorOut, bool useColorErr)
    {
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
            PrintWarningsAndSummary(options.Targets, outcome, useColorOut);
        }

        return outcome.Stats.FilesFailed > 0 ? 1 : 0;
    }

    /// <summary>Determines whether to print "Press any key to continue..." and wait before the
    ///     process exits.</summary>
    /// <param name="silent">Whether <c>--silent</c>/<c>--quiet</c> was passed.</param>
    /// <returns><see langword="true"/> only for a freshly allocated, solely owned console with a
    ///     live keyboard to read from.</returns>
    /// <remarks>A fresh console (Explorer double-click/drag-and-drop, a desktop shortcut, Task
    ///     Scheduler) closes itself the instant this process exits, taking the just-printed
    ///     summary/warnings with it before anyone can read them.
    ///     <see cref="NativeMethods.GetConsoleProcessList"/> returning <c>1</c> means this
    ///     process is that console's sole owner; a value greater than <c>1</c> means the console
    ///     was inherited from an already-running process (an interactive shell, a <c>.bat</c>
    ///     script) that keeps the window open on its own once this process exits, so pausing
    ///     there would just be an unwanted extra keypress on every invocation. Redirected
    ///     output/input also skip the pause - no live console to read a keypress from, and a
    ///     scripted/piped invocation should never block.</remarks>
    private static bool ShouldPauseBeforeExit(bool silent)
    {
        if (silent || Console.IsOutputRedirected || Console.IsInputRedirected)
        {
            return false;
        }

        var processIds = new uint[2];
        var attachedCount = NativeMethods.GetConsoleProcessList(processIds, (uint)processIds.Length);
        return attachedCount <= 1;
    }

    /// <summary>Attempts to turn on ANSI/VT100 escape sequence processing for one console
    ///     stream.</summary>
    /// <param name="stdHandleId"><see cref="NativeMethods.StdOutputHandle"/> or
    ///     <see cref="NativeMethods.StdErrorHandle"/>.</param>
    /// <returns><see langword="true"/> if that stream's console now interprets SGR escape
    ///     sequences.</returns>
    /// <remarks>A console's VT-processing mode is a property of the handle it's queried through,
    ///     so this must run per-stream (stdout/stderr can be two different handles, e.g., one
    ///     redirected and one not). A console inherited from an already-running terminal already
    ///     has this bit set, so the call is a no-op there; a freshly allocated console (Explorer
    ///     double-click/drag-and-drop, a shortcut, Task Scheduler) starts with it off and needs
    ///     the explicit <c>SetConsoleMode</c> call - which fails outright if the user has
    ///     classic conhost's legacy console mode enabled (a per-user Windows setting outside
    ///     this app's control). Any failure here means "assume no color support" rather than
    ///     throwing, so the output falls back to plain text instead of leaking raw escape bytes.</remarks>
    private static bool TryEnableAnsiColor(int stdHandleId)
    {
        var handle = NativeMethods.GetStdHandle(stdHandleId);
        if ((handle == NativeMethods.InvalidHandleValue) || !NativeMethods.GetConsoleMode(handle, out var mode))
        {
            return false;
        }

        if ((mode & NativeMethods.EnableVirtualTerminalProcessing) != 0)
        {
            return true;
        }

        return NativeMethods.SetConsoleMode(handle, mode | NativeMethods.EnableVirtualTerminalProcessing);
    }

    /// <summary>Wraps <paramref name="text"/> in SGR yellow, unless <paramref name="useColor"/>
    ///     is false.</summary>
    /// <returns>The wrapped text, or <paramref name="text"/> unchanged if color is disabled.</returns>
    /// <remarks>SGR 33 - the standard slot, reset via SGR 39. See <see cref="Red"/>'s remarks.</remarks>
    private static string Yellow(string text, bool useColor)
    {
        return useColor ? $"\x1b[33m{text}\x1b[39m" : text;
    }
}

/// <summary>Console-related <c>kernel32.dll</c> P/Invokes for
///     <see cref="Program.ShouldPauseBeforeExit"/> and
///     <see cref="Program.TryEnableAnsiColor"/>.</summary>
/// <remarks>This uses classic <see cref="DllImportAttribute"/> rather than
///     <c>LibraryImport</c>. Source-generated array marshaling for <c>LibraryImport</c>
///     requires the MSBuild property <c>AllowUnsafeBlocks</c>, which isn't otherwise needed
///     anywhere in this project. It isn't worth enabling unsafe code project-wide for the
///     one P/Invoke that classic marshaling already handles.</remarks>
internal static class NativeMethods
{
    internal const int StdOutputHandle = -11;

    internal const int StdErrorHandle = -12;

    internal const uint EnableVirtualTerminalProcessing = 0x0004;

    internal static readonly IntPtr InvalidHandleValue = new(-1);

    [DllImport("kernel32.dll")]
    internal static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetStdHandle(int stdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetConsoleMode(IntPtr consoleHandle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetConsoleMode(IntPtr consoleHandle, uint mode);
}

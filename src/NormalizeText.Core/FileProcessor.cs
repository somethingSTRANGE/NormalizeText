// -------------------------------------------------------------------------------------
// <copyright file="FileProcessor.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

public sealed class FileProcessor(
    NormalizationOptions options,
    string? backupRoot = null,
    Func<DateTime>? clockOverride = null)
{
    private static readonly string[] vcsDirectoryNames = [".git", ".svn"];

    private readonly string backupRootDirectory = backupRoot ?? DefaultBackupRootDirectory();

    private readonly Func<DateTime> clock = clockOverride ?? (() => DateTime.Now);

    public FileRunOutcome Run(IReadOnlyList<string> targets, string? outputPath, bool disableBackups = false)
    {
        var resolveResult = ResolveFiles(targets);
        if (resolveResult.Error is not null)
        {
            return FileRunOutcome.Fail(resolveResult.Error);
        }

        var files = resolveResult.Files;
        var destinationError = ValidateOutputPath(outputPath, files.Count);
        if (destinationError is not null)
        {
            return FileRunOutcome.Fail(destinationError);
        }

        EnsureOutputDirectoryExists(outputPath, files.Count);

        var needsBackup = !disableBackups && outputPath is null;
        var state = new RunState
            {
                Stats =
                    {
                        DirectoriesSkipped = resolveResult.SkippedDirectories.Count,
                    },
            };

        foreach (var file in files)
        {
            this.ProcessFile(file, files.Count, outputPath, needsBackup, state);
        }

        return FileRunOutcome.Ok(
            state.Stats,
            state.SkippedFiles,
            state.FailedFiles,
            resolveResult.SkippedDirectories,
            state.BackupDirectory);
    }

    private static void BackUpOriginalFile(ResolvedFile file, string backupDirectory, byte[] originalContent)
    {
        var relative = Path.GetRelativePath(file.Root, file.Path);
        var backupPath = Path.Combine(backupDirectory, relative);
        var backupDir = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        File.WriteAllBytes(backupPath, originalContent);
    }

    private static string DefaultBackupRootDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NormalizeText",
            "Backup");
    }

    private static void EnsureOutputDirectoryExists(string? outputPath, int fileCount)
    {
        if (outputPath is not null && (fileCount > 1) && !Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }
    }

    private static IEnumerable<string> EnumerateFilesExcludingVcsDirectories(
        string root,
        string directory,
        List<string> skippedDirectories)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            yield return file;
        }

        foreach (var subDirectory in Directory.EnumerateDirectories(directory))
        {
            if (IsVcsDirectory(subDirectory))
            {
                skippedDirectories.Add(Path.GetRelativePath(root, subDirectory));
                continue;
            }

            foreach (var file in EnumerateFilesExcludingVcsDirectories(root, subDirectory, skippedDirectories))
            {
                yield return file;
            }
        }
    }

    private static bool IsVcsDirectory(string directory)
    {
        var name = Path.GetFileName(directory);
        return vcsDirectoryNames.Any(vcsName => string.Equals(name, vcsName, StringComparison.OrdinalIgnoreCase));
    }

    private static void RecordFailure(ResolvedFile file, Exception ex, RunState state)
    {
        state.Stats.FilesFailed++;
        state.FailedFiles.Add(new FailedFileInfo(RelativeToRoot(file), ex.Message));
    }

    private static void RecordModified(NormalizationResult result, RunState state)
    {
        state.Stats.FilesModified++;
        if (result.BomChanged)
        {
            state.Stats.BomsChanged++;
        }

        if (result.EolChanged)
        {
            state.Stats.EolsChanged++;
        }
    }

    // Warning/error messages report paths relative to whichever target produced the file - the
    // same "root" already used for backup/output placement - so a message reads e.g.
    // "Editor\Foo.cs" instead of repeating the full dropped-folder path on every single line.
    private static string RelativeToRoot(ResolvedFile file)
    {
        return Path.GetRelativePath(file.Root, file.Path);
    }

    private static string ResolveDestination(ResolvedFile file, string? outputPath, int fileCount)
    {
        if (outputPath is null)
        {
            return file.Path;
        }

        if (fileCount > 1)
        {
            var relative = Path.GetRelativePath(file.Root, file.Path);
            return Path.Combine(outputPath, relative);
        }

        return Directory.Exists(outputPath) ? Path.Combine(outputPath, Path.GetFileName(file.Path)) : outputPath;
    }

    private static (IReadOnlyList<ResolvedFile> Files, IReadOnlyList<string> SkippedDirectories, string? Error)
        ResolveFiles(IReadOnlyList<string> targets)
    {
        var files = new List<ResolvedFile>();
        var skippedDirectories = new List<string>();

        foreach (var target in targets)
        {
            if (File.Exists(target))
            {
                var root = Path.GetDirectoryName(Path.GetFullPath(target)) ?? target;
                files.Add(new ResolvedFile(target, root));
            }
            else if (Directory.Exists(target))
            {
                foreach (var file in EnumerateFilesExcludingVcsDirectories(target, target, skippedDirectories))
                {
                    files.Add(new ResolvedFile(file, target));
                }
            }
            else
            {
                return (files, skippedDirectories, $"Target not found: '{target}'.");
            }
        }

        return (files, skippedDirectories, null);
    }

    private static string? ValidateOutputPath(string? outputPath, int fileCount)
    {
        if (outputPath is null || (fileCount <= 1))
        {
            return null;
        }

        if (File.Exists(outputPath))
        {
            return $"--output '{outputPath}' is a single file, but {fileCount} input files were resolved. "
                + "--output must be a directory when normalizing multiple files.";
        }

        return null;
    }

    private string CreateBackupSessionDirectory()
    {
        var sessionDirectoryName = BackupPaths.BuildSessionDirectoryName(this.clock());
        var sessionDirectory = Path.Combine(this.backupRootDirectory, sessionDirectoryName);
        Directory.CreateDirectory(sessionDirectory);
        return sessionDirectory;
    }

    private void ProcessFile(ResolvedFile file, int fileCount, string? outputPath, bool needsBackup, RunState state)
    {
        state.Stats.FilesProcessed++;

        byte[] content;
        try
        {
            content = File.ReadAllBytes(file.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            RecordFailure(file, ex, state);
            return;
        }

        var result = TextNormalizer.Normalize(content, options);

        if (result.Skipped)
        {
            state.Stats.FilesSkipped++;
            state.SkippedFiles.Add(new SkippedFileInfo(RelativeToRoot(file), result.DetectedEncoding));
            return;
        }

        var changed = result.BomChanged || result.EolChanged;

        // Only write when something actually changed, or when --output sends the file to a
        // different location entirely (a copy is expected there even if the content needed no
        // fixing). When processing in place, a file that's already conformant is left completely
        // untouched - no rewrite, no backup, no mtime change.
        if (!changed && outputPath is null)
        {
            return;
        }

        var written = this.TryWriteNormalizedFile(
            file,
            fileCount,
            outputPath,
            needsBackup,
            changed,
            content,
            result,
            state);
        if (!written || !changed)
        {
            return;
        }

        RecordModified(result, state);
    }

    private bool TryWriteNormalizedFile(
        ResolvedFile file,
        int fileCount,
        string? outputPath,
        bool needsBackup,
        bool changed,
        byte[] content,
        NormalizationResult result,
        RunState state)
    {
        try
        {
            if (needsBackup && changed)
            {
                state.BackupDirectory ??= this.CreateBackupSessionDirectory();
                BackUpOriginalFile(file, state.BackupDirectory, content);
                state.Stats.FilesBackedUp++;
            }

            var destination = ResolveDestination(file, outputPath, fileCount);
            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            File.WriteAllBytes(destination, result.OutputBytes!);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            RecordFailure(file, ex, state);
            return false;
        }
    }

    private sealed class RunState
    {
        public string? BackupDirectory { get; set; }

        public List<FailedFileInfo> FailedFiles { get; } = [];

        public List<SkippedFileInfo> SkippedFiles { get; } = [];

        public RunStats Stats { get; } = new();
    }

    private sealed record ResolvedFile(string Path, string Root);
}

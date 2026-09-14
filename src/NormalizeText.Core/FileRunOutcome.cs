// -------------------------------------------------------------------------------------
// <copyright file="FileRunOutcome.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

public sealed class FileRunOutcome
{
    private FileRunOutcome(
        bool success,
        string? error,
        RunStats stats,
        IReadOnlyList<SkippedFileInfo> skippedFiles,
        IReadOnlyList<FailedFileInfo> failedFiles,
        IReadOnlyList<string> skippedDirectories,
        string? backupDirectory)
    {
        this.Success = success;
        this.Error = error;
        this.Stats = stats;
        this.SkippedFiles = skippedFiles;
        this.FailedFiles = failedFiles;
        this.SkippedDirectories = skippedDirectories;
        this.BackupDirectory = backupDirectory;
    }

    public string? BackupDirectory { get; }

    public string? Error { get; }

    public IReadOnlyList<FailedFileInfo> FailedFiles { get; }

    public IReadOnlyList<string> SkippedDirectories { get; }

    public IReadOnlyList<SkippedFileInfo> SkippedFiles { get; }

    public RunStats Stats { get; }

    public bool Success { get; }

    public static FileRunOutcome Fail(string error)
    {
        return new FileRunOutcome(success: false, error, new RunStats(), [], [], [], backupDirectory: null);
    }

    public static FileRunOutcome Ok(
        RunStats stats,
        IReadOnlyList<SkippedFileInfo> skippedFiles,
        IReadOnlyList<FailedFileInfo> failedFiles,
        IReadOnlyList<string> skippedDirectories,
        string? backupDirectory)
    {
        return new FileRunOutcome(
            success: true,
            error: null,
            stats,
            skippedFiles,
            failedFiles,
            skippedDirectories,
            backupDirectory);
    }
}

// -------------------------------------------------------------------------------------
// <copyright file="FileProcessorTests.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Tests;

using NormalizeText.Core;

public class FileProcessorTests
{
    private string backupRoot = null!;

    private string tempDir = null!;

    [Test]
    public void Run_FileIsReadOnly_ReportsFailureAndContinuesRatherThanThrowing()
    {
        var file = Path.Combine(this.tempDir, "a.txt");
        File.WriteAllText(file, "hello\r\n");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        try
        {
            var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
            var outcome = processor.Run([file], outputPath: null);

            Assert.That(outcome.Success, Is.True);
            Assert.That(outcome.Stats.FilesFailed, Is.EqualTo(1));
            Assert.That(
                outcome.Stats.FilesProcessed,
                Is.EqualTo(1),
                "the file was checked, even though writing it failed");
            Assert.That(
                outcome.Stats.FilesModified,
                Is.EqualTo(0),
                "the overwrite never completed, so it isn't 'modified'");
            Assert.That(
                outcome.Stats.FilesBackedUp,
                Is.EqualTo(1),
                "backup should still succeed even though the in-place overwrite fails");
            Assert.That(outcome.FailedFiles, Has.Count.EqualTo(1));
            Assert.That(
                outcome.FailedFiles[0].Path,
                Is.EqualTo("a.txt"),
                "should be relative to the target, not an absolute path");
        }
        finally
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    [Test]
    public void Run_FolderTarget_AllFilesSkipped_NoBackupDirectoryIsCreated()
    {
        File.WriteAllBytes(Path.Combine(this.tempDir, "binary.dat"), [0x00, 0x01, 0x02]);

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
        var outcome = processor.Run([this.tempDir], outputPath: null);

        Assert.That(outcome.BackupDirectory, Is.Null);
        Assert.That(Directory.Exists(this.backupRoot), Is.False);
    }

    [Test]
    public void Run_FolderTarget_MixOfGoodAndReadOnlyFiles_ProcessesGoodOnesAndReportsFailure()
    {
        var goodFile = Path.Combine(this.tempDir, "good.txt");
        var badFile = Path.Combine(this.tempDir, "bad.txt");
        File.WriteAllText(goodFile, "one\r\n");
        File.WriteAllText(badFile, "two\r\n");
        File.SetAttributes(badFile, FileAttributes.ReadOnly);

        try
        {
            var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
            var outcome = processor.Run([this.tempDir], outputPath: null);

            Assert.That(outcome.Success, Is.True);
            Assert.That(outcome.Stats.FilesProcessed, Is.EqualTo(2), "both files were checked");
            Assert.That(outcome.Stats.FilesModified, Is.EqualTo(1), "only the good file completed successfully");
            Assert.That(outcome.Stats.FilesFailed, Is.EqualTo(1));
            Assert.That(File.ReadAllText(goodFile), Is.EqualTo("one\n"));
        }
        finally
        {
            File.SetAttributes(badFile, FileAttributes.Normal);
        }
    }

    [Test]
    public void Run_FolderTarget_NestedSkippedAndFailedFiles_ReportRelativeToDroppedFolder()
    {
        var subDir = Path.Combine(this.tempDir, "Editor");
        Directory.CreateDirectory(subDir);
        var utf16File = Path.Combine(subDir, "Legacy.cs");
        File.WriteAllBytes(utf16File, [0xFE, 0xFF, 0x00, 0x68, 0x00, 0x69]);
        var readOnlyFile = Path.Combine(subDir, "Locked.cs");
        File.WriteAllText(readOnlyFile, "hello\r\n");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        try
        {
            var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
            var outcome = processor.Run([this.tempDir], outputPath: null);

            Assert.That(outcome.SkippedFiles, Has.Count.EqualTo(1));
            Assert.That(outcome.SkippedFiles[0].Path, Is.EqualTo(Path.Combine("Editor", "Legacy.cs")));
            Assert.That(outcome.FailedFiles, Has.Count.EqualTo(1));
            Assert.That(outcome.FailedFiles[0].Path, Is.EqualTo(Path.Combine("Editor", "Locked.cs")));
        }
        finally
        {
            File.SetAttributes(readOnlyFile, FileAttributes.Normal);
        }
    }

    [Test]
    public void Run_FolderTarget_NoOutput_PreservesRelativeHierarchyInBackup()
    {
        var subDir = Path.Combine(this.tempDir, "baz");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(this.tempDir, "a.txt"), "one\r\n");
        File.WriteAllText(Path.Combine(subDir, "b.txt"), "two\r\n");
        var fixedTimestamp = new DateTime(2026, 1, 2, 3, 4, 5, 678);

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot, () => fixedTimestamp);
        processor.Run([this.tempDir], outputPath: null);

        var expectedSessionDir = Path.Combine(this.backupRoot, "2026-01-02 030405-678");
        Assert.That(File.Exists(Path.Combine(expectedSessionDir, "a.txt")), Is.True);
        Assert.That(File.Exists(Path.Combine(expectedSessionDir, "baz", "b.txt")), Is.True);
    }

    [Test]
    public void Run_FolderTarget_RecursesAndProcessesAllFiles()
    {
        var subDir = Path.Combine(this.tempDir, "nested");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(this.tempDir, "a.txt"), "one\r\n");
        File.WriteAllText(Path.Combine(subDir, "b.txt"), "two\r\n");

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
        var outcome = processor.Run([this.tempDir], outputPath: null);

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Stats.FilesProcessed, Is.EqualTo(2));
    }

    [Test]
    public void Run_FolderTarget_SkipsGitAndSvnDirectories()
    {
        var gitDir = Path.Combine(this.tempDir, ".git");
        var svnDir = Path.Combine(this.tempDir, ".svn");
        Directory.CreateDirectory(gitDir);
        Directory.CreateDirectory(svnDir);
        File.WriteAllText(Path.Combine(gitDir, "config"), "should not be touched\r\n");
        File.WriteAllText(Path.Combine(svnDir, "entries"), "should not be touched\r\n");
        File.WriteAllText(Path.Combine(this.tempDir, "real.txt"), "hello\r\n");

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
        var outcome = processor.Run([this.tempDir], outputPath: null);

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Stats.FilesProcessed, Is.EqualTo(1));
        Assert.That(outcome.Stats.DirectoriesSkipped, Is.EqualTo(2));
        Assert.That(outcome.SkippedDirectories, Has.Count.EqualTo(2));
        Assert.That(
            outcome.SkippedDirectories,
            Does.Contain(".git"),
            "should be relative to the dropped folder, not an absolute path");
        Assert.That(outcome.SkippedDirectories, Does.Contain(".svn"));
        Assert.That(File.ReadAllText(Path.Combine(gitDir, "config")), Is.EqualTo("should not be touched\r\n"));
        Assert.That(File.ReadAllText(Path.Combine(svnDir, "entries")), Is.EqualTo("should not be touched\r\n"));
    }

    [Test]
    public void Run_MultipleFileTargets_NoOutput_BacksUpOriginalsFlatUnderTimestampedFolder()
    {
        var fileA = Path.Combine(this.tempDir, "a.txt");
        var fileB = Path.Combine(this.tempDir, "b.txt");
        File.WriteAllText(fileA, "one\r\n");
        File.WriteAllText(fileB, "two\r\n");
        var fixedTimestamp = new DateTime(2026, 1, 2, 3, 4, 5, 678);

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot, () => fixedTimestamp);
        var outcome = processor.Run([fileA, fileB], outputPath: null);

        var expectedSessionDir = Path.Combine(this.backupRoot, "2026-01-02 030405-678");
        Assert.That(outcome.BackupDirectory, Is.EqualTo(expectedSessionDir));
        Assert.That(File.ReadAllText(Path.Combine(expectedSessionDir, "a.txt")), Is.EqualTo("one\r\n"));
        Assert.That(File.ReadAllText(Path.Combine(expectedSessionDir, "b.txt")), Is.EqualTo("two\r\n"));
        Assert.That(File.ReadAllText(fileA), Is.EqualTo("one\n"), "source file should still be normalized");
    }

    [Test]
    public void Run_MultipleFiles_NoBackupsFlag_SkipsBackupEvenWhenTriggerConditionsMet()
    {
        var fileA = Path.Combine(this.tempDir, "a.txt");
        var fileB = Path.Combine(this.tempDir, "b.txt");
        File.WriteAllText(fileA, "one\r\n");
        File.WriteAllText(fileB, "two\r\n");

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
        var outcome = processor.Run([fileA, fileB], outputPath: null, disableBackups: true);

        Assert.That(outcome.BackupDirectory, Is.Null);
        Assert.That(Directory.Exists(this.backupRoot), Is.False);
    }

    [Test]
    public void Run_MultipleFiles_OutputDirectoryDoesNotExist_IsCreated()
    {
        var fileA = Path.Combine(this.tempDir, "a.txt");
        var fileB = Path.Combine(this.tempDir, "b.txt");
        File.WriteAllText(fileA, "a\n");
        File.WriteAllText(fileB, "b\n");
        var outDir = Path.Combine(this.tempDir, "does-not-exist-yet");

        var processor = new FileProcessor(new NormalizationOptions());
        var outcome = processor.Run([fileA, fileB], outDir);

        Assert.That(outcome.Success, Is.True);
        Assert.That(File.Exists(Path.Combine(outDir, "a.txt")), Is.True);
        Assert.That(File.Exists(Path.Combine(outDir, "b.txt")), Is.True);
    }

    [Test]
    public void Run_MultipleFiles_OutputIsExistingFile_FailsWithMisuseError()
    {
        var fileA = Path.Combine(this.tempDir, "a.txt");
        var fileB = Path.Combine(this.tempDir, "b.txt");
        File.WriteAllText(fileA, "a\n");
        File.WriteAllText(fileB, "b\n");
        var outputFile = Path.Combine(this.tempDir, "single-destination.txt");
        File.WriteAllText(outputFile, "existing\n");

        var processor = new FileProcessor(new NormalizationOptions());
        var outcome = processor.Run([fileA, fileB], outputFile);

        Assert.That(outcome.Success, Is.False);
        Assert.That(outcome.Error, Does.Contain("--output"));
    }

    [Test]
    public void Run_MultipleFiles_WithOutput_NoBackupCreated()
    {
        var fileA = Path.Combine(this.tempDir, "a.txt");
        var fileB = Path.Combine(this.tempDir, "b.txt");
        File.WriteAllText(fileA, "one\r\n");
        File.WriteAllText(fileB, "two\r\n");
        var outDir = Path.Combine(this.tempDir, "out");

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
        var outcome = processor.Run([fileA, fileB], outDir);

        Assert.That(outcome.BackupDirectory, Is.Null);
        Assert.That(Directory.Exists(this.backupRoot), Is.False);
    }

    [Test]
    public void Run_NonTextFile_IsSkippedAndReported()
    {
        var file = Path.Combine(this.tempDir, "binary.dat");
        File.WriteAllBytes(file, [0x00, 0x01, 0x02]);

        var processor = new FileProcessor(new NormalizationOptions());
        var outcome = processor.Run([file], outputPath: null);

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Stats.FilesSkipped, Is.EqualTo(1));
        Assert.That(outcome.Stats.FilesProcessed, Is.EqualTo(1), "the file was checked, even though it was skipped");
        Assert.That(outcome.Stats.FilesModified, Is.EqualTo(0));
        Assert.That(outcome.SkippedFiles, Has.Count.EqualTo(1));
        Assert.That(outcome.SkippedFiles[0].DetectedEncoding, Is.EqualTo(DetectedEncodingKind.Binary));
    }

    [Test]
    public void Run_SingleFileTarget_NoOutput_NoBackupsFlag_SkipsBackup()
    {
        var file = Path.Combine(this.tempDir, "a.txt");
        File.WriteAllText(file, "hello\r\n");

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
        var outcome = processor.Run([file], outputPath: null, disableBackups: true);

        Assert.That(outcome.BackupDirectory, Is.Null);
        Assert.That(Directory.Exists(this.backupRoot), Is.False);
    }

    [Test]
    public void Run_SingleFileTarget_NoOutput_StillCreatesBackup()
    {
        var file = Path.Combine(this.tempDir, "a.txt");
        File.WriteAllText(file, "hello\r\n");
        var fixedTimestamp = new DateTime(2026, 1, 2, 3, 4, 5, 678);

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot, () => fixedTimestamp);
        var outcome = processor.Run([file], outputPath: null);

        var expectedSessionDir = Path.Combine(this.backupRoot, "2026-01-02 030405-678");
        Assert.That(outcome.BackupDirectory, Is.EqualTo(expectedSessionDir));
        Assert.That(File.ReadAllText(Path.Combine(expectedSessionDir, "a.txt")), Is.EqualTo("hello\r\n"));
    }

    [Test]
    public void Run_SingleFile_AlreadyNormalized_NoOutput_IsLeftCompletelyUntouched()
    {
        var file = Path.Combine(this.tempDir, "a.txt");
        File.WriteAllText(file, "already fine\n");
        var beforeWriteTime = File.GetLastWriteTimeUtc(file);

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
        var outcome = processor.Run([file], outputPath: null);

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Stats.FilesProcessed, Is.EqualTo(1));
        Assert.That(outcome.Stats.FilesModified, Is.EqualTo(0));
        Assert.That(outcome.Stats.FilesBackedUp, Is.EqualTo(0));
        Assert.That(outcome.BackupDirectory, Is.Null, "no backup session should be created for a no-op run");
        Assert.That(Directory.Exists(this.backupRoot), Is.False);
        Assert.That(
            File.GetLastWriteTimeUtc(file),
            Is.EqualTo(beforeWriteTime),
            "file must not be rewritten when nothing changed");
    }

    [Test]
    public void Run_SingleFile_AlreadyNormalized_WithOutput_StillCopiedButNotCountedAsModified()
    {
        var file = Path.Combine(this.tempDir, "a.txt");
        File.WriteAllText(file, "already fine\n");
        var outFile = Path.Combine(this.tempDir, "out.txt");

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
        var outcome = processor.Run([file], outFile);

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Stats.FilesModified, Is.EqualTo(0));
        Assert.That(File.Exists(outFile), Is.True);
        Assert.That(File.ReadAllText(outFile), Is.EqualTo("already fine\n"));
    }

    [Test]
    public void Run_SingleFile_NoOutput_NormalizesInPlace()
    {
        var file = Path.Combine(this.tempDir, "a.txt");
        File.WriteAllBytes(file, [0xEF, 0xBB, 0xBF, .. "line1\r\nline2\r\n"u8.ToArray()]);

        var processor = new FileProcessor(new NormalizationOptions(), this.backupRoot);
        var outcome = processor.Run([file], outputPath: null);

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Stats.FilesProcessed, Is.EqualTo(1));
        Assert.That(outcome.Stats.FilesModified, Is.EqualTo(1));
        Assert.That(outcome.Stats.BomsChanged, Is.EqualTo(1));
        Assert.That(outcome.Stats.EolsChanged, Is.EqualTo(1));
        Assert.That(File.ReadAllText(file), Is.EqualTo("line1\nline2\n"));
    }

    [Test]
    public void Run_SingleFile_OutputEndsInSeparatorButDoesNotExistYet_CreatesItAndReusesOriginalFileName()
    {
        var file = Path.Combine(this.tempDir, "a.txt");
        File.WriteAllText(file, "hello\r\n");
        var outDir = Path.Combine(this.tempDir, "out") + Path.DirectorySeparatorChar;

        var processor = new FileProcessor(new NormalizationOptions());
        var outcome = processor.Run([file], outDir);

        Assert.That(outcome.Success, Is.True);
        var expected = Path.Combine(this.tempDir, "out", "a.txt");
        Assert.That(File.Exists(expected), Is.True);
        Assert.That(File.ReadAllText(expected), Is.EqualTo("hello\n"));
    }

    [Test]
    public void Run_SingleFile_OutputIsExistingDirectory_WritesUsingOriginalFileName()
    {
        var file = Path.Combine(this.tempDir, "a.txt");
        File.WriteAllText(file, "hello\r\n");
        var outDir = Path.Combine(this.tempDir, "out");
        Directory.CreateDirectory(outDir);

        var processor = new FileProcessor(new NormalizationOptions());
        var outcome = processor.Run([file], outDir);

        Assert.That(outcome.Success, Is.True);
        var expected = Path.Combine(outDir, "a.txt");
        Assert.That(File.Exists(expected), Is.True);
        Assert.That(File.ReadAllText(expected), Is.EqualTo("hello\n"));
        Assert.That(File.ReadAllText(file), Is.EqualTo("hello\r\n"), "source file must be left untouched");
    }

    [Test]
    public void Run_SingleFile_OutputIsNewFilePath_WritesToThatExactPath()
    {
        var file = Path.Combine(this.tempDir, "a.txt");
        File.WriteAllText(file, "hello\r\n");
        var outFile = Path.Combine(this.tempDir, "renamed.txt");

        var processor = new FileProcessor(new NormalizationOptions());
        var outcome = processor.Run([file], outFile);

        Assert.That(outcome.Success, Is.True);
        Assert.That(File.Exists(outFile), Is.True);
        Assert.That(File.ReadAllText(outFile), Is.EqualTo("hello\n"));
    }

    [Test]
    public void Run_TargetDoesNotExist_FailsWithClearError()
    {
        var processor = new FileProcessor(new NormalizationOptions());
        var outcome = processor.Run([Path.Combine(this.tempDir, "missing.txt")], outputPath: null);

        Assert.That(outcome.Success, Is.False);
        Assert.That(outcome.Error, Does.Contain("not found"));
    }

    [SetUp]
    public void SetUp()
    {
        this.tempDir = Path.Combine(Path.GetTempPath(), "normalize-text-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(this.tempDir);
        this.backupRoot = Path.Combine(Path.GetTempPath(), "normalize-text-tests-backup-" + Guid.NewGuid());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this.tempDir))
        {
            Directory.Delete(this.tempDir, recursive: true);
        }

        if (Directory.Exists(this.backupRoot))
        {
            Directory.Delete(this.backupRoot, recursive: true);
        }
    }
}

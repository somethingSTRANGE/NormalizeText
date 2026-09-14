// -------------------------------------------------------------------------------------
// <copyright file="CliOptionsParserTests.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Tests;

using NormalizeText.Core;

public class CliOptionsParserTests
{
    [Test]
    public void Parse_EolAndBomOptions_AreParsedCaseInsensitively()
    {
        var result = CliOptionsParser.Parse(["file.txt", "--eol", "CRLF", "--bom", "Force"]);

        Assert.That(result.Options!.EolHandling, Is.EqualTo(EolHandling.CrLf));
        Assert.That(result.Options!.BomHandling, Is.EqualTo(BomHandling.Force));
    }

    [Test]
    public void Parse_HelpFlag_ReturnsHelpKindEvenWithoutTargets()
    {
        var result = CliOptionsParser.Parse(["--help"]);

        Assert.That(result.Kind, Is.EqualTo(CliParseResultKind.Help));
    }

    [Test]
    public void Parse_InvalidBomValue_Fails()
    {
        var result = CliOptionsParser.Parse(["file.txt", "--bom", "bogus"]);

        Assert.That(result.Kind, Is.EqualTo(CliParseResultKind.Error));
        Assert.That(result.Error, Does.Contain("--bom"));
    }

    [Test]
    public void Parse_InvalidEolValue_Fails()
    {
        var result = CliOptionsParser.Parse(["file.txt", "--eol", "bogus"]);

        Assert.That(result.Kind, Is.EqualTo(CliParseResultKind.Error));
        Assert.That(result.Error, Does.Contain("--eol"));
    }

    [Test]
    public void Parse_MissingValueForEol_Fails()
    {
        var result = CliOptionsParser.Parse(["file.txt", "--eol"]);

        Assert.That(result.Kind, Is.EqualTo(CliParseResultKind.Error));
    }

    [Test]
    public void Parse_MultipleTargets_AreAllCaptured()
    {
        var result = CliOptionsParser.Parse(["a.txt", "b.txt", "some-folder"]);

        Assert.That(result.Options!.Targets, Is.EqualTo(new[] { "a.txt", "b.txt", "some-folder" }));
    }

    [Test]
    public void Parse_NoArguments_Fails()
    {
        var result = CliOptionsParser.Parse([]);

        Assert.That(result.Kind, Is.EqualTo(CliParseResultKind.Error));
    }

    [Test]
    public void Parse_NoBackupsFlag_SetsNoBackups()
    {
        var result = CliOptionsParser.Parse(["file.txt", "--no-backups"]);

        Assert.That(result.Options!.NoBackups, Is.True);
    }

    [Test]
    public void Parse_NoColorFlag_SetsNoColor()
    {
        var result = CliOptionsParser.Parse(["file.txt", "--no-color"]);

        Assert.That(result.Options!.NoColor, Is.True);
    }

    [Test]
    public void Parse_OutputOption_IsCaptured()
    {
        var result = CliOptionsParser.Parse(["file.txt", "--output", "C:\\out\\dest.txt"]);

        Assert.That(result.Options!.OutputPath, Is.EqualTo("C:\\out\\dest.txt"));
    }

    [Test]
    public void Parse_SilentAndQuiet_BothSetSilentFlag()
    {
        Assert.That(CliOptionsParser.Parse(["file.txt", "--silent"]).Options!.Silent, Is.True);
        Assert.That(CliOptionsParser.Parse(["file.txt", "--quiet"]).Options!.Silent, Is.True);
    }

    [Test]
    public void Parse_SingleTarget_UsesDefaults()
    {
        var result = CliOptionsParser.Parse(["file.txt"]);

        Assert.That(result.Kind, Is.EqualTo(CliParseResultKind.Ok));
        Assert.That(result.Options!.Targets, Is.EqualTo(new[] { "file.txt" }));
        Assert.That(result.Options!.BomHandling, Is.EqualTo(BomHandling.Strip));
        Assert.That(result.Options!.EolHandling, Is.EqualTo(EolHandling.Lf));
        Assert.That(result.Options!.OutputPath, Is.Null);
        Assert.That(result.Options!.Silent, Is.False);
        Assert.That(result.Options!.NoBackups, Is.False);
        Assert.That(result.Options!.NoColor, Is.False);
    }

    [Test]
    public void Parse_UnknownOption_Fails()
    {
        var result = CliOptionsParser.Parse(["file.txt", "--nope"]);

        Assert.That(result.Kind, Is.EqualTo(CliParseResultKind.Error));
        Assert.That(result.Error, Does.Contain("--nope"));
    }

    [Test]
    public void Parse_VersionFlag_ReturnsVersionKindEvenWithoutTargets()
    {
        Assert.That(CliOptionsParser.Parse(["--version"]).Kind, Is.EqualTo(CliParseResultKind.Version));
        Assert.That(CliOptionsParser.Parse(["-v"]).Kind, Is.EqualTo(CliParseResultKind.Version));
    }
}

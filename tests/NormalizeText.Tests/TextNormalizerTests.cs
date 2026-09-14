// -------------------------------------------------------------------------------------
// <copyright file="TextNormalizerTests.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Tests;

using System.Diagnostics.CodeAnalysis;
using System.Text;

using NormalizeText.Core;

public class TextNormalizerTests
{
    private static readonly byte[] utf8Bom = [0xEF, 0xBB, 0xBF];

    [Test]
    [SuppressMessage("ReSharper", "UseUtf8StringLiteral")]
    public void Normalize_BinaryContent_IsSkipped()
    {
        byte[] input = [0x41, 0x00, 0x42];
        var options = new NormalizationOptions();

        var result = TextNormalizer.Normalize(input, options);

        Assert.That(result.Skipped, Is.True);
        Assert.That(result.DetectedEncoding, Is.EqualTo(DetectedEncodingKind.Binary));
        Assert.That(result.OutputBytes, Is.Null);
    }

    [Test]
    public void Normalize_BomForce_AddsBomWhenMissing()
    {
        var input = Encoding.UTF8.GetBytes("hello\n");
        var options = new NormalizationOptions(BomHandling: BomHandling.Force);

        var result = TextNormalizer.Normalize(input, options);

        Assert.That(result.BomChanged, Is.True);
        Assert.That(result.OutputBytes!.Take(3).ToArray(), Is.EqualTo(utf8Bom));
    }

    [Test]
    public void Normalize_BomIgnore_PreservesExistingBomState()
    {
        var input = utf8Bom.Concat(Encoding.UTF8.GetBytes("hello\n")).ToArray();
        var options = new NormalizationOptions(BomHandling: BomHandling.Ignore);

        var result = TextNormalizer.Normalize(input, options);

        Assert.That(result.BomChanged, Is.False);
        Assert.That(result.OutputBytes!.Take(3).ToArray(), Is.EqualTo(utf8Bom));
    }

    [Test]
    public void Normalize_DefaultOptions_NoChangeNeeded_ReportsNoChanges()
    {
        var input = Encoding.UTF8.GetBytes("already\nnormalized\n");
        var options = new NormalizationOptions();

        var result = TextNormalizer.Normalize(input, options);

        Assert.That(result.Skipped, Is.False);
        Assert.That(result.BomChanged, Is.False);
        Assert.That(result.EolChanged, Is.False);
    }

    [Test]
    public void Normalize_DefaultOptions_StripsBomAndConvertsCrLfToLf()
    {
        var input = utf8Bom.Concat(Encoding.UTF8.GetBytes("line1\r\nline2\r\n")).ToArray();
        var options = new NormalizationOptions();

        var result = TextNormalizer.Normalize(input, options);

        Assert.That(result.Skipped, Is.False);
        Assert.That(result.BomChanged, Is.True);
        Assert.That(result.EolChanged, Is.True);
        Assert.That(Encoding.UTF8.GetString(result.OutputBytes!), Is.EqualTo("line1\nline2\n"));
    }

    [Test]
    public void Normalize_EolCrLf_ConvertsLfToCrLf()
    {
        var input = Encoding.UTF8.GetBytes("line1\nline2\n");
        var options = new NormalizationOptions(EolHandling: EolHandling.CrLf);

        var result = TextNormalizer.Normalize(input, options);

        Assert.That(result.EolChanged, Is.True);
        Assert.That(Encoding.UTF8.GetString(result.OutputBytes!), Is.EqualTo("line1\r\nline2\r\n"));
    }

    [Test]
    public void Normalize_EolIgnore_LeavesLineEndingsUntouched()
    {
        var input = Encoding.UTF8.GetBytes("line1\r\nline2\n");
        var options = new NormalizationOptions(EolHandling: EolHandling.Ignore);

        var result = TextNormalizer.Normalize(input, options);

        Assert.That(result.EolChanged, Is.False);
        Assert.That(Encoding.UTF8.GetString(result.OutputBytes!), Is.EqualTo("line1\r\nline2\n"));
    }

    [Test]
    public void Normalize_LoneCr_ConvertsToLf()
    {
        var input = Encoding.UTF8.GetBytes("line1\rline2\r");
        var options = new NormalizationOptions();

        var result = TextNormalizer.Normalize(input, options);

        Assert.That(Encoding.UTF8.GetString(result.OutputBytes!), Is.EqualTo("line1\nline2\n"));
    }

    [Test]
    public void Normalize_Utf16Content_IsSkipped()
    {
        byte[] input = [0xFF, 0xFE, 0x68, 0x00, 0x69, 0x00];
        var options = new NormalizationOptions();

        var result = TextNormalizer.Normalize(input, options);

        Assert.That(result.Skipped, Is.True);
        Assert.That(result.DetectedEncoding, Is.EqualTo(DetectedEncodingKind.Utf16LittleEndian));
    }
}

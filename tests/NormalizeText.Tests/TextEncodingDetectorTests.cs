// -------------------------------------------------------------------------------------
// <copyright file="TextEncodingDetectorTests.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Tests;

using System.Diagnostics.CodeAnalysis;
using System.Text;

using NormalizeText.Core;

public class TextEncodingDetectorTests
{
    [Test]
    [SuppressMessage("ReSharper", "UseUtf8StringLiteral")]
    public void Detect_ContentWithNullByte_ReturnsBinary()
    {
        byte[] bytes = [0x41, 0x00, 0x42, 0x43];

        var result = TextEncodingDetector.Detect(bytes);

        Assert.That(result, Is.EqualTo(DetectedEncodingKind.Binary));
    }

    [Test]
    public void Detect_InvalidUtf8ByteSequence_ReturnsInvalidUtf8()
    {
        byte[] bytes = [0x41, 0xFF, 0xFE, 0x01, 0x80, 0x42];

        var result = TextEncodingDetector.Detect(bytes);

        Assert.That(result, Is.EqualTo(DetectedEncodingKind.InvalidUtf8));
    }

    [Test]
    public void Detect_PlainAsciiWithoutBom_ReturnsUtf8NoBom()
    {
        var bytes = Encoding.ASCII.GetBytes("hello world");

        var result = TextEncodingDetector.Detect(bytes);

        Assert.That(result, Is.EqualTo(DetectedEncodingKind.Utf8NoBom));
    }

    [Test]
    public void Detect_Utf16BeBom_ReturnsUtf16BigEndian()
    {
        byte[] bytes = [0xFE, 0xFF, 0x00, 0x68, 0x00, 0x69];

        var result = TextEncodingDetector.Detect(bytes);

        Assert.That(result, Is.EqualTo(DetectedEncodingKind.Utf16BigEndian));
    }

    [Test]
    public void Detect_Utf16LeBom_ReturnsUtf16LittleEndian()
    {
        byte[] bytes = [0xFF, 0xFE, 0x68, 0x00, 0x69, 0x00];

        var result = TextEncodingDetector.Detect(bytes);

        Assert.That(result, Is.EqualTo(DetectedEncodingKind.Utf16LittleEndian));
    }

    [Test]
    public void Detect_Utf32BeBom_ReturnsUtf32BigEndian()
    {
        byte[] bytes = [0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x68];

        var result = TextEncodingDetector.Detect(bytes);

        Assert.That(result, Is.EqualTo(DetectedEncodingKind.Utf32BigEndian));
    }

    [Test]
    public void Detect_Utf32LeBom_ReturnsUtf32LittleEndian()
    {
        byte[] bytes = [0xFF, 0xFE, 0x00, 0x00, 0x68, 0x00, 0x00, 0x00];

        var result = TextEncodingDetector.Detect(bytes);

        Assert.That(result, Is.EqualTo(DetectedEncodingKind.Utf32LittleEndian));
    }

    [Test]
    public void Detect_Utf8Bom_ReturnsUtf8WithBom()
    {
        byte[] bom = [0xEF, 0xBB, 0xBF];
        var bytes = bom.Concat(Encoding.UTF8.GetBytes("hello")).ToArray();

        var result = TextEncodingDetector.Detect(bytes);

        Assert.That(result, Is.EqualTo(DetectedEncodingKind.Utf8WithBom));
    }

    [Test]
    public void Detect_ValidMultiByteUtf8WithoutBom_ReturnsUtf8NoBom()
    {
        var bytes = Encoding.UTF8.GetBytes("café résumé 日本語");

        var result = TextEncodingDetector.Detect(bytes);

        Assert.That(result, Is.EqualTo(DetectedEncodingKind.Utf8NoBom));
    }
}

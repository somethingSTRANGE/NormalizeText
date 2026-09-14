// -------------------------------------------------------------------------------------
// <copyright file="TextEncodingDetector.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

using System.Text;

public static class TextEncodingDetector
{
    private static readonly byte[] utf16BeBom = [0xFE, 0xFF];

    private static readonly byte[] utf16LeBom = [0xFF, 0xFE];

    private static readonly byte[] utf32BeBom = [0x00, 0x00, 0xFE, 0xFF];

    private static readonly byte[] utf32LeBom = [0xFF, 0xFE, 0x00, 0x00];

    private static readonly byte[] utf8Bom = [0xEF, 0xBB, 0xBF];

    public static DetectedEncodingKind Detect(byte[] content)
    {
        if (StartsWith(content, utf32LeBom))
        {
            return DetectedEncodingKind.Utf32LittleEndian;
        }

        if (StartsWith(content, utf32BeBom))
        {
            return DetectedEncodingKind.Utf32BigEndian;
        }

        if (StartsWith(content, utf8Bom))
        {
            return DetectedEncodingKind.Utf8WithBom;
        }

        if (StartsWith(content, utf16LeBom))
        {
            return DetectedEncodingKind.Utf16LittleEndian;
        }

        if (StartsWith(content, utf16BeBom))
        {
            return DetectedEncodingKind.Utf16BigEndian;
        }

        if (Array.IndexOf(content, (byte)0) >= 0)
        {
            return DetectedEncodingKind.Binary;
        }

        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            strictUtf8.GetString(content);
            return DetectedEncodingKind.Utf8NoBom;
        }
        catch (DecoderFallbackException)
        {
            return DetectedEncodingKind.InvalidUtf8;
        }
    }

    private static bool StartsWith(byte[] content, byte[] prefix)
    {
        if (content.Length < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (content[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }
}

// -------------------------------------------------------------------------------------
// <copyright file="DetectedEncodingKind.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

public enum DetectedEncodingKind
{
    Utf8NoBom,

    Utf8WithBom,

    Utf16LittleEndian,

    Utf16BigEndian,

    Utf32LittleEndian,

    Utf32BigEndian,

    Binary,

    InvalidUtf8,
}

public static class DetectedEncodingKindExtensions
{
    public static bool IsUtf8(this DetectedEncodingKind kind)
    {
        return kind is DetectedEncodingKind.Utf8NoBom or DetectedEncodingKind.Utf8WithBom;
    }

    public static string ToDisplayName(this DetectedEncodingKind kind)
    {
        return kind switch
            {
                DetectedEncodingKind.Utf8NoBom => "UTF-8 (no BOM)",
                DetectedEncodingKind.Utf8WithBom => "UTF-8 (with BOM)",
                DetectedEncodingKind.Utf16LittleEndian => "UTF-16 LE (BOM)",
                DetectedEncodingKind.Utf16BigEndian => "UTF-16 BE (BOM)",
                DetectedEncodingKind.Utf32LittleEndian => "UTF-32 LE (BOM)",
                DetectedEncodingKind.Utf32BigEndian => "UTF-32 BE (BOM)",
                DetectedEncodingKind.Binary => "binary / unknown (non-text)",
                DetectedEncodingKind.InvalidUtf8 => "unknown (invalid UTF-8 byte sequence)",
                _ => "unknown",
            };
    }
}

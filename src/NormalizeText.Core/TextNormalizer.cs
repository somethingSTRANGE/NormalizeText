// -------------------------------------------------------------------------------------
// <copyright file="TextNormalizer.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

using System.Text;

public static class TextNormalizer
{
    private static readonly byte[] utf8BomBytes = [0xEF, 0xBB, 0xBF];

    public static NormalizationResult Normalize(byte[] input, NormalizationOptions options)
    {
        var detected = TextEncodingDetector.Detect(input);
        if (!detected.IsUtf8())
        {
            return NormalizationResult.Skip(detected);
        }

        var hadBom = detected == DetectedEncodingKind.Utf8WithBom;
        var contentStart = hadBom ? utf8BomBytes.Length : 0;

        var text = Encoding.UTF8.GetString(input, contentStart, input.Length - contentStart);
        var normalizedText = ApplyEol(text, options.EolHandling, out var eolChanged);

        var wantBom = options.BomHandling switch
            {
                BomHandling.Ignore => hadBom,
                BomHandling.Strip => false,
                BomHandling.Force => true,
                _ => hadBom,
            };
        var bomChanged = wantBom != hadBom;

        var bodyBytes = Encoding.UTF8.GetBytes(normalizedText);
        byte[] output;
        if (wantBom)
        {
            output = new byte[utf8BomBytes.Length + bodyBytes.Length];
            Buffer.BlockCopy(utf8BomBytes, 0, output, 0, utf8BomBytes.Length);
            Buffer.BlockCopy(bodyBytes, 0, output, utf8BomBytes.Length, bodyBytes.Length);
        }
        else
        {
            output = bodyBytes;
        }

        return NormalizationResult.Success(output, bomChanged, eolChanged, detected);
    }

    private static string ApplyEol(string text, EolHandling handling, out bool changed)
    {
        if (handling == EolHandling.Ignore)
        {
            changed = false;
            return text;
        }

        var lf = ToLf(text);
        var result = handling == EolHandling.CrLf ? LfToCrLf(lf) : lf;
        changed = !string.Equals(result, text, StringComparison.Ordinal);
        return result;
    }

    private static string LfToCrLf(string lfText)
    {
        var sb = new StringBuilder(lfText.Length + 16);
        foreach (var c in lfText)
        {
            if (c == '\n')
            {
                sb.Append('\r').Append('\n');
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string ToLf(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r')
            {
                sb.Append('\n');
                if ((i + 1 < text.Length) && (text[i + 1] == '\n'))
                {
                    i++;
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}

// -------------------------------------------------------------------------------------
// <copyright file="NormalizationResult.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

public sealed class NormalizationResult
{
    private NormalizationResult(
        bool skipped,
        DetectedEncodingKind detectedEncoding,
        byte[]? outputBytes,
        bool bomChanged,
        bool eolChanged)
    {
        this.Skipped = skipped;
        this.DetectedEncoding = detectedEncoding;
        this.OutputBytes = outputBytes;
        this.BomChanged = bomChanged;
        this.EolChanged = eolChanged;
    }

    public bool BomChanged { get; }

    public DetectedEncodingKind DetectedEncoding { get; }

    public bool EolChanged { get; }

    public byte[]? OutputBytes { get; }

    public bool Skipped { get; }

    public static NormalizationResult Skip(DetectedEncodingKind detectedEncoding)
    {
        return new NormalizationResult(
            skipped: true,
            detectedEncoding,
            outputBytes: null,
            bomChanged: false,
            eolChanged: false);
    }

    public static NormalizationResult Success(
        byte[] outputBytes,
        bool bomChanged,
        bool eolChanged,
        DetectedEncodingKind detectedEncoding)
    {
        return new NormalizationResult(skipped: false, detectedEncoding, outputBytes, bomChanged, eolChanged);
    }
}

// -------------------------------------------------------------------------------------
// <copyright file="NormalizationOptions.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

public sealed record NormalizationOptions(
    BomHandling BomHandling = BomHandling.Strip,
    EolHandling EolHandling = EolHandling.Lf);

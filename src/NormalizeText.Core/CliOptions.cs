// -------------------------------------------------------------------------------------
// <copyright file="CliOptions.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

public sealed record CliOptions(
    IReadOnlyList<string> Targets,
    BomHandling BomHandling,
    EolHandling EolHandling,
    string? OutputPath,
    bool Silent,
    bool NoBackups,
    bool NoColor);

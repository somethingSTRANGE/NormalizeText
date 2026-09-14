// -------------------------------------------------------------------------------------
// <copyright file="FailedFileInfo.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

public sealed record FailedFileInfo(string Path, string Reason);

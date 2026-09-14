// -------------------------------------------------------------------------------------
// <copyright file="RunStats.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

public sealed class RunStats
{
    public int BomsChanged { get; set; }

    public int DirectoriesSkipped { get; set; }

    public int EolsChanged { get; set; }

    public int FilesBackedUp { get; set; }

    public int FilesFailed { get; set; }

    public int FilesModified { get; set; }

    public int FilesProcessed { get; set; }

    public int FilesSkipped { get; set; }
}

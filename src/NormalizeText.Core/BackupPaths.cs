// -------------------------------------------------------------------------------------
// <copyright file="BackupPaths.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

public static class BackupPaths
{
    public static string BuildSessionDirectoryName(DateTime timestamp)
    {
        return timestamp.ToString("yyyy-MM-dd HHmmss-fff");
    }
}

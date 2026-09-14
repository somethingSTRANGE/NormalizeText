// -------------------------------------------------------------------------------------
// <copyright file="BackupPathsTests.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Tests;

using NormalizeText.Core;

public class BackupPathsTests
{
    [Test]
    public void BuildSessionDirectoryName_FormatsWithMillisecondsForCollisionAvoidance()
    {
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, 678);

        var name = BackupPaths.BuildSessionDirectoryName(timestamp);

        Assert.That(name, Is.EqualTo("2026-01-02 030405-678"));
    }
}

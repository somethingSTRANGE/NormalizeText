// -------------------------------------------------------------------------------------
// <copyright file="CliParseResult.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

public enum CliParseResultKind
{
    Ok,

    Help,

    Version,

    Error,
}

public sealed class CliParseResult
{
    private CliParseResult(CliParseResultKind kind, CliOptions? options, string? error)
    {
        this.Kind = kind;
        this.Options = options;
        this.Error = error;
    }

    public string? Error { get; }

    public CliParseResultKind Kind { get; }

    public CliOptions? Options { get; }

    public static CliParseResult Fail(string error)
    {
        return new CliParseResult(CliParseResultKind.Error, null, error);
    }

    public static CliParseResult Help()
    {
        return new CliParseResult(CliParseResultKind.Help, null, null);
    }

    public static CliParseResult Ok(CliOptions options)
    {
        return new CliParseResult(CliParseResultKind.Ok, options, null);
    }

    public static CliParseResult Version()
    {
        return new CliParseResult(CliParseResultKind.Version, null, null);
    }
}

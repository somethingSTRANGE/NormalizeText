// -------------------------------------------------------------------------------------
// <copyright file="CliOptionsParser.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace NormalizeText.Core;

public static class CliOptionsParser
{
    public static CliParseResult Parse(string[] args)
    {
        var state = new ParserState();

        for (var i = 0; i < args.Length; i++)
        {
            var failure = ParseArgument(args, ref i, state);
            if (failure is not null)
            {
                return failure;
            }
        }

        return Finish(state);
    }

    private static CliParseResult Finish(ParserState state)
    {
        if (state.Help)
        {
            return CliParseResult.Help();
        }

        if (state.Version)
        {
            return CliParseResult.Version();
        }

        if (state.Targets.Count == 0)
        {
            return CliParseResult.Fail("No file or folder target specified.");
        }

        return CliParseResult.Ok(
            new CliOptions(
                state.Targets,
                state.BomHandling,
                state.EolHandling,
                state.OutputPath,
                state.Silent,
                state.NoBackups,
                state.NoColor));
    }

    private static CliParseResult? ParseArgument(string[] args, ref int i, ParserState state)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--help":
            case "-h":
            case "/?":
                state.Help = true;
                return null;

            case "--version":
            case "-v":
                state.Version = true;
                return null;

            case "--silent":
            case "--quiet":
                state.Silent = true;
                return null;

            case "--no-backups":
                state.NoBackups = true;
                return null;

            case "--no-color":
                state.NoColor = true;
                return null;

            case "--eol":
                return ParseEolOption(args, ref i, state);

            case "--bom":
                return ParseBomOption(args, ref i, state);

            case "--output":
                return ParseOutputOption(args, ref i, state);

            default:
                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    return CliParseResult.Fail($"Unknown option '{arg}'.");
                }

                state.Targets.Add(arg);
                return null;
        }
    }

    private static CliParseResult? ParseBomOption(string[] args, ref int i, ParserState state)
    {
        if (!TryTakeValue(args, ref i, out var bomValue))
        {
            return CliParseResult.Fail("--bom requires a value (ignore, strip, force).");
        }

        if (!TryParseBom(bomValue, out var bomHandling))
        {
            return CliParseResult.Fail($"Invalid --bom value '{bomValue}'. Expected ignore, strip, or force.");
        }

        state.BomHandling = bomHandling;
        return null;
    }

    private static CliParseResult? ParseEolOption(string[] args, ref int i, ParserState state)
    {
        if (!TryTakeValue(args, ref i, out var eolValue))
        {
            return CliParseResult.Fail("--eol requires a value (ignore, lf, crlf).");
        }

        if (!TryParseEol(eolValue, out var eolHandling))
        {
            return CliParseResult.Fail($"Invalid --eol value '{eolValue}'. Expected ignore, lf, or crlf.");
        }

        state.EolHandling = eolHandling;
        return null;
    }

    private static CliParseResult? ParseOutputOption(string[] args, ref int i, ParserState state)
    {
        if (!TryTakeValue(args, ref i, out var outputValue))
        {
            return CliParseResult.Fail("--output requires a path.");
        }

        state.OutputPath = outputValue;
        return null;
    }

    private static bool TryParseBom(string value, out BomHandling handling)
    {
        switch (value.ToLowerInvariant())
        {
            case "ignore":
                handling = BomHandling.Ignore;
                return true;
            case "strip":
                handling = BomHandling.Strip;
                return true;
            case "force":
                handling = BomHandling.Force;
                return true;
            default:
                handling = default;
                return false;
        }
    }

    private static bool TryParseEol(string value, out EolHandling handling)
    {
        switch (value.ToLowerInvariant())
        {
            case "ignore":
                handling = EolHandling.Ignore;
                return true;
            case "lf":
                handling = EolHandling.Lf;
                return true;
            case "crlf":
                handling = EolHandling.CrLf;
                return true;
            default:
                handling = default;
                return false;
        }
    }

    private static bool TryTakeValue(string[] args, ref int i, out string value)
    {
        if (i + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        value = args[++i];
        return true;
    }

    private sealed class ParserState
    {
        public BomHandling BomHandling { get; set; } = BomHandling.Strip;

        public EolHandling EolHandling { get; set; } = EolHandling.Lf;

        public bool Help { get; set; }

        public bool NoBackups { get; set; }

        public bool NoColor { get; set; }

        public string? OutputPath { get; set; }

        public bool Silent { get; set; }

        public List<string> Targets { get; } = [];

        public bool Version { get; set; }
    }
}

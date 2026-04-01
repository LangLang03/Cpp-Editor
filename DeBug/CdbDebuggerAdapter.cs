using System.Diagnostics;
using System.Text.RegularExpressions;

namespace C__Editor;

internal sealed class CdbDebuggerAdapter : CommandLineDebuggerAdapterBase
{
    private static readonly Regex PromptPattern = new(@"[0-9A-Fa-f]+:[0-9A-Fa-f]+>\s*$", RegexOptions.Compiled);
    private static readonly Regex ExitCodePattern = new(
        @"(?:exit\s+code|exited\s+with\s+code|exit\s+process)\s*[=:]?\s*(?<code>-?\d+)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StackFramePattern = new(
        @"^\s*[0-9A-Fa-f]{2,}\s+[0-9A-Fa-f`]+\s+[0-9A-Fa-f`]+\s+",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex FunctionPattern = new(
        @"^\s*[0-9A-Fa-f]{2,}\s+[0-9A-Fa-f`]+\s+[0-9A-Fa-f`]+\s+(?<func>[^\s]+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex SourceAtPattern = new(
        @"\[(?<file>[A-Za-z]:\\[^\]\r\n]+?\.(?:c|cc|cpp|cxx|h|hpp))\s*@\s*(?<line>\d+)\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SourceParenPattern = new(
        @"(?<file>[A-Za-z]:\\[^\(\r\n]+?\.(?:c|cc|cpp|cxx|h|hpp))\((?<line>\d+)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VariableLinePattern = new(
        @"^(?:(?<address>[0-9A-Fa-f`]+)\s+@[^=]+\s+)?(?<name>[A-Za-z_~][A-Za-z0-9_:$<>~]*)\s*=\s*(?<value>.+)$",
        RegexOptions.Compiled);
    private static readonly Regex TrailingIdentifierPattern = new(
        @"(?<name>[A-Za-z_~][A-Za-z0-9_:$<>~]*)\s*(?:\[[^\]]+\])?\s*$",
        RegexOptions.Compiled);

    public override DebuggerKind Kind => DebuggerKind.Cdb;

    protected override Regex PromptRegex => PromptPattern;

    protected override ProcessStartInfo CreateStartInfo(string debuggerExecutablePath, DebugLaunchRequest request)
    {
        var targetPath = NormalizePathForDebugger(request.ExecutablePath);
        return new ProcessStartInfo
        {
            FileName = debuggerExecutablePath,
            Arguments = $"-lines \"{EscapeQuotes(targetPath)}\""
        };
    }

    protected override IReadOnlyList<string> BuildInitializationCommands(DebugLaunchRequest request)
    {
        var executablePath = NormalizePathForDebugger(request.ExecutablePath);
        var executableDirectory = Path.GetDirectoryName(executablePath);
        var commands = new List<string>
        {
            ".symfix",
        };

        if (!string.IsNullOrWhiteSpace(executableDirectory))
        {
            commands.Add($".sympath+ \"{EscapeQuotes(executableDirectory)}\"");
        }

        var sourceDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in request.BreakpointsByFile.Keys)
        {
            var normalized = NormalizePathForDebugger(filePath);
            var directory = Path.GetDirectoryName(normalized);
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            sourceDirectories.Add(directory);
        }

        foreach (var sourceDirectory in sourceDirectories)
        {
            commands.Add($".srcpath+ \"{EscapeQuotes(sourceDirectory)}\"");
        }

        commands.AddRange(new[]
        {
            ".reload /f"
        });

        var breakpointCount = 0;
        foreach (var pair in request.BreakpointsByFile)
        {
            var filePath = NormalizePathForDebugger(pair.Key);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                continue;
            }

            foreach (var line in pair.Value.OrderBy(value => value))
            {
                if (line <= 0)
                {
                    continue;
                }

                commands.Add(BuildSourceLineBreakpointCommand(filePath, line));
                breakpointCount++;
            }
        }

        if (breakpointCount == 0)
        {
            commands.Add("bu main");
        }

        return commands;
    }

    protected override string GetStartCommand() => "g";

    protected override string GetContinueCommand() => "g";

    protected override string GetStepIntoCommand() => "t";

    protected override string GetStepOverCommand() => "p";

    protected override string GetStepOutCommand() => "gu";

    protected override string GetQuitCommand() => "q";

    protected override bool TryParseExitCode(string output, out int exitCode)
    {
        exitCode = 0;
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        if (output.Contains("No runnable debuggees", StringComparison.OrdinalIgnoreCase))
        {
            exitCode = 0;
            return true;
        }

        if (output.Contains("NtTerminateProcess+0x14", StringComparison.OrdinalIgnoreCase) &&
            !output.Contains("Breakpoint", StringComparison.OrdinalIgnoreCase))
        {
            exitCode = 0;
            return true;
        }

        if (output.Contains("Process", StringComparison.OrdinalIgnoreCase) &&
            output.Contains("exited", StringComparison.OrdinalIgnoreCase))
        {
            exitCode = 0;
            return true;
        }

        var match = ExitCodePattern.Match(output);
        if (!match.Success)
        {
            return false;
        }

        if (int.TryParse(match.Groups["code"].Value, out var code))
        {
            exitCode = code;
            return true;
        }

        if (output.Contains("exit process", StringComparison.OrdinalIgnoreCase))
        {
            exitCode = 0;
            return true;
        }

        return false;
    }

    protected override async Task<DebugPauseSnapshot> BuildPauseSnapshotAsync(string commandOutput, CancellationToken cancellationToken)
    {
        var stackOutput = await QueryAsync("k", cancellationToken).ConfigureAwait(false);
        var localsOutput = await QueryAsync("dv", cancellationToken).ConfigureAwait(false);
        if (IsNoLocalsResult(localsOutput))
        {
            localsOutput = await QueryAsync("dv /v", cancellationToken).ConfigureAwait(false);
        }

        var locationOutput = await QueryAsync("l+t", cancellationToken).ConfigureAwait(false);

        var functionName = ParseFunctionName(stackOutput, commandOutput);
        var (filePath, lineNumber) = ParseLocation(commandOutput, stackOutput, locationOutput);
        var stackDepth = StackFramePattern.Matches(stackOutput).Count;

        return new DebugPauseSnapshot
        {
            FilePath = filePath,
            Line = lineNumber,
            Column = 0,
            FunctionName = functionName,
            Reason = ParseStopReason(commandOutput),
            CanStepOut = stackDepth > 1 && !string.IsNullOrWhiteSpace(functionName),
            Variables = ParseVariables(localsOutput),
            RawOutput = commandOutput
        };
    }

    private static string ParseFunctionName(params string[] outputs)
    {
        foreach (var output in outputs)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                continue;
            }

            var match = FunctionPattern.Match(output);
            if (match.Success)
            {
                var value = match.Groups["func"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return string.Empty;
    }

    private static (string filePath, int lineNumber) ParseLocation(params string[] outputs)
    {
        foreach (var output in outputs)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                continue;
            }

            foreach (Match match in SourceAtPattern.Matches(output))
            {
                if (!match.Success)
                {
                    continue;
                }

                var file = match.Groups["file"].Value.Trim();
                if (string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                if (!int.TryParse(match.Groups["line"].Value, out var line) || line <= 0)
                {
                    continue;
                }

                return (NormalizePathForDebugger(file), line);
            }

            foreach (Match match in SourceParenPattern.Matches(output))
            {
                if (!match.Success)
                {
                    continue;
                }

                var file = match.Groups["file"].Value.Trim();
                if (string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                if (!int.TryParse(match.Groups["line"].Value, out var line) || line <= 0)
                {
                    continue;
                }

                return (NormalizePathForDebugger(file), line);
            }
        }

        return (string.Empty, 0);
    }

    private static IReadOnlyList<DebugVariableValue> ParseVariables(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<DebugVariableValue>();
        }

        var variables = new List<DebugVariableValue>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("No local", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var splitIndex = line.IndexOf('=');
            if (splitIndex <= 0 || splitIndex >= line.Length - 1)
            {
                continue;
            }

            var leftPart = line[..splitIndex].Trim();
            var name = ExtractVariableName(leftPart);
            var value = NormalizeCdbValue(line[(splitIndex + 1)..].Trim());
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!seenNames.Add(name))
            {
                continue;
            }

            variables.Add(new DebugVariableValue
            {
                Name = name,
                Value = value
            });
        }

        return variables;
    }

    private static DebugStopReason ParseStopReason(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return DebugStopReason.Unknown;
        }

        if (output.Contains("Breakpoint", StringComparison.OrdinalIgnoreCase))
        {
            return DebugStopReason.Breakpoint;
        }

        if (output.Contains("first chance exception", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("second chance exception", StringComparison.OrdinalIgnoreCase))
        {
            return DebugStopReason.Exception;
        }

        if (output.Contains("Single step", StringComparison.OrdinalIgnoreCase))
        {
            return DebugStopReason.StepComplete;
        }

        return DebugStopReason.Unknown;
    }

    private static string EscapeQuotes(string value)
    {
        return value.Replace("\"", "\\\"");
    }

    private static string BuildSourceLineBreakpointCommand(string filePath, int line)
    {
        var normalizedPath = filePath.Replace("`", "``");
        return $"bu `{normalizedPath}:{line}`";
    }

    private static bool IsNoLocalsResult(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return true;
        }

        return output.Contains("No local", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("no symbols", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractVariableName(string leftPart)
    {
        if (string.IsNullOrWhiteSpace(leftPart))
        {
            return string.Empty;
        }

        var directMatch = VariableLinePattern.Match($"{leftPart} = _");
        if (directMatch.Success)
        {
            var directName = directMatch.Groups["name"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(directName))
            {
                return directName;
            }
        }

        var trailingMatch = TrailingIdentifierPattern.Match(leftPart);
        if (trailingMatch.Success)
        {
            var trailingName = trailingMatch.Groups["name"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(trailingName))
            {
                return trailingName;
            }
        }

        return leftPart.Trim();
    }

    private static string NormalizeCdbValue(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var trimmed = rawValue.Trim();
        if (trimmed.StartsWith("0n", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(trimmed[2..], out var decimalValue))
        {
            return decimalValue.ToString();
        }

        return trimmed;
    }
}

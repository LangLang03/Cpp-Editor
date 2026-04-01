using System.Diagnostics;
using System.Text.RegularExpressions;

namespace C__Editor;

internal sealed class GdbDebuggerAdapter : CommandLineDebuggerAdapterBase
{
    private static readonly Regex PromptPattern = new(@"\(gdb\)\s*$", RegexOptions.Compiled);
    private static readonly Regex StackFramePattern = new(@"^#\d+\s", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex FunctionPattern = new(
        @"#0\s+(?:0x[0-9a-fA-F]+\s+in\s+)?(?<func>[^\s(]+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex LocationPattern = new(
        @"at\s+(?<file>(?:[A-Za-z]:)?[^:\r\n]+?\.(?:c|cc|cpp|cxx|h|hpp)):(?<line>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ExitCodePattern = new(
        @"(?:exit(?:ed)?\s+with\s+code|exited\s+with\s+status)\s+(?<code>-?\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override DebuggerKind Kind => DebuggerKind.Gdb;

    protected override Regex PromptRegex => PromptPattern;

    protected override ProcessStartInfo CreateStartInfo(string debuggerExecutablePath, DebugLaunchRequest request)
    {
        return new ProcessStartInfo
        {
            FileName = debuggerExecutablePath,
            Arguments = "--quiet --nx"
        };
    }

    protected override IReadOnlyList<string> BuildInitializationCommands(DebugLaunchRequest request)
    {
        var commands = new List<string>
        {
            "set pagination off",
            "set confirm off",
            "set breakpoint pending on",
            "set print pretty on",
            $"file \"{EscapeQuotes(NormalizePathForDebugger(request.ExecutablePath).Replace('\\', '/'))}\""
        };
        var breakpointCommands = new HashSet<string>(StringComparer.Ordinal);

        var breakpointCount = 0;
        foreach (var pair in request.BreakpointsByFile)
        {
            var normalizedFile = NormalizePathForDebugger(pair.Key).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalizedFile))
            {
                continue;
            }

            foreach (var line in pair.Value.OrderBy(value => value))
            {
                if (line <= 0)
                {
                    continue;
                }

                AddBreakpointCommand(commands, breakpointCommands, normalizedFile, line);

                var fileNameOnly = Path.GetFileName(normalizedFile);
                if (!string.IsNullOrWhiteSpace(fileNameOnly) &&
                    !string.Equals(fileNameOnly, normalizedFile, StringComparison.Ordinal))
                {
                    AddBreakpointCommand(commands, breakpointCommands, fileNameOnly, line);
                }

                breakpointCount++;
            }
        }

        if (breakpointCount == 0)
        {
            commands.Add("break main");
        }

        return commands;
    }

    protected override string GetStartCommand() => "run";

    protected override string GetContinueCommand() => "continue";

    protected override string GetStepIntoCommand() => "step";

    protected override string GetStepOverCommand() => "next";

    protected override string GetStepOutCommand() => "finish";

    protected override string GetQuitCommand() => "quit";

    protected override bool TryParseExitCode(string output, out int exitCode)
    {
        exitCode = 0;
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        var match = ExitCodePattern.Match(output);
        if (match.Success && int.TryParse(match.Groups["code"].Value, out exitCode))
        {
            return true;
        }

        if (output.Contains("exited normally", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("Program exited normally", StringComparison.OrdinalIgnoreCase))
        {
            exitCode = 0;
            return true;
        }

        return false;
    }

    protected override async Task<DebugPauseSnapshot> BuildPauseSnapshotAsync(string commandOutput, CancellationToken cancellationToken)
    {
        var stackOutput = await QueryAsync("where", cancellationToken).ConfigureAwait(false);
        var frameOutput = await QueryAsync("frame", cancellationToken).ConfigureAwait(false);
        var localsOutput = await QueryAsync("info locals", cancellationToken).ConfigureAwait(false);

        var functionName = ParseFunctionName(frameOutput, stackOutput, commandOutput);
        var (filePath, lineNumber) = ParseLocation(frameOutput, stackOutput, commandOutput);
        var stackDepth = StackFramePattern.Matches(stackOutput).Count;

        return new DebugPauseSnapshot
        {
            FilePath = filePath,
            Line = lineNumber,
            Column = 0,
            FunctionName = functionName,
            Reason = ParseStopReason(commandOutput),
            CanStepOut = stackDepth > 1 && !string.IsNullOrWhiteSpace(functionName),
            Variables = ParseLocalVariables(localsOutput),
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

            foreach (Match match in LocationPattern.Matches(output))
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

                file = NormalizePathForDebugger(file).Replace('/', '\\');
                return (file, line);
            }
        }

        return (string.Empty, 0);
    }

    private static IReadOnlyList<DebugVariableValue> ParseLocalVariables(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<DebugVariableValue>();
        }

        var variables = new List<DebugVariableValue>();
        foreach (var rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("No locals", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var delimiter = line.IndexOf('=');
            if (delimiter <= 0 || delimiter >= line.Length - 1)
            {
                continue;
            }

            var name = line[..delimiter].Trim();
            var value = line[(delimiter + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(name))
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

        if (output.Contains("received signal", StringComparison.OrdinalIgnoreCase))
        {
            return DebugStopReason.Exception;
        }

        if (output.Contains("Temporary breakpoint", StringComparison.OrdinalIgnoreCase))
        {
            return DebugStopReason.Entry;
        }

        if (output.Contains("Single stepping", StringComparison.OrdinalIgnoreCase))
        {
            return DebugStopReason.StepComplete;
        }

        return DebugStopReason.Unknown;
    }

    private static string EscapeQuotes(string value)
    {
        return value.Replace("\"", "\\\"");
    }

    private static void AddBreakpointCommand(
        ICollection<string> commands,
        ISet<string> deduplicate,
        string fileSpec,
        int line)
    {
        if (string.IsNullOrWhiteSpace(fileSpec) || line <= 0)
        {
            return;
        }

        var command = $"break \"{EscapeQuotes(fileSpec)}\":{line}";
        if (deduplicate.Add(command))
        {
            commands.Add(command);
        }
    }
}

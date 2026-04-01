using System.Diagnostics;
using System.Text.RegularExpressions;

namespace C__Editor;

internal sealed class LldbDebuggerAdapter : CommandLineDebuggerAdapterBase
{
    private static readonly Regex PromptPattern = new(@"\(lldb\)\s*$", RegexOptions.Compiled);
    private static readonly Regex StackFramePattern = new(@"frame #\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FunctionPattern = new(
        @"frame #0:.*?`(?<func>[^\s+`]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LocationPattern = new(
        @"at\s+(?<file>(?:[A-Za-z]:)?[^:\r\n]+?\.(?:c|cc|cpp|cxx|h|hpp)):(?<line>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ExitCodePattern = new(
        @"exited\s+with\s+status\s*=\s*(?<code>-?\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VariablePattern = new(
        @"^\((?<type>.+?)\)\s+(?<name>[^\s=]+)\s*=\s*(?<value>.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public override DebuggerKind Kind => DebuggerKind.Lldb;

    protected override Regex PromptRegex => PromptPattern;

    protected override ProcessStartInfo CreateStartInfo(string debuggerExecutablePath, DebugLaunchRequest request)
    {
        return new ProcessStartInfo
        {
            FileName = debuggerExecutablePath,
            Arguments = "--no-lldbinit"
        };
    }

    protected override IReadOnlyList<string> BuildInitializationCommands(DebugLaunchRequest request)
    {
        var commands = new List<string>
        {
            "settings set auto-confirm true",
            $"target create \"{EscapeQuotes(NormalizePathForDebugger(request.ExecutablePath))}\""
        };
        var breakpointCommands = new HashSet<string>(StringComparer.Ordinal);

        var breakpointCount = 0;
        foreach (var pair in request.BreakpointsByFile)
        {
            var file = NormalizePathForDebugger(pair.Key);
            if (string.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            foreach (var line in pair.Value.OrderBy(value => value))
            {
                if (line <= 0)
                {
                    continue;
                }

                AddBreakpointCommand(commands, breakpointCommands, file, line);

                var fileNameOnly = Path.GetFileName(file);
                if (!string.IsNullOrWhiteSpace(fileNameOnly) &&
                    !string.Equals(fileNameOnly, file, StringComparison.Ordinal))
                {
                    AddBreakpointCommand(commands, breakpointCommands, fileNameOnly, line);
                }

                breakpointCount++;
            }
        }

        if (breakpointCount == 0)
        {
            commands.Add("breakpoint set --name main");
        }

        return commands;
    }

    protected override string GetStartCommand() => "run";

    protected override string GetContinueCommand() => "continue";

    protected override string GetStepIntoCommand() => "thread step-in";

    protected override string GetStepOverCommand() => "thread step-over";

    protected override string GetStepOutCommand() => "thread step-out";

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

        if (output.Contains("Process", StringComparison.OrdinalIgnoreCase) &&
            output.Contains("exited", StringComparison.OrdinalIgnoreCase))
        {
            exitCode = 0;
            return true;
        }

        return false;
    }

    protected override async Task<DebugPauseSnapshot> BuildPauseSnapshotAsync(string commandOutput, CancellationToken cancellationToken)
    {
        var stackOutput = await QueryAsync("bt", cancellationToken).ConfigureAwait(false);
        var frameOutput = await QueryAsync("frame info", cancellationToken).ConfigureAwait(false);
        var varsOutput = await QueryAsync("frame variable", cancellationToken).ConfigureAwait(false);

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
            Variables = ParseVariables(varsOutput),
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
                var functionName = match.Groups["func"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(functionName))
                {
                    return functionName;
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
        foreach (Match match in VariablePattern.Matches(output))
        {
            if (!match.Success)
            {
                continue;
            }

            var name = match.Groups["name"].Value.Trim();
            var value = match.Groups["value"].Value.Trim();
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

        if (output.Contains("breakpoint", StringComparison.OrdinalIgnoreCase))
        {
            return DebugStopReason.Breakpoint;
        }

        if (output.Contains("stop reason", StringComparison.OrdinalIgnoreCase) &&
            output.Contains("signal", StringComparison.OrdinalIgnoreCase))
        {
            return DebugStopReason.Exception;
        }

        if (output.Contains("step", StringComparison.OrdinalIgnoreCase))
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

        var command = $"breakpoint set --file \"{EscapeQuotes(fileSpec)}\" --line {line}";
        if (deduplicate.Add(command))
        {
            commands.Add(command);
        }
    }
}

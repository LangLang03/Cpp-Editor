using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace C__Editor;

public partial class MainEditorForm
{
    private static readonly Regex CompilerDiagnosticRegex = new(
        @"^(?<file>.+):(?<line>\d+):(?<column>\d+):\s*(?<severity>fatal error|error|warning|note):\s*(?<message>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DiagnosticCodeRegex = new(@"\[(?<code>[^\]]+)\]\s*$", RegexOptions.Compiled);
    private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

    private readonly object buildRunSyncRoot = new();
    private CancellationTokenSource? buildRunCts;
    private Process? activeBuildRunProcess;
    private bool isBuildRunOperationActive;

    private string? lastBuiltExecutablePath;
    private string? lastBuiltSourcePath;

    private sealed class BuildContext
    {
        public string CompilerPath { get; init; } = string.Empty;

        public string WorkingDirectory { get; init; } = string.Empty;

        public string SourceFilePath { get; init; } = string.Empty;

        public string OutputExecutablePath { get; init; } = string.Empty;

        public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    }

    private sealed class CompilerDiagnosticItem
    {
        public string Severity { get; init; } = "错误";

        public string File { get; init; } = string.Empty;

        public int Line { get; init; }

        public int Column { get; init; }

        public string Code { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;
    }

    private async Task ExecuteBuildCommandAsync(string commandId)
    {
        if (!TryBeginBuildRunOperation(commandId))
        {
            AppendBuildOutput("已有正在执行的编译/运行任务，请先停止。");
            return;
        }

        try
        {
            var token = GetBuildRunCancellationToken();
            switch (commandId)
            {
                case EditorCommandIds.BuildCompile:
                    await CompileCurrentDocumentAsync(forceRebuild: false, token);
                    break;
                case EditorCommandIds.BuildRebuild:
                    await CompileCurrentDocumentAsync(forceRebuild: true, token);
                    break;
                case EditorCommandIds.BuildRun:
                    if (await CompileCurrentDocumentAsync(forceRebuild: false, token))
                    {
                        await RunCompiledExecutableAsync(token);
                    }

                    break;
                default:
                    AppendBuildOutput($"未知编译命令: {commandId}");
                    break;
            }
        }
        finally
        {
            EndBuildRunOperation();
        }
    }

    private void ExecuteDebugCommandInternal(string commandId)
    {
        switch (commandId)
        {
            case EditorCommandIds.DebugStop:
                StopActiveBuildRunOperation("已停止当前任务。");
                break;
            case EditorCommandIds.DebugStart:
                AppendBuildOutput("调试器尚未接入，已改为直接运行。");
                ExecuteBuildCommand(EditorCommandIds.BuildRun);
                break;
            case EditorCommandIds.DebugStepInto:
            case EditorCommandIds.DebugStepOver:
                AppendBuildOutput("单步调试尚未接入。");
                break;
            default:
                AppendBuildOutput($"未知调试命令: {commandId}");
                break;
        }
    }

    private async Task<bool> CompileCurrentDocumentAsync(bool forceRebuild, CancellationToken token)
    {
        if (!TryCreateBuildContext(out var context))
        {
            return false;
        }

        ClearCompileDiagnostics();
        SelectBottomTab(0);

        AppendBuildOutput($"开始编译: {context.SourceFilePath}");
        AppendBuildOutput($"使用编译器: {context.CompilerPath}");
        AppendBuildOutput($"工作目录: {context.WorkingDirectory}");
        AppendBuildOutput($"命令: {QuoteArgumentForDisplay(context.CompilerPath)} {BuildDisplayArguments(context.Arguments)}");
        AppendBuildOutput($"通过 cmd 执行: {BuildCmdDisplayText(context.CompilerPath, context.Arguments)}");

        if (forceRebuild)
        {
            TryDeleteFile(context.OutputExecutablePath);
            AppendBuildOutput("已执行重新编译前清理。");
        }

        try
        {
            var exitCode = await RunProcessAsync(
                context.CompilerPath,
                context.Arguments,
                context.WorkingDirectory,
                stdoutLine => HandleCompilerOutputLine(stdoutLine),
                stderrLine => HandleCompilerOutputLine(stderrLine),
                token);

            if (exitCode != 0)
            {
                AppendBuildOutput($"编译失败，退出码: {exitCode}");
                return false;
            }

            if (!File.Exists(context.OutputExecutablePath))
            {
                AppendBuildOutput($"编译结束，但未找到输出文件: {context.OutputExecutablePath}");
                return false;
            }

            lastBuiltExecutablePath = context.OutputExecutablePath;
            lastBuiltSourcePath = context.SourceFilePath;
            TryCopyCompilerRuntimeDependencies(context.CompilerPath, context.OutputExecutablePath);

            AppendBuildOutput($"编译成功: {context.OutputExecutablePath}");
            return true;
        }
        catch (OperationCanceledException)
        {
            AppendBuildOutput("编译已取消。");
            return false;
        }
        catch (Exception ex)
        {
            AppendBuildOutput($"编译异常: {ex.Message}");
            return false;
        }
    }

    private async Task RunCompiledExecutableAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(lastBuiltExecutablePath) || !File.Exists(lastBuiltExecutablePath))
        {
            AppendBuildOutput("未找到可运行的可执行文件，请先编译成功。");
            return;
        }

        var executablePath = lastBuiltExecutablePath;
        var workingDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            workingDirectory = Environment.CurrentDirectory;
        }

        ClearRunOutput();
        SelectBottomTab(2);
        AppendRunStatus($"启动: {executablePath}");

        try
        {
            var exitCode = await RunProcessAsync(
                executablePath,
                Array.Empty<string>(),
                workingDirectory,
                stdoutLine => AppendRunOutputLine(stdoutLine),
                stderrLine => AppendRunOutputLine(stderrLine),
                token);

            AppendRunStatus($"程序退出，退出码: {exitCode}");
        }
        catch (OperationCanceledException)
        {
            AppendRunStatus("运行已取消。");
        }
        catch (Exception ex)
        {
            AppendRunStatus($"运行异常: {ex.Message}");
        }
    }

    private bool TryCreateBuildContext(out BuildContext context)
    {
        context = new BuildContext();

        if (!EnsureCurrentDocumentReadyForBuild(out var sourceFilePath))
        {
            return false;
        }

        var workingDirectory = ResolveBuildWorkingDirectory(sourceFilePath);
        var outputExecutablePath = ResolveOutputExecutablePath(sourceFilePath, workingDirectory, toolchainSettings);
        var compilerArguments = BuildCompilerArguments(sourceFilePath, outputExecutablePath, toolchainSettings);

        if (!EditorToolchainSettingsController.TryResolveCompilerExecutable(toolchainSettings, out var compilerPath, out var compilerDetail))
        {
            AppendBuildOutput(compilerDetail);
            MessageBox.Show(this, compilerDetail, "未找到编译器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        compilerPath = ResolveCompilerPathForSource(compilerPath, sourceFilePath);
        AppendBuildOutput(compilerDetail);

        context = new BuildContext
        {
            CompilerPath = compilerPath,
            WorkingDirectory = workingDirectory,
            SourceFilePath = sourceFilePath,
            OutputExecutablePath = outputExecutablePath,
            Arguments = compilerArguments
        };

        return true;
    }

    private bool EnsureCurrentDocumentReadyForBuild(out string sourceFilePath)
    {
        sourceFilePath = string.Empty;

        var state = GetSelectedDocumentState();
        if (state is null)
        {
            MessageBox.Show(this, "当前没有可编译的文档。", "编译", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        if (string.IsNullOrWhiteSpace(state.FilePath))
        {
            var savedAs = SaveCurrentDocumentAs();
            state = GetSelectedDocumentState();
            if (!savedAs || state is null || string.IsNullOrWhiteSpace(state.FilePath))
            {
                AppendBuildOutput("编译取消：未保存文件。");
                return false;
            }
        }

        if (state.IsDirty)
        {
            var decision = MessageBox.Show(
                this,
                "当前文件有未保存修改。编译前需要先保存，是否保存？",
                "编译",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (decision != DialogResult.Yes || !SaveCurrentDocument())
            {
                AppendBuildOutput("编译取消：文件未保存。");
                return false;
            }

            state = GetSelectedDocumentState();
            if (state is null || string.IsNullOrWhiteSpace(state.FilePath))
            {
                return false;
            }
        }

        var normalizedPath = Path.GetFullPath(state.FilePath);
        if (!File.Exists(normalizedPath))
        {
            AppendBuildOutput($"编译取消：文件不存在 -> {normalizedPath}");
            return false;
        }

        if (!IsCompilableSourceFile(normalizedPath))
        {
            MessageBox.Show(this, "当前仅支持编译 C/C++ 源文件（.c/.cpp/.cc/.cxx）。", "编译", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        sourceFilePath = normalizedPath;
        return true;
    }

    private static bool IsCompilableSourceFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".c" or ".cc" or ".cpp" or ".cxx";
    }

    private string ResolveBuildWorkingDirectory(string sourceFilePath)
    {
        try
        {
            if (treeProject is not null)
            {
                var preferred = GetTargetDirectory(treeProject.SelectedNode);
                if (!string.IsNullOrWhiteSpace(preferred) && Directory.Exists(preferred))
                {
                    return preferred;
                }
            }
        }
        catch
        {
            // Ignore project tree lookup issues.
        }

        var sourceDirectory = Path.GetDirectoryName(sourceFilePath);
        if (!string.IsNullOrWhiteSpace(sourceDirectory) && Directory.Exists(sourceDirectory))
        {
            return sourceDirectory;
        }

        return Environment.CurrentDirectory;
    }

    private static string ResolveOutputExecutablePath(string sourceFilePath, string workingDirectory, ToolchainSettingsConfig settings)
    {
        var relativeOutputDirectory = string.IsNullOrWhiteSpace(settings.BuildOutputDirectory)
            ? Path.Combine(".cppeditor", "build")
            : settings.BuildOutputDirectory.Trim();

        var outputDirectory = Path.IsPathRooted(relativeOutputDirectory)
            ? relativeOutputDirectory
            : Path.Combine(workingDirectory, relativeOutputDirectory);

        Directory.CreateDirectory(outputDirectory);
        return Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.exe");
    }

    private static IReadOnlyList<string> BuildCompilerArguments(
        string sourceFilePath,
        string outputExecutablePath,
        ToolchainSettingsConfig settings)
    {
        var arguments = ParseCommandLineArguments(settings.CompilerArguments).ToList();
        if (!arguments.Any(arg => arg.StartsWith("-fdiagnostics-color", StringComparison.OrdinalIgnoreCase)))
        {
            arguments.Add("-fdiagnostics-color=never");
        }

        arguments.Add(sourceFilePath);
        arguments.Add("-o");
        arguments.Add(outputExecutablePath);
        return arguments;
    }

    private static IEnumerable<string> ParseCommandLineArguments(string? argumentsText)
    {
        if (string.IsNullOrWhiteSpace(argumentsText))
        {
            yield break;
        }

        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < argumentsText.Length; i++)
        {
            var current = argumentsText[i];

            if (current == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(current))
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                continue;
            }

            if (current == '\\' &&
                i + 1 < argumentsText.Length &&
                argumentsText[i + 1] == '"')
            {
                builder.Append('"');
                i++;
                continue;
            }

            builder.Append(current);
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static string BuildDisplayArguments(IReadOnlyList<string> arguments)
    {
        return string.Join(" ", arguments.Select(QuoteArgumentForDisplay));
    }

    private static string QuoteArgumentForDisplay(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return "\"\"";
        }

        return argument.Any(char.IsWhiteSpace)
            ? $"\"{argument}\""
            : argument;
    }

    private string ResolveCompilerPathForSource(string compilerPath, string sourceFilePath)
    {
        if (!string.Equals(Path.GetExtension(sourceFilePath), ".c", StringComparison.OrdinalIgnoreCase))
        {
            return compilerPath;
        }

        var compilerDirectory = Path.GetDirectoryName(compilerPath);
        if (string.IsNullOrWhiteSpace(compilerDirectory))
        {
            return compilerPath;
        }

        var gccPath = Path.Combine(compilerDirectory, "gcc.exe");
        return File.Exists(gccPath) ? gccPath : compilerPath;
    }

    private async Task<int> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        Action<string> onStdout,
        Action<string> onStderr,
        CancellationToken token)
    {
        var cmdScriptPath = CreateTemporaryCmdScript(fileName, arguments);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /c {QuoteArgumentForCmd(cmdScriptPath)}",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            }
        };

        var stdoutCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                stdoutCompleted.TrySetResult(true);
                return;
            }

            try
            {
                onStdout(eventArgs.Data);
            }
            catch
            {
                // Keep process pumping even if one UI update fails.
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                stderrCompleted.TrySetResult(true);
                return;
            }

            try
            {
                onStderr(eventArgs.Data);
            }
            catch
            {
                // Keep process pumping even if one UI update fails.
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: cmd.exe /d /c {cmdScriptPath}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        SetActiveBuildRunProcess(process);
        try
        {
            using var cancellationRegistration = token.Register(() => TryTerminateProcess(process));
            await process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(stdoutCompleted.Task, stderrCompleted.Task).ConfigureAwait(false);

            if (token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }

            return process.ExitCode;
        }
        finally
        {
            try
            {
                process.CancelOutputRead();
            }
            catch
            {
                // Ignore cancellation failures.
            }

            try
            {
                process.CancelErrorRead();
            }
            catch
            {
                // Ignore cancellation failures.
            }

            TryDeleteFile(cmdScriptPath);
            SetActiveBuildRunProcess(null);
        }
    }

    private static string BuildCmdInvocation(string executablePath, IReadOnlyList<string> arguments)
    {
        var tokens = new List<string>(arguments.Count + 1)
        {
            QuoteArgumentForCmd(executablePath)
        };

        foreach (var argument in arguments)
        {
            tokens.Add(QuoteArgumentForCmd(argument));
        }

        return string.Join(" ", tokens);
    }

    private static string BuildCmdBody(string executablePath, IReadOnlyList<string> arguments)
    {
        return $"chcp 65001>nul & {BuildCmdInvocation(executablePath, arguments)}";
    }

    private static string QuoteArgumentForCmd(string argument)
    {
        if (argument is null)
        {
            return "\"\"";
        }

        var escaped = argument.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string CreateTemporaryCmdScript(string executablePath, IReadOnlyList<string> arguments)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"cppeditor_{Guid.NewGuid():N}.cmd");
        var lines = new[]
        {
            "@echo off",
            "chcp 65001>nul",
            BuildCmdInvocation(executablePath, arguments),
            "exit /b %errorlevel%"
        };

        File.WriteAllLines(scriptPath, lines, new UTF8Encoding(false));
        return scriptPath;
    }

    private static string BuildCmdDisplayText(string executablePath, IReadOnlyList<string> arguments)
    {
        return $"cmd /d /c {BuildCmdBody(executablePath, arguments)}";
    }

    private void HandleCompilerOutputLine(string line)
    {
        var normalized = NormalizeOutputLine(line);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        AppendBuildOutput(normalized);

        if (TryParseCompilerDiagnostic(normalized, out var diagnostic))
        {
            AddCompileDiagnostic(diagnostic);
        }
    }

    private bool TryParseCompilerDiagnostic(string line, out CompilerDiagnosticItem item)
    {
        item = new CompilerDiagnosticItem();
        var match = CompilerDiagnosticRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        var file = match.Groups["file"].Value.Trim();
        var severity = match.Groups["severity"].Value.Trim().ToLowerInvariant();
        var description = match.Groups["message"].Value.Trim();
        var code = ExtractDiagnosticCode(description);
        if (!string.IsNullOrWhiteSpace(code))
        {
            description = description.Replace($"[{code}]", string.Empty).Trim();
        }

        if (!int.TryParse(match.Groups["line"].Value, out var lineNumber))
        {
            lineNumber = 0;
        }

        if (!int.TryParse(match.Groups["column"].Value, out var columnNumber))
        {
            columnNumber = 0;
        }

        item = new CompilerDiagnosticItem
        {
            Severity = severity.Contains("warning", StringComparison.Ordinal) ? "警告"
                : severity.Contains("note", StringComparison.Ordinal) ? "提示"
                : "错误",
            File = file,
            Line = lineNumber,
            Column = columnNumber,
            Code = code,
            Description = description
        };

        return true;
    }

    private static string ExtractDiagnosticCode(string message)
    {
        var codeMatch = DiagnosticCodeRegex.Match(message ?? string.Empty);
        return codeMatch.Success
            ? codeMatch.Groups["code"].Value.Trim()
            : string.Empty;
    }

    private void AddCompileDiagnostic(CompilerDiagnosticItem item)
    {
        if (dgvCompileErrors is null)
        {
            return;
        }

        void AddRow()
        {
            dgvCompileErrors.Rows.Add(
                item.Severity,
                item.File,
                item.Line <= 0 ? string.Empty : item.Line.ToString(),
                item.Column <= 0 ? string.Empty : item.Column.ToString(),
                item.Code,
                item.Description);
        }

        if (dgvCompileErrors.InvokeRequired)
        {
            dgvCompileErrors.BeginInvoke(new Action(AddRow));
            return;
        }

        AddRow();
    }

    private void ClearCompileDiagnostics()
    {
        if (dgvCompileErrors is null)
        {
            return;
        }

        void ClearRows() => dgvCompileErrors.Rows.Clear();
        if (dgvCompileErrors.InvokeRequired)
        {
            dgvCompileErrors.BeginInvoke(new Action(ClearRows));
            return;
        }

        ClearRows();
    }

    private void ClearRunOutput()
    {
        if (rtbRunOutput is null)
        {
            return;
        }

        void ClearText() => rtbRunOutput.Clear();
        if (rtbRunOutput.InvokeRequired)
        {
            rtbRunOutput.BeginInvoke(new Action(ClearText));
            return;
        }

        ClearText();
    }

    private void AppendRunStatus(string message)
    {
        AppendRunOutputLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private void AppendRunOutputLine(string line)
    {
        if (rtbRunOutput is null)
        {
            return;
        }

        void Append()
        {
            rtbRunOutput.AppendText(line + Environment.NewLine);
            rtbRunOutput.SelectionStart = rtbRunOutput.TextLength;
            rtbRunOutput.ScrollToCaret();
        }

        if (rtbRunOutput.InvokeRequired)
        {
            rtbRunOutput.BeginInvoke(new Action(Append));
            return;
        }

        Append();
    }

    private void SelectBottomTab(int index)
    {
        if (tabBottom is null)
        {
            return;
        }

        void Select()
        {
            if (index >= 0 && index < tabBottom.TabPages.Count)
            {
                tabBottom.SelectedIndex = index;
            }
        }

        if (tabBottom.InvokeRequired)
        {
            tabBottom.BeginInvoke(new Action(Select));
            return;
        }

        Select();
    }

    private static string NormalizeOutputLine(string line)
    {
        return AnsiEscapeRegex.Replace(line ?? string.Empty, string.Empty);
    }

    private bool TryBeginBuildRunOperation(string commandId)
    {
        lock (buildRunSyncRoot)
        {
            if (isBuildRunOperationActive)
            {
                return false;
            }

            isBuildRunOperationActive = true;
            buildRunCts = new CancellationTokenSource();
        }

        UpdateBuildRunMenuState();
        AppendBuildOutput($"执行命令: {commandId}");
        return true;
    }

    private void EndBuildRunOperation()
    {
        CancellationTokenSource? ctsToDispose;
        lock (buildRunSyncRoot)
        {
            ctsToDispose = buildRunCts;
            buildRunCts = null;
            activeBuildRunProcess = null;
            isBuildRunOperationActive = false;
        }

        ctsToDispose?.Dispose();
        UpdateBuildRunMenuState();
    }

    private CancellationToken GetBuildRunCancellationToken()
    {
        lock (buildRunSyncRoot)
        {
            return buildRunCts?.Token ?? CancellationToken.None;
        }
    }

    private void SetActiveBuildRunProcess(Process? process)
    {
        lock (buildRunSyncRoot)
        {
            activeBuildRunProcess = process;
        }
    }

    private void StopActiveBuildRunOperation(string reason)
    {
        CancellationTokenSource? cts;
        Process? process;
        lock (buildRunSyncRoot)
        {
            cts = buildRunCts;
            process = activeBuildRunProcess;
        }

        if (cts is null)
        {
            AppendBuildOutput("当前没有正在执行的编译/运行任务。");
            return;
        }

        if (!cts.IsCancellationRequested)
        {
            cts.Cancel();
        }

        TryTerminateProcess(process);
        AppendBuildOutput(reason);
    }

    private static void TryTerminateProcess(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore process termination failures.
        }
    }

    private void UpdateBuildRunMenuState()
    {
        var isBusy = false;
        lock (buildRunSyncRoot)
        {
            isBusy = isBuildRunOperationActive;
        }

        void Apply()
        {
            if (menuBuildCompile is not null)
            {
                menuBuildCompile.Enabled = !isBusy;
            }

            if (menuBuildRebuild is not null)
            {
                menuBuildRebuild.Enabled = !isBusy;
            }

            if (menuBuildRun is not null)
            {
                menuBuildRun.Enabled = !isBusy;
            }

            if (menuDebugStart is not null)
            {
                menuDebugStart.Enabled = !isBusy;
            }

            if (menuDebugStop is not null)
            {
                menuDebugStop.Enabled = isBusy;
            }
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(Apply));
            return;
        }

        Apply();
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private void TryCopyCompilerRuntimeDependencies(string compilerPath, string outputExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(compilerPath) || string.IsNullOrWhiteSpace(outputExecutablePath))
        {
            return;
        }

        var compilerDirectory = Path.GetDirectoryName(compilerPath);
        var outputDirectory = Path.GetDirectoryName(outputExecutablePath);
        if (string.IsNullOrWhiteSpace(compilerDirectory) ||
            string.IsNullOrWhiteSpace(outputDirectory) ||
            !Directory.Exists(compilerDirectory) ||
            !Directory.Exists(outputDirectory))
        {
            return;
        }

        var candidateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "libstdc++-6.dll",
            "libwinpthread-1.dll"
        };

        try
        {
            foreach (var libgccPath in Directory.EnumerateFiles(compilerDirectory, "libgcc_s_*.dll", SearchOption.TopDirectoryOnly))
            {
                candidateNames.Add(Path.GetFileName(libgccPath));
            }
        }
        catch
        {
            // Ignore libgcc probing failures.
        }

        foreach (var fileName in candidateNames)
        {
            var sourcePath = Path.Combine(compilerDirectory, fileName);
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var targetPath = Path.Combine(outputDirectory, fileName);
            try
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
                AppendBuildOutput($"已复制运行时依赖: {fileName}");
            }
            catch (Exception ex)
            {
                AppendBuildOutput($"复制运行时依赖失败: {fileName} ({ex.Message})");
            }
        }
    }
}

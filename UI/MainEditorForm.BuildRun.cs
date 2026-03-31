using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace C__Editor;

public partial class MainEditorForm
{
    private static readonly Regex GnuCompilerDiagnosticRegex = new(
        @"^(?<file>.+):(?<line>\d+):(?<column>\d+):\s*(?<severity>fatal error|error|warning|note):\s*(?<message>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MsvcCompilerDiagnosticRegex = new(
        @"^(?<file>.+)\((?<line>\d+)(,(?<column>\d+))?\)\s*:\s*(?<severity>fatal error|error|warning|note)\s*(?<code>[A-Za-z]+\d+)\s*:\s*(?<message>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MsvcToolDiagnosticRegex = new(
        @"^(?<tool>cl|link)\s*:\s*(?<severity>fatal error|error|warning)\s*(?<code>[A-Za-z]+\d+)\s*:\s*(?<message>.*)$",
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
        public ToolchainId ToolchainId { get; init; } = ToolchainId.BuiltInMsvc;

        public ToolchainFamily ToolchainFamily { get; init; } = ToolchainFamily.Msvc;

        public string ToolchainDisplayName { get; init; } = string.Empty;

        public string ToolchainSource { get; init; } = string.Empty;

        public string CompilerPath { get; init; } = string.Empty;

        public string SetupScriptPath { get; init; } = string.Empty;

        public string WorkspaceRoot { get; init; } = string.Empty;

        public string WorkingDirectory { get; init; } = string.Empty;

        public string SourceFilePath { get; init; } = string.Empty;

        public IReadOnlyList<string> SourceFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> CompileListPatterns { get; init; } = Array.Empty<string>();

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
        ClearCompileOutput();
        SelectBottomTab(0);

        AppendBuildOutput($"开始编译: {context.SourceFilePath}");
        AppendBuildOutput($"使用工具链: {context.ToolchainDisplayName} ({context.ToolchainSource})");
        AppendBuildOutput($"使用编译器: {context.CompilerPath}");
        if (!string.IsNullOrWhiteSpace(context.SetupScriptPath))
        {
            AppendBuildOutput($"使用环境脚本: {context.SetupScriptPath}");
        }
        else if (context.ToolchainFamily == ToolchainFamily.Msvc)
        {
            AppendBuildOutput("未检测到 vcvars64.bat，将直接调用 cl.exe。");
        }

        AppendBuildOutput($"工作区根目录: {context.WorkspaceRoot}");
        AppendBuildOutput($"工作目录: {context.WorkingDirectory}");
        if (context.CompileListPatterns.Count > 0)
        {
            AppendBuildOutput($"编译列表: {WorkspaceCompileListController.GetConfigPath(context.WorkspaceRoot)}");
            AppendBuildOutput($"编译列表条目数: {context.CompileListPatterns.Count}，实际参与编译源文件数: {context.SourceFilePaths.Count}");
        }

        AppendBuildOutput($"命令: {QuoteArgumentForDisplay(context.CompilerPath)} {BuildDisplayArguments(context.Arguments)}");
        AppendBuildOutput($"通过 cmd 执行: {BuildCmdDisplayText(context.CompilerPath, context.Arguments, context.SetupScriptPath)}");

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
                token,
                context.SetupScriptPath);

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
        var workspaceRoot = ResolveWorkspaceRootForSource(sourceFilePath, workingDirectory);
        var compileListConfig = WorkspaceCompileListController.Load(workspaceRoot);
        var compileSources = ResolveBuildSourceFiles(sourceFilePath, workspaceRoot, compileListConfig.Include);
        var outputExecutablePath = ResolveOutputExecutablePath(sourceFilePath, workingDirectory, toolchainSettings);
        if (!EditorToolchainSettingsController.TryResolveSelectedToolchain(
                toolchainSettings,
                out var resolvedToolchain,
                out var resolveDetail))
        {
            AppendBuildOutput(resolveDetail);
            MessageBox.Show(this, resolveDetail, "工具链不可用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // Use build configuration specific arguments
        var buildConfigArgs = buildConfigurationSettings.GetArgumentsForCurrentConfig(resolvedToolchain.Id);
        var compilerArguments = BuildCompilerArguments(compileSources, outputExecutablePath, resolvedToolchain, buildConfigArgs);
        AppendBuildOutput(resolveDetail);
        AppendBuildOutput($"构建配置: {buildConfigurationSettings.Configuration}");
        if (resolvedToolchain.Family == ToolchainFamily.Msvc &&
            string.IsNullOrWhiteSpace(resolvedToolchain.SetupScriptPath))
        {
            AppendBuildOutput("警告: 未配置 vcvars64.bat，MSVC 编译可能因环境变量缺失而失败。");
        }

        context = new BuildContext
        {
            ToolchainId = resolvedToolchain.Id,
            ToolchainFamily = resolvedToolchain.Family,
            ToolchainDisplayName = resolvedToolchain.DisplayName,
            ToolchainSource = resolvedToolchain.Source,
            CompilerPath = resolvedToolchain.CompilerPath,
            SetupScriptPath = resolvedToolchain.SetupScriptPath,
            WorkspaceRoot = workspaceRoot,
            WorkingDirectory = workingDirectory,
            SourceFilePath = sourceFilePath,
            SourceFilePaths = compileSources,
            CompileListPatterns = compileListConfig.Include,
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

    private string ResolveWorkspaceRootForSource(string sourceFilePath, string fallbackWorkingDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return fallbackWorkingDirectory;
        }

        var normalizedSourcePath = Path.GetFullPath(sourceFilePath);
        var bestRoot = string.Empty;

        if (treeProject is not null)
        {
            foreach (TreeNode rootNode in treeProject.Nodes)
            {
                var nodeData = GetNodeData(rootNode);
                if (nodeData?.Kind != ExplorerNodeKind.Directory || string.IsNullOrWhiteSpace(nodeData.FullPath))
                {
                    continue;
                }

                string rootPath;
                try
                {
                    rootPath = Path.GetFullPath(nodeData.FullPath);
                }
                catch
                {
                    continue;
                }

                if (!IsPathInsideOrEqual(normalizedSourcePath, rootPath))
                {
                    continue;
                }

                if (rootPath.Length > bestRoot.Length)
                {
                    bestRoot = rootPath;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(bestRoot))
        {
            return bestRoot;
        }

        return fallbackWorkingDirectory;
    }

    private IReadOnlyList<string> ResolveBuildSourceFiles(
        string primarySourceFilePath,
        string workspaceRoot,
        IReadOnlyList<string> compileListPatterns)
    {
        var fallback = new List<string> { primarySourceFilePath };
        if (compileListPatterns is null || compileListPatterns.Count == 0)
        {
            return fallback;
        }

        var matchedFiles = WorkspaceCompileListController.ResolveFiles(workspaceRoot, compileListPatterns);
        if (matchedFiles.Count == 0)
        {
            AppendBuildOutput("编译列表已配置，但未匹配到任何文件，已回退为当前文件编译。");
            return fallback;
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in matchedFiles)
        {
            if (!IsCompilableSourceFile(filePath))
            {
                AppendBuildOutput($"跳过非源文件: {filePath}");
                continue;
            }

            var normalizedPath = Path.GetFullPath(filePath);
            if (seen.Add(normalizedPath))
            {
                result.Add(normalizedPath);
            }
        }

        if (result.Count > 0)
        {
            return result;
        }

        AppendBuildOutput("编译列表匹配到的文件都不是可编译源文件，已回退为当前文件编译。");
        return fallback;
    }

    private static bool IsPathInsideOrEqual(string targetPath, string directoryPath)
    {
        var normalizedTarget = Path.GetFullPath(targetPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedDirectory = Path.GetFullPath(directoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(normalizedTarget, normalizedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var directoryPrefix = normalizedDirectory + Path.DirectorySeparatorChar;
        return normalizedTarget.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase);
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
        IReadOnlyList<string> sourceFilePaths,
        string outputExecutablePath,
        ResolvedToolchainContext toolchainContext,
        string? customArguments = null)
    {
        var builder = CompilerCommandBuilderFactory.Get(toolchainContext.Family);
        var arguments = !string.IsNullOrWhiteSpace(customArguments)
            ? customArguments
            : toolchainContext.CompilerArguments;
        return builder.BuildArguments(
            sourceFilePaths,
            outputExecutablePath,
            arguments);
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

    private async Task<int> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        Action<string> onStdout,
        Action<string> onStderr,
        CancellationToken token,
        string? setupScriptPath = null)
    {
        var cmdScriptPath = CreateTemporaryCmdScript(fileName, arguments, setupScriptPath);

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

    private static string BuildCmdBody(string executablePath, IReadOnlyList<string> arguments, string? setupScriptPath = null)
    {
        if (string.IsNullOrWhiteSpace(setupScriptPath))
        {
            return $"chcp 65001>nul & {BuildCmdInvocation(executablePath, arguments)}";
        }

        return $"chcp 65001>nul & call {QuoteArgumentForCmd(setupScriptPath)} >nul & {BuildCmdInvocation(executablePath, arguments)}";
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

    private static string CreateTemporaryCmdScript(string executablePath, IReadOnlyList<string> arguments, string? setupScriptPath = null)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"cppeditor_{Guid.NewGuid():N}.cmd");
        var lines = new List<string>
        {
            "@echo off",
            "chcp 65001>nul"
        };

        if (!string.IsNullOrWhiteSpace(setupScriptPath))
        {
            lines.Add($"call {QuoteArgumentForCmd(setupScriptPath)} >nul");
            lines.Add("if errorlevel 1 exit /b %errorlevel%");
        }

        lines.Add(BuildCmdInvocation(executablePath, arguments));
        lines.Add("exit /b %errorlevel%");

        File.WriteAllLines(scriptPath, lines, new UTF8Encoding(false));
        return scriptPath;
    }

    private static string BuildCmdDisplayText(string executablePath, IReadOnlyList<string> arguments, string? setupScriptPath = null)
    {
        return $"cmd /d /c {BuildCmdBody(executablePath, arguments, setupScriptPath)}";
    }

    private void HandleCompilerOutputLine(string line)
    {
        var normalized = NormalizeOutputLine(line);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        AppendCompileOutputLine(normalized);

        if (TryParseCompilerDiagnostic(normalized, out var diagnostic))
        {
            AddCompileDiagnostic(diagnostic);
        }
    }

    private bool TryParseCompilerDiagnostic(string line, out CompilerDiagnosticItem item)
    {
        item = new CompilerDiagnosticItem();
        var msvcMatch = MsvcCompilerDiagnosticRegex.Match(line);
        if (msvcMatch.Success)
        {
            var file = msvcMatch.Groups["file"].Value.Trim();
            var severity = msvcMatch.Groups["severity"].Value.Trim().ToLowerInvariant();
            var code = msvcMatch.Groups["code"].Value.Trim();
            var description = msvcMatch.Groups["message"].Value.Trim();

            _ = int.TryParse(msvcMatch.Groups["line"].Value, out var lineNumber);
            _ = int.TryParse(msvcMatch.Groups["column"].Value, out var columnNumber);

            item = new CompilerDiagnosticItem
            {
                Severity = NormalizeSeverity(severity),
                File = file,
                Line = lineNumber,
                Column = columnNumber,
                Code = code,
                Description = description
            };

            return true;
        }

        var msvcToolMatch = MsvcToolDiagnosticRegex.Match(line);
        if (msvcToolMatch.Success)
        {
            var severity = msvcToolMatch.Groups["severity"].Value.Trim().ToLowerInvariant();
            var code = msvcToolMatch.Groups["code"].Value.Trim();
            var description = msvcToolMatch.Groups["message"].Value.Trim();
            var tool = msvcToolMatch.Groups["tool"].Value.Trim();

            item = new CompilerDiagnosticItem
            {
                Severity = NormalizeSeverity(severity),
                File = tool,
                Line = 0,
                Column = 0,
                Code = code,
                Description = description
            };

            return true;
        }

        var gnuMatch = GnuCompilerDiagnosticRegex.Match(line);
        if (!gnuMatch.Success)
        {
            return false;
        }

        var gnuFile = gnuMatch.Groups["file"].Value.Trim();
        var gnuSeverity = gnuMatch.Groups["severity"].Value.Trim().ToLowerInvariant();
        var gnuDescription = gnuMatch.Groups["message"].Value.Trim();
        var gnuCode = ExtractDiagnosticCode(gnuDescription);
        if (!string.IsNullOrWhiteSpace(gnuCode))
        {
            gnuDescription = gnuDescription.Replace($"[{gnuCode}]", string.Empty).Trim();
        }

        _ = int.TryParse(gnuMatch.Groups["line"].Value, out var gnuLineNumber);
        _ = int.TryParse(gnuMatch.Groups["column"].Value, out var gnuColumnNumber);

        item = new CompilerDiagnosticItem
        {
            Severity = NormalizeSeverity(gnuSeverity),
            File = gnuFile,
            Line = gnuLineNumber,
            Column = gnuColumnNumber,
            Code = gnuCode,
            Description = gnuDescription
        };

        return true;
    }

    private static string NormalizeSeverity(string severity)
    {
        return severity.Contains("warning", StringComparison.OrdinalIgnoreCase) ? "警告"
            : severity.Contains("note", StringComparison.OrdinalIgnoreCase) ? "提示"
            : "错误";
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

    private void ClearCompileOutput()
    {
        if (rtbBuildOutput is null)
        {
            return;
        }

        void ClearText() => rtbBuildOutput.Clear();
        if (rtbBuildOutput.InvokeRequired)
        {
            rtbBuildOutput.BeginInvoke(new Action(ClearText));
            return;
        }

        ClearText();
    }

    private void AppendCompileOutputLine(string line)
    {
        if (rtbBuildOutput is null)
        {
            return;
        }

        void Append()
        {
            rtbBuildOutput.AppendText(line + Environment.NewLine);
            rtbBuildOutput.SelectionStart = rtbBuildOutput.TextLength;
            rtbBuildOutput.ScrollToCaret();
        }

        if (rtbBuildOutput.InvokeRequired)
        {
            rtbBuildOutput.BeginInvoke(new Action(Append));
            return;
        }

        Append();
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
        AppendBuildOutput($"运行: {message}");
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

        var compilerFileName = Path.GetFileName(compilerPath);
        if (!string.Equals(compilerFileName, "g++.exe", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(compilerFileName, "gcc.exe", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(compilerFileName, "clang++.exe", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(compilerFileName, "clang.exe", StringComparison.OrdinalIgnoreCase))
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

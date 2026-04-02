namespace C__Editor;

public partial class MainEditorForm
{
    private const int DebugVariablesTabIndex = 3;

    private DebuggerSession? activeDebuggerSession;
    private CancellationTokenSource? debuggerSessionCts;
    private bool isDebuggerCommandInFlight;
    private bool isDebuggerPaused;
    private bool canDebuggerStepOut;
    private DebugPauseSnapshot? lastDebuggerPause;
    private DebugControlPopup? debugControlPopup;

    private async Task ExecuteDebugCommandInternalAsync(string commandId)
    {
        try
        {
            switch (commandId)
            {
                case EditorCommandIds.DebugStart:
                    if (activeDebuggerSession is null || !activeDebuggerSession.IsActive)
                    {
                        await StartDebuggerSessionAsync();
                    }
                    else
                    {
                        await ContinueDebuggerSessionAsync();
                    }

                    break;
                case EditorCommandIds.DebugStepInto:
                    await StepDebuggerAsync(EditorCommandIds.DebugStepInto);
                    break;
                case EditorCommandIds.DebugStepOver:
                    await StepDebuggerAsync(EditorCommandIds.DebugStepOver);
                    break;
                case EditorCommandIds.DebugStepOut:
                    await StepDebuggerAsync(EditorCommandIds.DebugStepOut);
                    break;
                case EditorCommandIds.DebugStop:
                    if (IsBuildRunOperationInProgress())
                    {
                        StopActiveBuildRunOperation("已停止当前任务。");
                    }

                    await StopActiveDebuggerSessionAsync("已停止调试。", requestDebuggerQuit: true);
                    break;
                default:
                    AppendBuildOutput($"未知调试命令: {commandId}");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            AppendBuildOutput("调试命令已取消。");
        }
        catch (Exception ex)
        {
            AppendBuildOutput($"调试异常: {ex.Message}");
            await StopActiveDebuggerSessionAsync(null, requestDebuggerQuit: false);
        }
    }

    private async Task StartDebuggerSessionAsync()
    {
        if (isDebuggerCommandInFlight)
        {
            AppendBuildOutput("调试命令正在执行，请稍后重试。");
            return;
        }

        SetDebuggerCommandInFlight(true);
        try
        {
            if (!TryBeginBuildRunOperation(EditorCommandIds.DebugStart))
            {
                AppendBuildOutput("已有正在执行的编译/运行任务，请先停止。");
                return;
            }

            var buildSucceeded = false;
            try
            {
                buildSucceeded = await CompileCurrentDocumentAsync(forceRebuild: false, GetBuildRunCancellationToken());
            }
            finally
            {
                EndBuildRunOperation();
            }

            if (!buildSucceeded)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(lastBuiltExecutablePath) || !File.Exists(lastBuiltExecutablePath))
            {
                AppendBuildOutput("调试取消：未找到可执行文件，请先确保编译成功。");
                return;
            }

            if (!EditorToolchainSettingsController.TryResolveSelectedToolchain(toolchainSettings, out var toolchain, out var detail))
            {
                AppendBuildOutput(detail);
                return;
            }

            if (!DebuggerExecutableResolver.TryResolve(toolchainSettings, toolchain, out var debuggerResolution, out var debugDetail))
            {
                AppendBuildOutput(debugDetail);
                MessageBox.Show(this, debugDetail, "调试器不可用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AppendBuildOutput(debugDetail);
            ApplyEditorDebugReadOnlyState(true);

            var launchRequest = BuildDebugLaunchRequest(lastBuiltExecutablePath, lastBuiltSourcePath);
            var breakpointFileCount = launchRequest.BreakpointsByFile.Count;
            var breakpointCount = launchRequest.BreakpointsByFile.Values.Sum(lines => lines.Count);
            AppendBuildOutput($"已加载断点: 文件 {breakpointFileCount}，断点 {breakpointCount}");
            debuggerSessionCts?.Dispose();
            debuggerSessionCts = new CancellationTokenSource();

            activeDebuggerSession = await DebuggerSession.CreateAsync(
                debuggerResolution,
                launchRequest,
                debuggerSessionCts.Token);

            AppendBuildOutput($"调试器已启动: {debuggerResolution.Kind}");
            var startResult = await activeDebuggerSession.StartExecutionAsync(debuggerSessionCts.Token);
            await ApplyDebuggerCommandResultAsync(startResult);
        }
        finally
        {
            SetDebuggerCommandInFlight(false);
        }
    }

    private async Task ContinueDebuggerSessionAsync()
    {
        if (!TryGetActivePausedDebugger(out var session))
        {
            return;
        }

        await ExecuteDebuggerActionAsync("继续调试", session, (target, token) => target.ContinueAsync(token));
    }

    private async Task StepDebuggerAsync(string stepCommandId)
    {
        if (!TryGetActivePausedDebugger(out var session))
        {
            return;
        }

        if (stepCommandId == EditorCommandIds.DebugStepOut && !canDebuggerStepOut)
        {
            AppendBuildOutput("当前不在可跳出的函数栈内，单步跳出不可用。");
            return;
        }

        switch (stepCommandId)
        {
            case EditorCommandIds.DebugStepInto:
                await ExecuteDebuggerActionAsync("单步进入", session, (target, token) => target.StepIntoAsync(token));
                break;
            case EditorCommandIds.DebugStepOver:
                await ExecuteDebuggerActionAsync("单步跳过", session, (target, token) => target.StepOverAsync(token));
                break;
            case EditorCommandIds.DebugStepOut:
                await ExecuteDebuggerActionAsync("单步跳出", session, (target, token) => target.StepOutAsync(token));
                break;
            default:
                AppendBuildOutput($"未知单步命令: {stepCommandId}");
                break;
        }
    }

    private async Task ExecuteDebuggerActionAsync(
        string actionName,
        DebuggerSession session,
        Func<DebuggerSession, CancellationToken, Task<DebugCommandResult>> action)
    {
        if (isDebuggerCommandInFlight)
        {
            AppendBuildOutput("调试命令正在执行，请稍后重试。");
            return;
        }

        SetDebuggerCommandInFlight(true);
        try
        {
            AppendBuildOutput(actionName);
            isDebuggerPaused = false;
            canDebuggerStepOut = false;
            UpdateDebugControlPopupState();
            UpdateBuildRunMenuState();

            var token = debuggerSessionCts?.Token ?? CancellationToken.None;
            var result = await action(session, token);
            await ApplyDebuggerCommandResultAsync(result);
        }
        finally
        {
            SetDebuggerCommandInFlight(false);
        }
    }

    private async Task ApplyDebuggerCommandResultAsync(DebugCommandResult result)
    {
        if (result.State == DebugExecutionState.Paused && result.Pause is not null)
        {
            ApplyDebuggerPausedState(result.Pause);
            return;
        }

        if (result.State == DebugExecutionState.Exited)
        {
            var exitCode = result.ExitCode ?? 0;
            AppendBuildOutput($"调试结束，退出码: {exitCode}");
            await StopActiveDebuggerSessionAsync(null, requestDebuggerQuit: false);
            return;
        }

        AppendBuildOutput("调试器返回未知状态。");
    }

    private void ApplyDebuggerPausedState(DebugPauseSnapshot pause)
    {
        lastDebuggerPause = pause;
        isDebuggerPaused = true;
        canDebuggerStepOut = pause.CanStepOut;

        var filePath = ResolveDebugPauseFilePath(pause.FilePath);
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            ShowFileInEditorPlaceholder(filePath);
            if (pause.Line > 0)
            {
                NavigateToEditorPositionZeroBased(pause.Line - 1, Math.Max(0, pause.Column));
            }
        }

        SetDebugExecutionLineMarker(filePath, pause.Line);
        UpdateDebugVariablesGrid(pause.Variables);
        SelectBottomTab(DebugVariablesTabIndex);
        ApplyEditorDebugReadOnlyState(true);

        ShowDebugControlPopup();
        UpdateBuildRunMenuState();

        var locationText = pause.Line > 0 && !string.IsNullOrWhiteSpace(filePath)
            ? $"{filePath}:{pause.Line}"
            : "(未知位置)";
        AppendBuildOutput($"已暂停: {locationText}");
    }

    private async Task StopActiveDebuggerSessionAsync(string? message, bool requestDebuggerQuit)
    {
        var session = activeDebuggerSession;
        activeDebuggerSession = null;

        if (session is not null)
        {
            try
            {
                debuggerSessionCts?.Cancel();
                if (requestDebuggerQuit)
                {
                    await session.StopAsync(CancellationToken.None);
                }

                await session.DisposeAsync();
            }
            catch
            {
                // Ignore session shutdown failures.
            }
        }

        debuggerSessionCts?.Dispose();
        debuggerSessionCts = null;

        isDebuggerPaused = false;
        canDebuggerStepOut = false;
        lastDebuggerPause = null;
        SetDebugExecutionLineMarker(null, -1);
        ClearDebugVariablesGrid();
        ApplyEditorDebugReadOnlyState(false);
        HideDebugControlPopup();

        if (!string.IsNullOrWhiteSpace(message))
        {
            AppendBuildOutput(message);
        }

        UpdateBuildRunMenuState();
    }

    private DebugLaunchRequest BuildDebugLaunchRequest(string executablePath, string? sourcePath)
    {
        var workingDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            workingDirectory = Environment.CurrentDirectory;
        }

        var workspaceRoot = ResolveWorkspaceRootForDebug(sourcePath, workingDirectory);
        var breakpoints = CollectWorkspaceBreakpoints(workspaceRoot);

        return new DebugLaunchRequest
        {
            ExecutablePath = executablePath,
            WorkingDirectory = workingDirectory,
            BreakpointsByFile = breakpoints
        };
    }

    private Dictionary<string, IReadOnlyCollection<int>> CollectWorkspaceBreakpoints(string workspaceRoot)
    {
        var merged = new Dictionary<string, SortedSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in WorkspaceBreakpointMarkerController.LoadAllLines(workspaceRoot))
        {
            var normalizedPath = NormalizeBreakpointFilePath(pair.Key);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            if (!merged.TryGetValue(normalizedPath, out var lines))
            {
                lines = new SortedSet<int>();
                merged[normalizedPath] = lines;
            }

            foreach (var line in pair.Value.Where(value => value > 0))
            {
                lines.Add(line);
            }
        }

        foreach (var pair in breakpointLinesByFile)
        {
            var normalizedPath = NormalizeBreakpointFilePath(pair.Key);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            if (!merged.TryGetValue(normalizedPath, out var lines))
            {
                lines = new SortedSet<int>();
                merged[normalizedPath] = lines;
            }

            foreach (var line in pair.Value.Where(value => value > 0))
            {
                lines.Add(line);
            }
        }

        return merged.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyCollection<int>)pair.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private string ResolveWorkspaceRootForDebug(string? sourcePath, string fallbackDirectory)
    {
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            var normalized = NormalizeBreakpointFilePath(sourcePath);
            var sourceDirectory = Path.GetDirectoryName(normalized);
            if (!string.IsNullOrWhiteSpace(normalized) && !string.IsNullOrWhiteSpace(sourceDirectory))
            {
                return ResolveWorkspaceRootForSource(normalized, sourceDirectory);
            }
        }

        return fallbackDirectory;
    }

    private string ResolveDebugPauseFilePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        var normalized = NormalizeBreakpointFilePath(rawPath);
        if (!string.IsNullOrWhiteSpace(normalized) && File.Exists(normalized))
        {
            return normalized;
        }

        if (!string.IsNullOrWhiteSpace(lastBuiltSourcePath))
        {
            var sourceDirectory = Path.GetDirectoryName(lastBuiltSourcePath);
            if (!string.IsNullOrWhiteSpace(sourceDirectory))
            {
                var candidate = NormalizeBreakpointFilePath(Path.Combine(sourceDirectory, rawPath));
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return normalized;
    }

    private void ApplyEditorDebugReadOnlyState(bool readOnly)
    {
        if (editorControlMain is null)
        {
            return;
        }

        editorControlMain.Settings.SetReadOnly(readOnly);
    }

    private bool TryGetActivePausedDebugger(out DebuggerSession session)
    {
        session = activeDebuggerSession!;
        if (activeDebuggerSession is null || !activeDebuggerSession.IsActive)
        {
            AppendBuildOutput("当前没有活动调试会话。");
            return false;
        }

        if (!activeDebuggerSession.IsPaused || !isDebuggerPaused)
        {
            AppendBuildOutput("调试器未暂停，当前单步命令不可用。");
            return false;
        }

        session = activeDebuggerSession;
        return true;
    }

    private bool IsBuildRunOperationInProgress()
    {
        lock (buildRunSyncRoot)
        {
            return isBuildRunOperationActive;
        }
    }

    private bool IsDebuggerSessionActive()
    {
        return activeDebuggerSession is not null && activeDebuggerSession.IsActive;
    }

    private void SetDebuggerCommandInFlight(bool inFlight)
    {
        isDebuggerCommandInFlight = inFlight;
        UpdateDebugControlPopupState();
        UpdateBuildRunMenuState();
    }

    private void ShowDebugControlPopup()
    {
        if (debugControlPopup is null || debugControlPopup.IsDisposed)
        {
            debugControlPopup = new DebugControlPopup();
            EditorThemeController.ApplyFlatTheme(uiSettings.ThemeId, debugControlPopup);
            debugControlPopup.StepIntoRequested += (_, _) => ExecuteDebugCommand(EditorCommandIds.DebugStepInto);
            debugControlPopup.StepOverRequested += (_, _) => ExecuteDebugCommand(EditorCommandIds.DebugStepOver);
            debugControlPopup.StepOutRequested += (_, _) => ExecuteDebugCommand(EditorCommandIds.DebugStepOut);
            debugControlPopup.ContinueRequested += (_, _) => ExecuteDebugCommand(EditorCommandIds.DebugStart);
            debugControlPopup.StopRequested += (_, _) => ExecuteDebugCommand(EditorCommandIds.DebugStop);
        }

        if (!debugControlPopup.Visible)
        {
            debugControlPopup.Show(this);
        }

        PositionDebugControlPopup();
        UpdateDebugControlPopupState();
    }

    private void HideDebugControlPopup()
    {
        if (debugControlPopup is null || debugControlPopup.IsDisposed)
        {
            return;
        }

        debugControlPopup.Hide();
    }

    private void UpdateDebugControlPopupState()
    {
        if (debugControlPopup is null || debugControlPopup.IsDisposed)
        {
            return;
        }

        var hasActiveSession = IsDebuggerSessionActive();
        var canControl = hasActiveSession && isDebuggerPaused && !isDebuggerCommandInFlight;

        debugControlPopup.UpdateState(
            canContinue: canControl,
            canStepInto: canControl,
            canStepOver: canControl,
            canStepOut: canControl && canDebuggerStepOut,
            canStop: hasActiveSession && !isDebuggerCommandInFlight);
    }

    private void PositionDebugControlPopup()
    {
        if (debugControlPopup is null || debugControlPopup.IsDisposed)
        {
            return;
        }

        var workingArea = Screen.FromControl(this).WorkingArea;
        var maxWidth = Math.Max(420, workingArea.Width - 20);
        if (debugControlPopup.Width > maxWidth)
        {
            debugControlPopup.Width = maxWidth;
        }

        var x = Right - debugControlPopup.Width - 28;
        var y = Top + menuMain.Height + 38;

        var maxX = Math.Max(workingArea.Left, workingArea.Right - debugControlPopup.Width);
        var maxY = Math.Max(workingArea.Top, workingArea.Bottom - debugControlPopup.Height);
        x = Math.Clamp(x, workingArea.Left, maxX);
        y = Math.Clamp(y, workingArea.Top, maxY);

        debugControlPopup.Location = new Point(x, y);
    }

    private void ShutdownDebuggerOnFormClosing()
    {
        try
        {
            debuggerSessionCts?.Cancel();
            if (activeDebuggerSession is not null)
            {
                _ = activeDebuggerSession.StopAsync(CancellationToken.None);
                _ = activeDebuggerSession.DisposeAsync();
            }
        }
        catch
        {
            // Ignore close-time debugger cleanup failures.
        }
    }
}

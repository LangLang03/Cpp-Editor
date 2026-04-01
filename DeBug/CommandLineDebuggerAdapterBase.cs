using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace C__Editor;

internal abstract class CommandLineDebuggerAdapterBase : IDebuggerAdapter
{
    private readonly object outputSyncRoot = new();
    private readonly SemaphoreSlim commandGate = new(1, 1);

    private TaskCompletionSource<bool> outputSignal = CreateSignal();
    private readonly StringBuilder outputBuffer = new();

    private Process? process;
    private StreamWriter? stdinWriter;
    private Task? stdoutPumpTask;
    private Task? stderrPumpTask;
    private CancellationTokenSource? processLifetimeCts;
    private bool isDisposed;

    protected DebugLaunchRequest LaunchRequest { get; private set; } = new();

    public event Action<string>? OutputReceived;

    public abstract DebuggerKind Kind { get; }

    protected abstract Regex PromptRegex { get; }

    protected virtual Encoding OutputEncoding => new UTF8Encoding(false);

    protected abstract ProcessStartInfo CreateStartInfo(string debuggerExecutablePath, DebugLaunchRequest request);

    protected abstract IReadOnlyList<string> BuildInitializationCommands(DebugLaunchRequest request);

    protected abstract string GetStartCommand();

    protected abstract string GetContinueCommand();

    protected abstract string GetStepIntoCommand();

    protected abstract string GetStepOverCommand();

    protected abstract string GetStepOutCommand();

    protected abstract string GetQuitCommand();

    protected abstract Task<DebugPauseSnapshot> BuildPauseSnapshotAsync(string commandOutput, CancellationToken cancellationToken);

    protected abstract bool TryParseExitCode(string output, out int exitCode);

    public async Task InitializeAsync(
        DebugLaunchRequest request,
        string debuggerExecutablePath,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        LaunchRequest = request ?? throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(debuggerExecutablePath))
        {
            throw new ArgumentException("Debugger executable path is required.", nameof(debuggerExecutablePath));
        }

        var startInfo = CreateStartInfo(debuggerExecutablePath, request);
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;
        startInfo.StandardOutputEncoding = OutputEncoding;
        startInfo.StandardErrorEncoding = OutputEncoding;

        if (string.IsNullOrWhiteSpace(startInfo.WorkingDirectory))
        {
            startInfo.WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? Environment.CurrentDirectory
                : request.WorkingDirectory;
        }

        process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start debugger process: {debuggerExecutablePath}");
        }

        stdinWriter = process.StandardInput;
        process.Exited += (_, _) =>
        {
            TaskCompletionSource<bool> signalToSet;
            lock (outputSyncRoot)
            {
                signalToSet = outputSignal;
                outputSignal = CreateSignal();
            }

            signalToSet.TrySetResult(true);
        };
        processLifetimeCts = new CancellationTokenSource();
        stdoutPumpTask = PumpStreamAsync(process.StandardOutput, processLifetimeCts.Token);
        stderrPumpTask = PumpStreamAsync(process.StandardError, processLifetimeCts.Token);

        _ = await WaitForPromptFromOffsetAsync(0, cancellationToken).ConfigureAwait(false);

        foreach (var command in BuildInitializationCommands(request))
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            _ = await SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<DebugCommandResult> StartExecutionAsync(CancellationToken cancellationToken)
    {
        return ExecuteControlCommandAsync(GetStartCommand(), cancellationToken);
    }

    public Task<DebugCommandResult> ContinueAsync(CancellationToken cancellationToken)
    {
        return ExecuteControlCommandAsync(GetContinueCommand(), cancellationToken);
    }

    public Task<DebugCommandResult> StepIntoAsync(CancellationToken cancellationToken)
    {
        return ExecuteControlCommandAsync(GetStepIntoCommand(), cancellationToken);
    }

    public Task<DebugCommandResult> StepOverAsync(CancellationToken cancellationToken)
    {
        return ExecuteControlCommandAsync(GetStepOverCommand(), cancellationToken);
    }

    public Task<DebugCommandResult> StepOutAsync(CancellationToken cancellationToken)
    {
        return ExecuteControlCommandAsync(GetStepOutCommand(), cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (isDisposed)
        {
            return;
        }

        var targetProcess = process;
        if (targetProcess is null)
        {
            return;
        }

        try
        {
            if (!targetProcess.HasExited)
            {
                var quitCommand = GetQuitCommand();
                if (!string.IsNullOrWhiteSpace(quitCommand))
                {
                    try
                    {
                        _ = await SendCommandAsync(quitCommand, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore graceful-quit failures and fall back to terminate.
                    }
                }
            }
        }
        catch
        {
            // Ignore process state failures.
        }

        await EnsureProcessTerminatedAsync(targetProcess, cancellationToken).ConfigureAwait(false);
    }

    protected async Task<string> QueryAsync(string command, CancellationToken cancellationToken)
    {
        return await SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
    }

    protected static string NormalizePathForDebugger(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(filePath);
        }
        catch
        {
            return filePath;
        }
    }

    private async Task<DebugCommandResult> ExecuteControlCommandAsync(string command, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var output = await SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
        if (TryParseExitCode(output, out var exitCode))
        {
            return new DebugCommandResult
            {
                State = DebugExecutionState.Exited,
                ExitCode = exitCode,
                RawOutput = output
            };
        }

        if (IsProcessExited())
        {
            return new DebugCommandResult
            {
                State = DebugExecutionState.Exited,
                ExitCode = TryGetProcessExitCode(),
                RawOutput = output
            };
        }

        var pause = await BuildPauseSnapshotAsync(output, cancellationToken).ConfigureAwait(false);
        return new DebugCommandResult
        {
            State = DebugExecutionState.Paused,
            Pause = pause,
            RawOutput = output
        };
    }

    private async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (process is null || stdinWriter is null)
        {
            throw new InvalidOperationException("Debugger process has not been initialized.");
        }

        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var startOffset = GetCurrentOutputLength();
            try
            {
                await stdinWriter.WriteLineAsync(command).ConfigureAwait(false);
                await stdinWriter.FlushAsync().ConfigureAwait(false);
            }
            catch (IOException) when (IsProcessExited())
            {
                var exitedRaw = await WaitForPromptFromOffsetAsync(startOffset, cancellationToken).ConfigureAwait(false);
                return StripTrailingPrompt(exitedRaw);
            }
            catch (ObjectDisposedException) when (IsProcessExited())
            {
                var exitedRaw = await WaitForPromptFromOffsetAsync(startOffset, cancellationToken).ConfigureAwait(false);
                return StripTrailingPrompt(exitedRaw);
            }

            var raw = await WaitForPromptFromOffsetAsync(startOffset, cancellationToken).ConfigureAwait(false);
            return StripTrailingPrompt(raw);
        }
        finally
        {
            commandGate.Release();
        }
    }

    private async Task<string> WaitForPromptFromOffsetAsync(int startOffset, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            string slice;
            var hasPrompt = false;
            var hasExited = false;

            lock (outputSyncRoot)
            {
                var output = outputBuffer.ToString();
                var safeOffset = Math.Clamp(startOffset, 0, output.Length);
                slice = output[safeOffset..];
                hasPrompt = PromptRegex.IsMatch(slice);
                hasExited = IsProcessExitedUnsafe();
                if (hasPrompt || hasExited)
                {
                    return slice;
                }

                waitTask = outputSignal.Task;
            }

            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private string StripTrailingPrompt(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return string.Empty;
        }

        var normalized = rawOutput.TrimEnd('\r', '\n');
        var match = PromptRegex.Match(normalized);
        if (match.Success && match.Index + match.Length == normalized.Length)
        {
            normalized = normalized[..match.Index];
        }

        return normalized.TrimEnd('\r', '\n', ' ', '\t');
    }

    private int GetCurrentOutputLength()
    {
        lock (outputSyncRoot)
        {
            return outputBuffer.Length;
        }
    }

    private async Task PumpStreamAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        while (!cancellationToken.IsCancellationRequested)
        {
            int readCount;
            try
            {
                readCount = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                break;
            }

            if (readCount <= 0)
            {
                break;
            }

            AppendOutput(new string(buffer, 0, readCount));
        }
    }

    private void AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        TaskCompletionSource<bool> signalToSet;
        lock (outputSyncRoot)
        {
            outputBuffer.Append(text);
            signalToSet = outputSignal;
            outputSignal = CreateSignal();
        }

        signalToSet.TrySetResult(true);
        OutputReceived?.Invoke(text);
    }

    private static TaskCompletionSource<bool> CreateSignal()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private bool IsProcessExited()
    {
        lock (outputSyncRoot)
        {
            return IsProcessExitedUnsafe();
        }
    }

    private bool IsProcessExitedUnsafe()
    {
        try
        {
            return process is null || process.HasExited;
        }
        catch
        {
            return true;
        }
    }

    private int? TryGetProcessExitCode()
    {
        try
        {
            return process?.HasExited == true ? process.ExitCode : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task EnsureProcessTerminatedAsync(Process targetProcess, CancellationToken cancellationToken)
    {
        try
        {
            if (!targetProcess.HasExited)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    await targetProcess.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore wait timeout, we'll hard-kill below.
                }
            }
        }
        catch
        {
            // Ignore state errors.
        }

        try
        {
            if (!targetProcess.HasExited)
            {
                targetProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore kill failures.
        }

        processLifetimeCts?.Cancel();
        try
        {
            if (stdoutPumpTask is not null)
            {
                await stdoutPumpTask.ConfigureAwait(false);
            }
        }
        catch
        {
            // Ignore pump failures.
        }

        try
        {
            if (stderrPumpTask is not null)
            {
                await stderrPumpTask.ConfigureAwait(false);
            }
        }
        catch
        {
            // Ignore pump failures.
        }
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Ignore dispose-time stop failures.
        }
        finally
        {
            isDisposed = true;
            processLifetimeCts?.Cancel();
            processLifetimeCts?.Dispose();
            processLifetimeCts = null;

            stdinWriter?.Dispose();
            stdinWriter = null;

            process?.Dispose();
            process = null;

            commandGate.Dispose();
        }
    }
}

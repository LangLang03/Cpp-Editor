namespace C__Editor;

internal sealed class DebuggerSession : IAsyncDisposable
{
    private readonly IDebuggerAdapter adapter;
    private readonly SemaphoreSlim commandGate = new(1, 1);

    private bool isDisposed;

    private DebuggerSession(IDebuggerAdapter adapter)
    {
        this.adapter = adapter;
        this.adapter.OutputReceived += text => OutputReceived?.Invoke(text);
    }

    internal event Action<string>? OutputReceived;

    internal DebuggerKind Kind => adapter.Kind;

    internal bool IsActive { get; private set; }

    internal bool IsPaused { get; private set; }

    internal DebugPauseSnapshot? LastPause { get; private set; }

    internal static async Task<DebuggerSession> CreateAsync(
        DebuggerResolution resolution,
        DebugLaunchRequest request,
        CancellationToken cancellationToken)
    {
        var adapter = DebuggerAdapterFactory.Create(resolution.Kind);
        var session = new DebuggerSession(adapter);
        await session.adapter.InitializeAsync(request, resolution.ExecutablePath, cancellationToken).ConfigureAwait(false);
        session.IsActive = true;
        return session;
    }

    internal Task<DebugCommandResult> StartExecutionAsync(CancellationToken cancellationToken)
    {
        return ExecuteCommandAsync(core => core.StartExecutionAsync(cancellationToken), cancellationToken);
    }

    internal Task<DebugCommandResult> ContinueAsync(CancellationToken cancellationToken)
    {
        return ExecuteCommandAsync(core => core.ContinueAsync(cancellationToken), cancellationToken);
    }

    internal Task<DebugCommandResult> StepIntoAsync(CancellationToken cancellationToken)
    {
        return ExecuteCommandAsync(core => core.StepIntoAsync(cancellationToken), cancellationToken);
    }

    internal Task<DebugCommandResult> StepOverAsync(CancellationToken cancellationToken)
    {
        return ExecuteCommandAsync(core => core.StepOverAsync(cancellationToken), cancellationToken);
    }

    internal Task<DebugCommandResult> StepOutAsync(CancellationToken cancellationToken)
    {
        return ExecuteCommandAsync(core => core.StepOutAsync(cancellationToken), cancellationToken);
    }

    internal async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsActive || isDisposed)
        {
            return;
        }

        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsActive || isDisposed)
            {
                return;
            }

            await adapter.StopAsync(cancellationToken).ConfigureAwait(false);
            IsActive = false;
            IsPaused = false;
            LastPause = null;
        }
        finally
        {
            commandGate.Release();
        }
    }

    private async Task<DebugCommandResult> ExecuteCommandAsync(
        Func<IDebuggerAdapter, Task<DebugCommandResult>> executor,
        CancellationToken cancellationToken)
    {
        if (!IsActive || isDisposed)
        {
            return new DebugCommandResult
            {
                State = DebugExecutionState.Exited,
                ExitCode = 0,
                RawOutput = "Debugger session is not active."
            };
        }

        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsActive || isDisposed)
            {
                return new DebugCommandResult
                {
                    State = DebugExecutionState.Exited,
                    ExitCode = 0,
                    RawOutput = "Debugger session is not active."
                };
            }

            var result = await executor(adapter).ConfigureAwait(false);
            switch (result.State)
            {
                case DebugExecutionState.Paused:
                    IsPaused = true;
                    LastPause = result.Pause;
                    break;
                case DebugExecutionState.Exited:
                    IsPaused = false;
                    LastPause = null;
                    IsActive = false;
                    break;
                default:
                    IsPaused = false;
                    break;
            }

            return result;
        }
        finally
        {
            commandGate.Release();
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
            await adapter.DisposeAsync().ConfigureAwait(false);
            commandGate.Dispose();
        }
    }
}

namespace C__Editor;

internal interface IDebuggerAdapter : IAsyncDisposable
{
    event Action<string>? OutputReceived;

    DebuggerKind Kind { get; }

    Task InitializeAsync(DebugLaunchRequest request, string debuggerExecutablePath, CancellationToken cancellationToken);

    Task<DebugCommandResult> StartExecutionAsync(CancellationToken cancellationToken);

    Task<DebugCommandResult> ContinueAsync(CancellationToken cancellationToken);

    Task<DebugCommandResult> StepIntoAsync(CancellationToken cancellationToken);

    Task<DebugCommandResult> StepOverAsync(CancellationToken cancellationToken);

    Task<DebugCommandResult> StepOutAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

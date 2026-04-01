using System.Diagnostics;
using System.Text;

namespace C__Editor;

internal static class DebuggerExecutableValidator
{
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(10);

    internal static bool TryValidate(DebuggerKind kind, string executablePath, out string detail)
    {
        detail = string.Empty;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            detail = "调试器路径为空。";
            return false;
        }

        if (!File.Exists(executablePath))
        {
            detail = $"调试器文件不存在: {executablePath}";
            return false;
        }

        var arguments = kind switch
        {
            DebuggerKind.Cdb => "-version",
            DebuggerKind.Gdb => "--version",
            DebuggerKind.Lldb => "--version",
            _ => "--version"
        };

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = new UTF8Encoding(false),
                    StandardErrorEncoding = new UTF8Encoding(false)
                }
            };

            if (!process.Start())
            {
                detail = $"无法启动调试器: {executablePath}";
                return false;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var waitCts = new CancellationTokenSource(ValidationTimeout);
            try
            {
                process.WaitForExitAsync(waitCts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                TryTerminate(process);
                detail = $"调试器启动超时: {executablePath}";
                return false;
            }

            var stdout = ReadSafe(stdoutTask);
            var stderr = ReadSafe(stderrTask);
            if (process.ExitCode == 0)
            {
                return true;
            }

            if (kind == DebuggerKind.Cdb &&
                (stdout.Contains("cdb version", StringComparison.OrdinalIgnoreCase) ||
                 stderr.Contains("cdb version", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var message = PickMeaningfulError(stdout, stderr);
            if (kind == DebuggerKind.Lldb &&
                message.Contains("python", StringComparison.OrdinalIgnoreCase) &&
                message.Contains(".dll", StringComparison.OrdinalIgnoreCase))
            {
                message = $"{message}（当前 LLDB 依赖的 Python 运行库缺失或版本不匹配）";
            }

            detail = string.IsNullOrWhiteSpace(message)
                ? $"调试器启动失败，退出码 {process.ExitCode}: {executablePath}"
                : $"调试器启动失败，退出码 {process.ExitCode}: {message}";
            return false;
        }
        catch (Exception ex)
        {
            detail = $"调试器校验失败: {ex.Message}";
            return false;
        }
    }

    private static string ReadSafe(Task<string> task)
    {
        try
        {
            return task.GetAwaiter().GetResult();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string PickMeaningfulError(string stdout, string stderr)
    {
        var merged = string.Join(
            Environment.NewLine,
            new[] { stderr, stdout }.Where(text => !string.IsNullOrWhiteSpace(text)));

        if (string.IsNullOrWhiteSpace(merged))
        {
            return string.Empty;
        }

        foreach (var line in merged.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("PLEASE submit", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return trimmed;
        }

        return merged.Trim();
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore termination failures.
        }
    }
}

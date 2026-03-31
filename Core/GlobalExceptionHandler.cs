using System.Threading;

namespace C__Editor;

internal static class GlobalExceptionHandler
{
    private static int isHandlingException;
    private static bool isRegistered;

    internal static void Register()
    {
        if (isRegistered)
        {
            return;
        }

        isRegistered = true;

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            HandleFatalException(e.Exception, "UI 线程未处理异常", isTerminating: true);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var exception = e.ExceptionObject as Exception
                ?? new Exception("发生未知未处理异常。");

            HandleFatalException(exception, "后台线程未处理异常", e.IsTerminating);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            HandleFatalException(e.Exception, "Task 未观察异常", isTerminating: true);
        };
    }

    internal static void HandleStartupException(Exception exception)
    {
        HandleFatalException(exception, "应用启动异常", isTerminating: true);
    }

    private static void HandleFatalException(Exception exception, string source, bool isTerminating)
    {
        if (Interlocked.Exchange(ref isHandlingException, 1) == 1)
        {
            Environment.Exit(1);
            return;
        }

        try
        {
            ShowDialogOnStaThread(exception, source, isTerminating);
        }
        catch
        {
            try
            {
                MessageBox.Show(
                    exception.ToString(),
                    "C++Editor - 未处理异常",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // Ignore fallback failures and terminate.
            }
        }
        finally
        {
            Environment.Exit(1);
        }
    }

    private static void ShowDialogOnStaThread(Exception exception, string source, bool isTerminating)
    {
        using var completed = new ManualResetEventSlim(false);

        var dialogThread = new Thread(() =>
        {
            try
            {
                using var dialog = new UnhandledExceptionDialog(exception, source, isTerminating);
                dialog.ShowDialog();
            }
            finally
            {
                completed.Set();
            }
        });

        dialogThread.IsBackground = true;
        dialogThread.SetApartmentState(ApartmentState.STA);
        dialogThread.Start();

        completed.Wait();
    }
}

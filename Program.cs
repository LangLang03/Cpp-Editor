namespace C__Editor;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        try
        {
            GlobalExceptionHandler.Register();
            ApplicationConfiguration.Initialize();
            Application.Run(new MainEditorForm());
        }
        catch (Exception ex)
        {
            GlobalExceptionHandler.HandleStartupException(ex);
        }
    }
}

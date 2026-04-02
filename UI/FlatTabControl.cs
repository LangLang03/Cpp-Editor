using System.Runtime.InteropServices;

namespace C__Editor;

internal sealed class FlatTabControl : TabControl
{
    private const int WsExClientEdge = 0x0200;

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(nint hWnd, string? pszSubAppName, string? pszSubIdList);

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle &= ~WsExClientEdge;
            return createParams;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Disable system visual-style rendering to prevent embossed/sunken tab effects.
        _ = SetWindowTheme(Handle, string.Empty, string.Empty);
    }
}

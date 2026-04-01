namespace C__Editor;

public partial class MainEditorForm
{
    private const float GutterToggleRightPadding = 18f;

    private readonly EditorBreakpointIconProvider breakpointIconProvider = new();
    private readonly Dictionary<string, SortedSet<int>> breakpointLinesByFile = new(StringComparer.OrdinalIgnoreCase);
    private string debugExecutionFilePath = string.Empty;
    private int debugExecutionLine = -1;

    private void InitializeBreakpointMarkerSupport()
    {
        if (editorControlMain is null)
        {
            return;
        }

        editorControlMain.SetEditorIconProvider(breakpointIconProvider);
        editorControlMain.Settings.SetMaxGutterIcons(2);
        editorControlMain.Settings.SetContentStartPadding(2f);
        ApplyBreakpointMarkersForCurrentDocument();
    }

    private void EditorControlMain_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || editorControlMain is null || isLoadingEditorDocument)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(currentEditorFilePath))
        {
            return;
        }

        if (!IsBreakpointGutterClick(e.Location))
        {
            return;
        }

        if (!TryGetVisibleLogicalLineByY(e.Y, out var logicalLine))
        {
            return;
        }

        ToggleBreakpointMarkerAtLine(logicalLine + 1);
    }

    private bool IsBreakpointGutterClick(Point location)
    {
        if (editorControlMain is null)
        {
            return false;
        }

        if (location.X < 0 || location.Y < 0 ||
            location.X >= editorControlMain.Width || location.Y >= editorControlMain.Height)
        {
            return false;
        }

        var metrics = editorControlMain.GetScrollMetrics();
        var gutterRight = metrics.TextAreaX - GutterToggleRightPadding;
        if (float.IsNaN(gutterRight) || float.IsInfinity(gutterRight) || gutterRight <= 0f)
        {
            return false;
        }

        return location.X < gutterRight;
    }

    private bool TryGetVisibleLogicalLineByY(int y, out int logicalLine)
    {
        logicalLine = -1;
        if (editorControlMain is null)
        {
            return false;
        }

        var visibleRange = editorControlMain.GetVisibleLineRange();
        if (visibleRange.end < visibleRange.start)
        {
            return false;
        }

        var firstRect = editorControlMain.GetPositionRect(visibleRange.start, 0);
        var lastRect = editorControlMain.GetPositionRect(visibleRange.end, 0);
        var minY = firstRect.Y;
        var maxY = lastRect.Y + Math.Max(1f, lastRect.Height);
        if (y < minY || y > maxY)
        {
            return false;
        }

        var nearestLine = -1;
        var nearestDistance = float.MaxValue;
        for (var line = visibleRange.start; line <= visibleRange.end; line++)
        {
            var rect = editorControlMain.GetPositionRect(line, 0);
            var lineHeight = Math.Max(1f, rect.Height);
            if (y >= rect.Y && y < rect.Y + lineHeight)
            {
                logicalLine = line;
                return true;
            }

            var centerY = rect.Y + lineHeight * 0.5f;
            var distance = Math.Abs(y - centerY);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestLine = line;
            }
        }

        if (nearestLine < 0)
        {
            return false;
        }

        logicalLine = nearestLine;
        return true;
    }

    private void ToggleBreakpointMarkerAtLine(int oneBasedLine)
    {
        if (oneBasedLine <= 0 || string.IsNullOrWhiteSpace(currentEditorFilePath))
        {
            return;
        }

        var filePath = NormalizeBreakpointFilePath(currentEditorFilePath);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var lineSet = GetOrLoadBreakpointLines(filePath);
        if (!lineSet.Add(oneBasedLine))
        {
            lineSet.Remove(oneBasedLine);
        }

        SaveBreakpointLines(filePath, lineSet);
        ApplyBreakpointMarkersForCurrentDocument();
    }

    private void ApplyBreakpointMarkersForCurrentDocument()
    {
        if (editorControlMain is null)
        {
            return;
        }

        editorControlMain.ClearGutterIcons();

        var filePath = ResolveCurrentBreakpointFilePath();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            editorControlMain.Flush();
            return;
        }

        var lines = GetOrLoadBreakpointLines(filePath);
        if (lines.Count <= 0)
        {
            editorControlMain.Flush();
            return;
        }

        var iconsByLine = new Dictionary<int, IList<SweetEditor.GutterIcon>>();
        foreach (var oneBasedLine in lines)
        {
            var zeroBasedLine = oneBasedLine - 1;
            if (zeroBasedLine < 0)
            {
                continue;
            }

            iconsByLine[zeroBasedLine] = new List<SweetEditor.GutterIcon>
            {
                new(EditorGutterIconIds.BreakpointMarker)
            };
        }

        var normalizedExecutionFile = NormalizeBreakpointFilePath(debugExecutionFilePath);
        if (!string.IsNullOrWhiteSpace(normalizedExecutionFile) &&
            string.Equals(normalizedExecutionFile, filePath, StringComparison.OrdinalIgnoreCase) &&
            debugExecutionLine > 0)
        {
            var executionZeroBased = debugExecutionLine - 1;
            if (executionZeroBased >= 0)
            {
                if (!iconsByLine.TryGetValue(executionZeroBased, out var icons))
                {
                    icons = new List<SweetEditor.GutterIcon>();
                    iconsByLine[executionZeroBased] = icons;
                }

                icons.Add(new SweetEditor.GutterIcon(EditorGutterIconIds.DebugExecutionPointer));
            }
        }

        if (iconsByLine.Count > 0)
        {
            editorControlMain.SetBatchLineGutterIcons(iconsByLine);
        }

        editorControlMain.Flush();
    }

    private SortedSet<int> GetOrLoadBreakpointLines(string filePath)
    {
        if (breakpointLinesByFile.TryGetValue(filePath, out var cached))
        {
            return cached;
        }

        var workspaceRoot = ResolveWorkspaceRootForFile(filePath);
        var loaded = WorkspaceBreakpointMarkerController.LoadLines(workspaceRoot, filePath);
        var lineSet = new SortedSet<int>(loaded.Where(line => line > 0));
        breakpointLinesByFile[filePath] = lineSet;
        return lineSet;
    }

    private void SaveBreakpointLines(string filePath, IReadOnlyCollection<int> lines)
    {
        var workspaceRoot = ResolveWorkspaceRootForFile(filePath);
        WorkspaceBreakpointMarkerController.SaveLines(workspaceRoot, filePath, lines);
    }

    private string ResolveWorkspaceRootForFile(string filePath)
    {
        try
        {
            var normalizedFilePath = Path.GetFullPath(filePath);
            var sourceDirectory = Path.GetDirectoryName(normalizedFilePath);
            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                return Environment.CurrentDirectory;
            }

            return ResolveWorkspaceRootForSource(normalizedFilePath, sourceDirectory);
        }
        catch
        {
            return Environment.CurrentDirectory;
        }
    }

    private static string NormalizeBreakpointFilePath(string? filePath)
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
            return string.Empty;
        }
    }

    private string ResolveCurrentBreakpointFilePath()
    {
        var normalized = NormalizeBreakpointFilePath(currentEditorFilePath);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        var selectedState = GetSelectedDocumentState();
        return NormalizeBreakpointFilePath(selectedState?.FilePath);
    }

    private void SetDebugExecutionLineMarker(string? filePath, int oneBasedLine)
    {
        debugExecutionFilePath = NormalizeBreakpointFilePath(filePath);
        debugExecutionLine = oneBasedLine > 0 ? oneBasedLine : -1;
        ApplyBreakpointMarkersForCurrentDocument();
    }
}

namespace C__Editor;

public partial class MainEditorForm
{
    private void ApplyEditorLanguageConfiguration(string? filePath)
    {
        if (editorControlMain is null)
        {
            return;
        }

        var configuration = EditorAutoPairController.BuildLanguageConfiguration(filePath);
        editorControlMain.SetLanguageConfiguration(configuration);
    }
}

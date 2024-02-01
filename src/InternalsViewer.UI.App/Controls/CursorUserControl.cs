using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls;

/// <summary>
/// User control with a public method for Change Cursor
/// </summary>
public class CursorUserControl: UserControl
{
    protected void ChangeCursor(InputCursor cursor)
    {
        ProtectedCursor = cursor;
    }
}

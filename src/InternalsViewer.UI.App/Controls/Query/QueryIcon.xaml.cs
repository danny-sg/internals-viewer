using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Allocation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace InternalsViewer.UI.App.Controls.Query;

/// <summary>
/// Query tab icon, a play triangle sliced by the trace lines
/// </summary>
/// <remarks>
/// Slices take the app logo's right hand column, one colour per row, so the icon carries the logo palette
/// </remarks>
public sealed partial class QueryIcon : UserControl
{
    public QueryIcon()
    {
        InitializeComponent();

        Path[] slices = [TopSlice, MiddleSlice, BottomSlice];

        var lastColumn = DatabaseIconBuilder.ColumnCount - 1;

        for (var row = 0; row < slices.Length; row++)
        {
            var colour = DatabaseIconBuilder.DefaultCells[(row * DatabaseIconBuilder.ColumnCount) + lastColumn];

            slices[row].Fill = new SolidColorBrush(colour.ToWindowsColor());
        }
    }
}

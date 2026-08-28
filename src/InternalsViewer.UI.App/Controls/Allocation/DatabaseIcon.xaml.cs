using System.Collections.Generic;
using System.Drawing;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Colors = Microsoft.UI.Colors;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace InternalsViewer.UI.App.Controls.Allocation;

/// <summary>
/// Quantized version of the app logo, coloured by a database's allocation layers
/// </summary>
public sealed partial class DatabaseIcon : UserControl
{
    public static readonly DependencyProperty CellsProperty =
        DependencyProperty.Register(nameof(Cells),
                                    typeof(IReadOnlyList<Color>),
                                    typeof(DatabaseIcon),
                                    new PropertyMetadata(null, OnCellsChanged));

    public IReadOnlyList<Color>? Cells
    {
        get => (IReadOnlyList<Color>?)GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    public DatabaseIcon()
    {
        InitializeComponent();

        Rectangles = [Cell0, Cell1, Cell2, Cell3, Cell4, Cell5, Cell6, Cell7, Cell8];

        ApplyCells();
    }

    private Rectangle[] Rectangles { get; }

    private void ApplyCells()
    {
        var cells = Cells;

        for (var index = 0; index < Rectangles.Length; index++)
        {
            var colour = cells is not null && index < cells.Count
                ? cells[index].ToWindowsColor()
                : Colors.Transparent;

            Rectangles[index].Fill = new SolidColorBrush(colour);
        }
    }

    private static void OnCellsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DatabaseIcon)d).ApplyCells();
    }
}

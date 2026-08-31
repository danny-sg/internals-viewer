using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Columnstore;

public sealed partial class ColumnstoreTooltip : UserControl
{
    public ColumnstoreTooltip()
    {
        InitializeComponent();
    }

    public void Show(ColumnstoreRegion? region, Point position)
    {
        if (region is null || region.Details.Count == 0)
        {
            Hide();

            return;
        }

        TooltipTitle.Text = region.Label;

        TooltipDetails.ItemsSource = region.Details;

        TooltipPopup.HorizontalOffset = position.X + 12;

        TooltipPopup.VerticalOffset = position.Y + 12;

        TooltipPopup.IsOpen = true;
    }

    public void Hide() => TooltipPopup.IsOpen = false;
}

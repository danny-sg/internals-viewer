using InternalsViewer.Internals.Annotations;
using InternalsViewer.UI.App.Services.Markers;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.Controls.Columnstore;

/// <summary>
/// The colour a marker of one item type is drawn in, shown where the hex itself is not on screen
/// </summary>
public sealed partial class MarkerKey
{
    public MarkerKey()
    {
        InitializeComponent();
    }

    public ItemType ItemType
    {
        get => (ItemType)GetValue(ItemTypeProperty);
        set => SetValue(ItemTypeProperty, value);
    }

    public static readonly DependencyProperty ItemTypeProperty
        = DependencyProperty.Register(nameof(ItemType),
                                      typeof(ItemType),
                                      typeof(MarkerKey),
                                      new PropertyMetadata(ItemType.PageAddress, OnItemTypeChanged));

    private static void OnItemTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MarkerKey)d;

        control.Swatch.Background = new MarkStyleProvider().GetMarkStyle(control.ItemType).BackColour;
    }
}

using InternalsViewer.Internals.Annotations;
using InternalsViewer.UI.App.Services.Markers;

namespace InternalsViewer.UI.App.Controls.Columnstore;

/// <summary>
/// The colour a marker of one item type is drawn in, shown where the hex itself is not on screen
/// </summary>
public sealed partial class MarkerKey
{
    public static readonly DependencyProperty ItemTypeProperty
        = DependencyProperty.Register(nameof(ItemType),
                                      typeof(ItemType),
                                      typeof(MarkerKey),
                                      new PropertyMetadata(ItemType.PageAddress, OnItemTypeChanged));

    public ItemType ItemType
    {
        get => (ItemType)GetValue(ItemTypeProperty);
        set => SetValue(ItemTypeProperty, value);
    }

    public MarkerKey()
    {
        InitializeComponent();
    }

    private static void OnItemTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MarkerKey)d;

        // A column whose marker depends on what is selected has nothing to show when the selection carries none
        control.Visibility = control.ItemType == ItemType.None ? Visibility.Collapsed : Visibility.Visible;

        if (control.ItemType == ItemType.None)
        {
            return;
        }

        var style = MarkStyleProvider.Default.GetMarkStyle(control.ItemType);

        control.Swatch.Background = style.BackColour;

        // The colour a marker writes its bytes in, which is half of what tells one marker from another
        control.Swatch.BorderBrush = style.ForeColour;
    }
}

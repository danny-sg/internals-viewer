using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Input;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class BreakpointToggle : UserControl
{
    public static readonly DependencyProperty IsSetProperty =
        DependencyProperty.Register(nameof(IsSet),
                                    typeof(bool),
                                    typeof(BreakpointToggle),
                                    new PropertyMetadata(false, OnIsSetChanged));

    public bool IsSet
    {
        get => (bool)GetValue(IsSetProperty);
        set => SetValue(IsSetProperty, value);
    }

    private static SolidColorBrush RestBrush { get; } = new(Windows.UI.Color.FromArgb(40, 128, 128, 128));

    private static SolidColorBrush HoverBrush { get; } = new(Windows.UI.Color.FromArgb(190, 32, 32, 32));

    private static SolidColorBrush SetBrush { get; } = new(Windows.UI.Color.FromArgb(255, 229, 20, 0));

    public BreakpointToggle()
    {
        InitializeComponent();

        ToolTipService.SetToolTip(this, "Set Breakpoint");

        Apply();
    }

    private bool IsPointerOver { get; set; }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        IsPointerOver = true;

        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

        Apply();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        IsPointerOver = false;

        Apply();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        IsSet = !IsSet;

        e.Handled = true;
    }

    private void Apply()
        => Glyph.Fill = IsSet ? SetBrush : IsPointerOver ? HoverBrush : RestBrush;

    private static void OnIsSetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((BreakpointToggle)sender).Apply();
}

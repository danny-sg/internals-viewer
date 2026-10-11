using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Columns;

public sealed class ColumnHeader : ContentControl
{
    public static readonly DependencyProperty CanResizeProperty =
        DependencyProperty.Register(nameof(CanResize),
                                    typeof(bool),
                                    typeof(ColumnHeader),
                                    new PropertyMetadata(false));

    public bool CanResize
    {
        get => (bool)GetValue(CanResizeProperty);
        set => SetValue(CanResizeProperty, value);
    }

    public static readonly DependencyProperty DesiredWidthProperty =
        DependencyProperty.Register(nameof(DesiredWidth),
                                    typeof(GridLength),
                                    typeof(ColumnHeader),
                                    new PropertyMetadata(new GridLength(1, GridUnitType.Star), OnDesiredWidthChanged));

    public GridLength DesiredWidth
    {
        get => (GridLength)GetValue(DesiredWidthProperty);
        set => SetValue(DesiredWidthProperty, value);
    }

    public ColumnHeader()
    {
        DefaultStyleKey = typeof(ColumnHeader);

        RegisterPropertyChangedCallback(WidthProperty, OnWidthChanged);
    }

    internal GridLength CurrentWidth => double.IsNaN(Width) ? DesiredWidth : new GridLength(Width);

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("ColumnSizer") is ContentSizer sizer)
        {
            sizer.TargetControl = this;
        }
    }

    private void InvalidateHeaderPanel() => (Parent as ColumnHeaderPanel)?.InvalidateMeasure();

    private static void OnWidthChanged(DependencyObject sender, DependencyProperty property)
        => ((ColumnHeader)sender).InvalidateHeaderPanel();

    private static void OnDesiredWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ColumnHeader)d).InvalidateHeaderPanel();
}

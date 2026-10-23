using System;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace InternalsViewer.UI.App.Controls.HexView;

public sealed partial class HexTipControl : UserControl
{
    public static readonly DependencyProperty TipProperty
        = DependencyProperty.Register(nameof(Tip),
            typeof(HexTipViewModel),
            typeof(HexTipControl),
            new PropertyMetadata(null));

    public HexTipViewModel? Tip
    {
        get => (HexTipViewModel?)GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }

    public HexTipControl()
    {
        InitializeComponent();
    }

    public event EventHandler<HexTipViewModel>? PinClicked;

    public event EventHandler<HexTipValue>? ValuePinClicked;

    public event EventHandler<HexTipViewModel>? LabelEditing;

    private void LabelTextBox_GettingFocus(UIElement sender, GettingFocusEventArgs e)
    {
        if (Tip is { } tip)
        {
            LabelEditing?.Invoke(this, tip);
        }
    }

    private void LabelTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (Tip is not { } tip)
        {
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;

            SetLabel(tip);
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;

            LabelTextBox.Text = tip.Label;
        }
    }

    private void LabelTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (Tip is { } tip)
        {
            SetLabel(tip);
        }
    }

    private void SetLabel(HexTipViewModel tip)
    {
        tip.SetLabel(LabelTextBox.Text);

        LabelTextBox.Text = tip.Label;
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (Tip is { } tip)
        {
            PinClicked?.Invoke(this, tip);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (Tip is { } tip)
        {
            ClipboardText.Copy(tip.Value);
        }
    }

    private void ValueCopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HexTipValue value })
        {
            ClipboardText.Copy(value.Value);
        }
    }

    private void ValuePinButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HexTipValue value })
        {
            ValuePinClicked?.Invoke(this, value);
        }
    }

    private Visibility GetVisibility(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsValuesVisible(bool isValueTip) => GetVisibility(!isValueTip);

    private Thickness GetPadding(bool isValueTip) => isValueTip ? new Thickness(2, 0, 0, 0) : new Thickness(4);

    private double GetTextSize(bool isValueTip) => isValueTip ? 12 : 14;
}

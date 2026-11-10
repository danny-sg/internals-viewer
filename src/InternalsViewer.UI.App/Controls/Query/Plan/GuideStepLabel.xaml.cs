using System;
using CommunityToolkit.WinUI.Controls;
using InternalsViewer.UI.App.Models.Query.Plan;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Controls.Query.Plan;

public sealed partial class GuideStepLabel : UserControl
{
    public GuideStepLabel()
    {
        InitializeComponent();
    }

    public event EventHandler<bool>? Clicked;

    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    public void Show(GuideStepItem step)
    {
        NameText.Config = step.Config;

        NameText.Text = step.Name;

        DescriptionText.Config = step.Config;

        DescriptionText.Text = step.Description;

        DescriptionText.Visibility = step.HasDescription ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e) => Clicked?.Invoke(this, true);

    private void OnNextClick(object sender, RoutedEventArgs e) => Clicked?.Invoke(this, false);

    private void OnDescriptionLinkClicked(object? sender, LinkClickedEventArgs e) => LinkClicked?.Invoke(sender, e);

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e) => e.Handled = true;
}

using System;
using System.Numerics;
using CommunityToolkit.WinUI.Controls;
using InternalsViewer.UI.App.Models.Query.Plan;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Controls.Query.Plan;

public sealed partial class GuideStepLabel : UserControl
{
    private const float ShadowBlurRadius = 10;

    private const float ShadowOpacity = 0.14f;

    private const float ShadowOffset = 4;

    public GuideStepLabel()
    {
        InitializeComponent();

        ElementCompositionPreview.SetElementChildVisual(ShadowTarget, CreateShadow());
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

    private SpriteVisual CreateShadow()
    {
        var compositor = ElementCompositionPreview.GetElementVisual(ShadowTarget).Compositor;

        var shadow = compositor.CreateDropShadow();

        shadow.BlurRadius = ShadowBlurRadius;

        shadow.Opacity = ShadowOpacity;

        shadow.Offset = new Vector3(ShadowOffset, ShadowOffset, 0);

        var visual = compositor.CreateSpriteVisual();

        visual.RelativeSizeAdjustment = Vector2.One;

        visual.Shadow = shadow;

        return visual;
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e) => Clicked?.Invoke(this, true);

    private void OnNextClick(object sender, RoutedEventArgs e) => Clicked?.Invoke(this, false);

    private void OnDescriptionLinkClicked(object? sender, LinkClickedEventArgs e) => LinkClicked?.Invoke(sender, e);

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e) => e.Handled = true;
}

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace InternalsViewer.UI.App.Controls.Query.Plan;

internal sealed class PlanHighlight
{
    private static readonly TimeSpan MinimumFade = TimeSpan.FromMilliseconds(50);

    private static readonly TimeSpan MaximumFade = TimeSpan.FromMilliseconds(250);

    public PlanHighlight(params UIElement[] elements)
    {
        Elements = elements;

        SetOpacity(0);

        foreach (var element in elements)
        {
            Fader.Children.Add(CreateFade(element));
        }
    }

    private UIElement[] Elements { get; }

    private Storyboard Fader { get; } = new();

    private bool IsLit { get; set; }

    private long? LitAt { get; set; }

    private TimeSpan FadeDuration { get; set; } = MaximumFade;

    public void Light()
    {
        Fader.Stop();

        SetOpacity(1);

        if (!IsLit)
        {
            var now = Environment.TickCount64;

            FadeDuration = LitAt is { } previous
                ? TimeSpan.FromMilliseconds(Math.Min(now - previous, MaximumFade.TotalMilliseconds))
                : MaximumFade;

            LitAt = now;
        }

        IsLit = true;
    }

    public void Fade()
    {
        if (!IsLit)
        {
            return;
        }

        IsLit = false;

        Fader.Stop();

        SetOpacity(0);

        if (FadeDuration < MinimumFade)
        {
            return;
        }

        foreach (var animation in Fader.Children)
        {
            animation.Duration = new Duration(FadeDuration);
        }

        Fader.Begin();
    }

    public void Stop() => Fader.Stop();

    private static DoubleAnimation CreateFade(UIElement target)
    {
        var animation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        Storyboard.SetTarget(animation, target);

        Storyboard.SetTargetProperty(animation, nameof(UIElement.Opacity));

        return animation;
    }

    private void SetOpacity(double opacity)
    {
        foreach (var element in Elements)
        {
            element.Opacity = opacity;
        }
    }
}

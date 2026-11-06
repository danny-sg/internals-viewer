using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace InternalsViewer.UI.App.Controls.Query.Plan;

internal sealed class PlanHighlight
{
    private static readonly TimeSpan MinimumFade = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan MaximumFade = TimeSpan.FromMilliseconds(600);

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

    public static TimeSpan GetFade(long elapsedMilliseconds)
        => TimeSpan.FromMilliseconds(Math.Min(elapsedMilliseconds, MaximumFade.TotalMilliseconds));

    public void Light()
    {
        Fader.Stop();

        SetOpacity(1);

        IsLit = true;
    }

    public void Fade(TimeSpan duration)
    {
        if (!IsLit)
        {
            return;
        }

        IsLit = false;

        Fader.Stop();

        SetOpacity(0);

        if (duration < MinimumFade)
        {
            return;
        }

        foreach (var animation in Fader.Children)
        {
            animation.Duration = new Duration(duration);
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

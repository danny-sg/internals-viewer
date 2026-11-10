using System;

namespace InternalsViewer.UI.App.ViewModels.Query;

public interface ITimelineTransport
{
    const double StepHoldMs = 700;

    const double MinimumPlaySpeed = 0.01;

    event Action<bool>? PlayStateChanged;

    bool IsPlaying { get; }

    double PlaySpeed { get; set; }

    void TogglePlay();

    void Step(bool forward);
}

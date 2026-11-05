using System;

namespace InternalsViewer.UI.App.ViewModels.Query;

public interface ITimelineTransport
{
    event Action<bool>? PlayStateChanged;

    bool IsPlaying { get; }

    double PlaySpeed { get; set; }

    void TogglePlay();

    void Step(bool forward);
}

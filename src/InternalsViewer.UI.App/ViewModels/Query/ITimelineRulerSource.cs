using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Query.Timeline;
using InternalsViewer.UI.App.Models.Query.Timeline.Enums;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.ViewModels.Query;

public interface ITimelineRulerSource : ITimelineTransport
{
    event Action? RulerChanged;

    TimelineRulerState? GetRulerState();

    double? GetRulerOffset(UIElement relativeTo);

    string FormatRulerTick(double offset, double step);

    string FormatRulerPlayhead(double value);

    List<TimelineRulerMarks> GetRulerMarks();

    bool ClickRulerMark(TimelineRulerMarkType type, double from, double to, double minimumLength);

    void MoveRulerPlayhead(double value);

    void MoveRulerCrop(TimelineRulerHandle handle, double value);

    void EndRulerCrop();

    void ResetRulerCrop();
}

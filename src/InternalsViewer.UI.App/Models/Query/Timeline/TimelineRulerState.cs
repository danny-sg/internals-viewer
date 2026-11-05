namespace InternalsViewer.UI.App.Models.Query.Timeline;

public readonly record struct TimelineRulerState(double ViewStart,
                                                 double ViewEnd,
                                                 double FullStart,
                                                 double FullEnd,
                                                 double Origin,
                                                 double ContentLeft,
                                                 double ContentRight,
                                                 double? Playhead,
                                                 double? CropStart,
                                                 double? CropEnd);

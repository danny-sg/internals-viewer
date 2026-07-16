using InternalsViewer.Query.Events.Operators;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// The laid-out geometry and colour of one operator's bar for a paint pass — its time span (StartX/EndX), its
/// row-sized slot, the bar rectangle within that slot, and the colour it draws in
/// </summary>
/// <remarks>
/// Built once per frame by the control's operator layout, then consumed by both the trace rails (which drop from a
/// bar) and the operator renderer (which draws the bar). Purely positional data — holds no native resources.
/// </remarks>
internal readonly record struct OperatorBar(ExecutionOperatorEvent Op,
                                            float StartX,
                                            float EndX,
                                            float BarTop,
                                            float BarBottom,
                                            float BarCentreY,
                                            float LineWidth,
                                            float CornerRadius,
                                            float SlotCentreY,
                                            float SlotHeight,
                                            SKColor BarColour);

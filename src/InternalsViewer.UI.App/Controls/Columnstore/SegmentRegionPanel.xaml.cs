using System.Collections.ObjectModel;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Controls.Columnstore;

/// <summary>
/// Markers over the detail for one region of a segment, split so either can be given the room
/// </summary>
public sealed partial class SegmentRegionPanel
{
    public SegmentRegionPanel()
    {
        InitializeComponent();
    }

    public ObservableCollection<Marker>? Markers
    {
        get => (ObservableCollection<Marker>?)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    public static readonly DependencyProperty MarkersProperty
        = DependencyProperty.Register(nameof(Markers),
                                      typeof(ObservableCollection<Marker>),
                                      typeof(SegmentRegionPanel),
                                      new PropertyMetadata(null));

    public Marker? SelectedMarker
    {
        get => (Marker?)GetValue(SelectedMarkerProperty);
        set => SetValue(SelectedMarkerProperty, value);
    }

    public static readonly DependencyProperty SelectedMarkerProperty
        = DependencyProperty.Register(nameof(SelectedMarker),
                                      typeof(Marker),
                                      typeof(SegmentRegionPanel),
                                      new PropertyMetadata(null));

    /// <summary>
    /// Dims the markers while they are behind the window, rather than leaving them looking current
    /// </summary>
    public double MarkerOpacity
    {
        get => (double)GetValue(MarkerOpacityProperty);
        set => SetValue(MarkerOpacityProperty, value);
    }

    public static readonly DependencyProperty MarkerOpacityProperty
        = DependencyProperty.Register(nameof(MarkerOpacity),
                                      typeof(double),
                                      typeof(SegmentRegionPanel),
                                      new PropertyMetadata(1.0));

    /// <summary>
    /// Content shown beneath the markers, which the regions fill in as their detail views are built
    /// </summary>
    public object? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public static readonly DependencyProperty DetailProperty
        = DependencyProperty.Register(nameof(Detail),
                                      typeof(object),
                                      typeof(SegmentRegionPanel),
                                      new PropertyMetadata(null, OnDetailChanged));

    private static void OnDetailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (SegmentRegionPanel)d;

        panel.HasDetail = e.NewValue is null ? Visibility.Collapsed : Visibility.Visible;

        panel.DetailHeight = e.NewValue is null ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
    }

    public Visibility HasDetail
    {
        get => (Visibility)GetValue(HasDetailProperty);
        set => SetValue(HasDetailProperty, value);
    }

    public static readonly DependencyProperty HasDetailProperty
        = DependencyProperty.Register(nameof(HasDetail),
                                      typeof(Visibility),
                                      typeof(SegmentRegionPanel),
                                      new PropertyMetadata(Visibility.Collapsed));

    public GridLength DetailHeight
    {
        get => (GridLength)GetValue(DetailHeightProperty);
        set => SetValue(DetailHeightProperty, value);
    }

    public static readonly DependencyProperty DetailHeightProperty
        = DependencyProperty.Register(nameof(DetailHeight),
                                      typeof(GridLength),
                                      typeof(SegmentRegionPanel),
                                      new PropertyMetadata(new GridLength(0)));
}

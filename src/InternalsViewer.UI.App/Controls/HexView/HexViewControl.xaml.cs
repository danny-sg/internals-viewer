using System;
using System.Collections.ObjectModel;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Controls.HexView;

public sealed partial class HexViewControl
{
    private const int BytesPerLine = HexLayout.BytesPerLine;

    // Matches the RichTextBlock LineHeight the hex is laid out with
    private const double LineHeight = 16;

    private const int WheelDeltaPerNotch = 120;

    private const int LinesPerNotch = 3;

    public static readonly DependencyProperty MarkersProperty = DependencyProperty
        .Register(nameof(Markers),
            typeof(ObservableCollection<Marker>),
            typeof(HexViewControl),
            new PropertyMetadata(null, OnMarkersChanged));

    public ObservableCollection<Marker>? Markers
    {
        get { return (ObservableCollection<Marker>)GetValue(MarkersProperty); }
        set { SetValue(MarkersProperty, value); }
    }

    public static readonly DependencyProperty SelectedMarkerProperty
        = DependencyProperty.Register(nameof(SelectedMarker),
            typeof(Marker),
            typeof(HexViewControl),
            new PropertyMetadata(null, OnSelectedMarkerChanged));

    public Marker? SelectedMarker
    {
        get => (Marker?)GetValue(SelectedMarkerProperty);
        set => SetValue(SelectedMarkerProperty, value);
    }

    public HexViewControl()
    {
        InitializeComponent();

        SetAddress();

        MouseOver = new MouseOverInfo(null, null);

        // Border positions depend on the rendered text layout, so a resize/re-layout invalidates them
        HexRichTextBlock.SizeChanged += (_, _) =>
        {
            DrawChangeSpans();

            DrawSelectionMask();
        };

        ScrollViewer.SizeChanged += (_, _) => UpdateVirtualization();

        ScrollViewer.ViewChanged += OnScrollViewerViewChanged;

        // The virtualized window holds only the lines on screen, so the ScrollViewer has nothing of its own to scroll
        ScrollViewer.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChanged), true);

        // A drag that ends without its closing scroll would leave the window held, so the pointer ends it too
        VirtualScrollBar.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(OnScrollBarCaptureLost), true);

        // The text block marks these handled to select text, so the click that clears the mask is watched for anyway
        HexRichTextBlock.AddHandler(PointerPressedEvent, new PointerEventHandler(OnHexPointerPressed), true);

        HexRichTextBlock.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnHexPointerReleased), true);
    }

    public HexControlViewModel ViewModel { get; } = new();

    private static void OnSelectedMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HexViewControl)d;

        if (e.NewValue is Marker marker)
        {
            ScrollToPosition(control, marker.StartPosition, isFollowingSelection: true);
        }

        if (MarkerLookup.GetRange(e.OldValue as Marker) == MarkerLookup.GetRange(e.NewValue as Marker))
        {
            return;
        }

        control.DrawSelectionMask();
    }

    private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HexViewControl)d;

        // A pending rebuild highlights from the new markers itself, so highlighting them twice is only wasted work
        if (control._isHexRebuildPending)
        {
            return;
        }

        HighlightMarkers(control, (ObservableCollection<Marker>)e.NewValue);
    }
}

using InternalsViewer.UI.App.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System;
using InternalsViewer.UI.App.ViewModels.Query.Simulation;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Simulation;

public sealed partial class SimulationVisualPanelView : UserControl
{
    private const float IndexZoomToPageZoom = 1f;

    private const double AllocationZoomToPageZoom = 3d;

    public SimulationVisualPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public SimulationVisualViewModel? ViewModel => DataContext as SimulationVisualViewModel;

    public Windows.UI.Color ToWindowsColor(System.Drawing.Color colour)
        => Windows.UI.Color.FromArgb(colour.A, colour.R, colour.G, colour.B);

    /// <summary>
    /// The zoom the index map animates to while Zoom to Page is on, or null to leave the zoom alone
    /// </summary>
    public float? ZoomToPageTarget(bool isZoomToPage) => isZoomToPage ? IndexZoomToPageZoom : null;

    /// <summary>
    /// The zoom the allocation map follows the current span at while Zoom to Page is on, or null to leave the zoom alone
    /// </summary>
    public double? AllocationZoomToPageTarget(bool isZoomToPage) => isZoomToPage ? AllocationZoomToPageZoom : null;

    public Visibility IndexVisibility(SimulationVisualType visualType, bool isInitialized)
        => visualType == SimulationVisualType.Index && isInitialized ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadingVisibility(SimulationVisualType visualType, bool isInitialized)
        => visualType is SimulationVisualType.Index or SimulationVisualType.Columnstore && !isInitialized
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility AllocationVisibility(SimulationVisualType visualType, bool isInitialized)
        => visualType == SimulationVisualType.Allocation && isInitialized ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ColumnstoreVisibility(SimulationVisualType visualType, bool isInitialized)
        => visualType == SimulationVisualType.Columnstore && isInitialized ? Visibility.Visible : Visibility.Collapsed;

#pragma warning disable VSTHRD100
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel is { } viewModel)
            {
                await viewModel.LoadVisualAsync();
            }
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100
}

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Services;

namespace InternalsViewer.UI.App.Controls.Allocation;

public sealed partial class AllocationControl
{
    private const string ZoomSettingKey = "AllocationMapZoom";

    public static readonly DependencyProperty IsZoomSharedProperty
        = DependencyProperty.Register(nameof(IsZoomShared),
                                      typeof(bool),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(true, OnIsZoomSharedChanged));

    public bool IsZoomShared
    {
        get => (bool)GetValue(IsZoomSharedProperty);
        set => SetValue(IsZoomSharedProperty, value);
    }

    private static double? SharedZoom { get; set; }

    private static Task<double?>? SavedZoom { get; set; }

    private void RegisterSharedZoom()
        => WeakReferenceMessenger.Default.Register<AllocationControl, AllocationZoomChangedMessage>(
            this,
            static (r, m) => r.ApplySharedZoom(m.Value));

    private void UnregisterSharedZoom() => WeakReferenceMessenger.Default.Unregister<AllocationZoomChangedMessage>(this);

    private async Task ApplySavedZoomAsync()
    {
        SavedZoom ??= App.GetService<SettingsService>().ReadSettingAsync<double?>(ZoomSettingKey);

        var saved = await SavedZoom;

        SharedZoom ??= saved;

        ApplySharedZoom(SharedZoom);
    }

    private void ShareZoom()
    {
        if (!IsZoomShared || ZoomToCurrentPage is > 0)
        {
            return;
        }

        SharedZoom = Zoom;

        _ = App.GetService<SettingsService>().SaveSettingAsync(ZoomSettingKey, Zoom);

        WeakReferenceMessenger.Default.Send(new AllocationZoomChangedMessage(Zoom));
    }

    private void ApplySharedZoom(double? zoom)
    {
        if (zoom is { } value && IsZoomShared && ZoomToCurrentPage is not > 0)
        {
            Zoom = Math.Clamp(value, MinimumZoom, MaximumZoom);
        }
    }

    private static void OnIsZoomSharedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            ((AllocationControl)d).ApplySharedZoom(SharedZoom);
        }
    }
}

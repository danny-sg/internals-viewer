using CommunityToolkit.Mvvm.Messaging.Messages;

namespace InternalsViewer.UI.App.Messages;

public sealed class AllocationZoomChangedMessage(double zoom) : ValueChangedMessage<double>(zoom);

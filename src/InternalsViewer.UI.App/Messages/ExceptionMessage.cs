using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace InternalsViewer.UI.App.Messages;

public sealed class ExceptionMessage(Exception exception) : AsyncRequestMessage<bool>
{
    public Exception Exception { get; } = exception;
}
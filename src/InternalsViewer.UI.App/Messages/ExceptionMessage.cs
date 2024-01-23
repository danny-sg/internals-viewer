using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace InternalsViewer.UI.App.Messages;

public class ExceptionMessage(Exception exception) : ValueChangedMessage<Exception>(exception);
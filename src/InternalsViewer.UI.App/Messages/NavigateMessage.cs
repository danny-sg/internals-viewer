using CommunityToolkit.Mvvm.Messaging.Messages;

namespace InternalsViewer.UI.App.Messages;

public sealed class NavigateMessage(string target) : ValueChangedMessage<string>(target);
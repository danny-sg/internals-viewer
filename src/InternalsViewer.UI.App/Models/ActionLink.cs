using System;

namespace InternalsViewer.UI.App.Models;

public sealed record ActionLink(string Text, Action Invoke);

using System;

namespace InternalsViewer.UI.App.ViewModels.Docking;

public sealed record DocumentMenuItem(string Text, Action Invoke);

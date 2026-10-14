using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace InternalsViewer.UI.App.Helpers;

public static class KeyboardState
{
    public static bool IsShiftDown()
        => InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
}

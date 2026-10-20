using System;
using System.IO;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class TraceTimelineControl
{
    private const string KeyboardLogPath = @"C:\ProgramData\InternalsViewer\keyboard-debug.log";

    private bool _isWatchingKeyboard;

    private static void LogKeyboard(string message)
    {
        try
        {
            File.AppendAllText(KeyboardLogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void WatchKeyboard()
    {
        if (_isWatchingKeyboard || XamlRoot?.Content is not UIElement root)
        {
            return;
        }

        _isWatchingKeyboard = true;

        _keyboardHost.GotFocus += (_, _) => LogKeyboard("host got focus");

        _keyboardHost.LosingFocus += (_, e) => LogKeyboard($"host losing focus to {e.NewFocusedElement?.GetType().Name} ({e.FocusState})");

        root.AddHandler(KeyDownEvent,
                        new KeyEventHandler((_, e) => LogKeyboard($"root key {e.Key} source={e.OriginalSource?.GetType().Name} "
                                                                  + $"handled={e.Handled} "
                                                                  + $"focused={FocusManager.GetFocusedElement(XamlRoot)?.GetType().Name}")),
                        true);
    }
}

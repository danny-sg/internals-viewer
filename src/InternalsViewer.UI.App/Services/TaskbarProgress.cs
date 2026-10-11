using System;
using System.Runtime.InteropServices;

namespace InternalsViewer.UI.App.Services;

public static class TaskbarProgress
{
    private const ulong Scale = 1000;

    private static object? Owner { get; set; }

    private static ITaskbarList3? Taskbar { get; set; }

    public static void Show(object owner, double percentage)
    {
        Owner = owner;

        Apply(TaskbarProgressState.Normal, percentage);
    }

    public static void ShowIndeterminate(object owner)
    {
        Owner = owner;

        Apply(TaskbarProgressState.Indeterminate, null);
    }

    public static void Clear(object owner)
    {
        if (!ReferenceEquals(Owner, owner))
        {
            return;
        }

        Owner = null;

        Apply(TaskbarProgressState.None, null);
    }

    private static void Apply(TaskbarProgressState state, double? percentage)
    {
        if (App.MainWindow is not { } window)
        {
            return;
        }

        try
        {
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);

            var taskbar = Taskbar ??= Create();

            taskbar.SetProgressState(handle, state);

            if (percentage is { } value)
            {
                taskbar.SetProgressValue(handle, (ulong)(Math.Clamp(value, 0, 100) * Scale / 100), Scale);
            }
        }
        catch (COMException)
        {
        }
    }

    private static ITaskbarList3 Create()
    {
        var taskbar = (ITaskbarList3)new TaskbarList();

        taskbar.HrInit();

        return taskbar;
    }

    private enum TaskbarProgressState
    {
        None = 0,
        Indeterminate = 0x1,
        Normal = 0x2
    }

    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();

        void AddTab(IntPtr hwnd);

        void DeleteTab(IntPtr hwnd);

        void ActivateTab(IntPtr hwnd);

        void SetActiveAlt(IntPtr hwnd);

        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);

        void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);

        void SetProgressState(IntPtr hwnd, TaskbarProgressState state);
    }

    [ComImport]
    [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarList
    {
    }
}

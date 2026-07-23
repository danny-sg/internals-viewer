using System;
using Microsoft.UI.Dispatching;

namespace InternalsViewer.UI.App.ViewModels.Tabs;

public sealed class UiDispatcher(DispatcherQueue? queue)
{
    public static UiDispatcher ForCurrentThread()
    {
        try
        {
            return new UiDispatcher(DispatcherQueue.GetForCurrentThread());
        }
        catch (Exception)
        {
            return new UiDispatcher(null);
        }
    }

    public bool TryEnqueue(DispatcherQueueHandler handler)
    {
        if (queue is not null)
        {
            return queue.TryEnqueue(handler);
        }

        handler();

        return true;
    }
}

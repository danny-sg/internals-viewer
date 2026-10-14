namespace InternalsViewer.Query.Events.Scheduling;

// ReSharper disable InconsistentNaming
public enum WorkerCallbackType
{
    CALLBACK_SUSPEND = 0,
    CALLBACK_RESUME = 1,
    CALLBACK_DONE = 2,
    CALLBACK_INSTANT_RESUME = 3,
    CALLBACK_LAST = 4
}

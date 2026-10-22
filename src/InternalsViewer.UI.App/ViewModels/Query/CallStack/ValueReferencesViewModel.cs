using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed partial class ValueReferencesViewModel(ulong value,
                                                     CallStoreLookup lookup,
                                                     CallTreeTabViewModel owner,
                                                     ILogger logger) : ObservableObject, IDisposable
{
    public const int CallPage = 500;

    [ObservableProperty]
    private string _heading = ReferencesTo(value);

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private ValueReferences? _references;

    public ulong Value { get; } = value;

    public string Title => $"Memory Address 0x{Value:X}";

    public CallTreeTabViewModel Owner { get; } = owner;

    public CallStoreLookup Lookup { get; } = lookup;

    private CancellationTokenSource Cancellation { get; set; } = new();

    public async Task LoadAsync()
    {
        await Cancellation.CancelAsync();

        Cancellation.Dispose();

        var cancellation = new CancellationTokenSource();

        Cancellation = cancellation;

        Heading = ReferencesTo(Value);
        References = null;
        IsSearching = true;

        try
        {
            var references = await Lookup.GetReferencesAsync(Value, cancellation.Token);

            References = references;

            Heading = references.Groups.Count == 0 ? $"No {ReferencesTo(Value)}" : ReferencesTo(Value);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            logger.LogWarning(exception, "Searching the call log for {Value} failed", Value);

            Heading = exception.Message;
        }
        finally
        {
            if (ReferenceEquals(Cancellation, cancellation))
            {
                IsSearching = false;
            }
        }
    }

    public Task<IReadOnlyList<TraceValueCall>> FindCallsAsync(ValueUseRow row)
        => ReadAsync(token => Lookup.FindCallsAsync(row, token),
                     exception => logger.LogWarning(exception,
                                                    "Listing the calls of {Function} with {Value} failed",
                                                    row.Function,
                                                    row.Value));

    public Task<IReadOnlyList<ValueCallRow>> CallRowsAsync(TraceValueUse use, IReadOnlyList<TraceValueCall> valueCalls, int from)
        => ReadAsync(token => Lookup.CallRowsAsync(use, valueCalls, from, CallPage, token),
                     exception => logger.LogWarning(exception, "Reading the calls of 0x{Function:X} failed", use.FunctionAddress));

    public void Dispose()
    {
        Cancellation.Cancel();

        Cancellation.Dispose();
    }

    private static string ReferencesTo(ulong value) => $"References To 0x{value:X}";

    private async Task<IReadOnlyList<T>> ReadAsync<T>(Func<CancellationToken, Task<IReadOnlyList<T>>> read, Action<Exception> warn)
    {
        try
        {
            return await read(Cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            warn(exception);

            return [];
        }
    }
}

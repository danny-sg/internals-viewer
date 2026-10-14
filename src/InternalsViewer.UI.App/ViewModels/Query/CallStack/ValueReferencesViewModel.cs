using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed partial class ValueReferencesViewModel(ulong value,
                                                     CallLogLookup lookup,
                                                     CallTreeTabViewModel owner,
                                                     ILogger logger) : ObservableObject, IDisposable
{
    public const int CallPage = 500;

    [ObservableProperty]
    private string _heading = $"References To 0x{value:X}";

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private ValueReferences? _references;

    public ulong Value { get; } = value;

    public string Title => $"0x{Value:X}";

    public CallTreeTabViewModel Owner { get; } = owner;

    public CallLogLookup Lookup { get; } = lookup;

    private CancellationTokenSource Cancellation { get; set; } = new();

    public async Task LoadAsync()
    {
        await Cancellation.CancelAsync();

        Cancellation.Dispose();

        var cancellation = new CancellationTokenSource();

        Cancellation = cancellation;

        Heading = $"References To 0x{Value:X}";
        References = null;
        IsSearching = true;

        try
        {
            var references = await Lookup.GetReferencesAsync(Value, Owner.Exclusions, cancellation.Token);

            References = references;
            Heading = references.Groups.Count == 0 ? $"No References To 0x{Value:X}" : $"References To 0x{Value:X}";
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

    public async Task<IReadOnlyList<TraceValueMatch>> FindCallsAsync(ValueUseRow row)
    {
        try
        {
            return await Lookup.FindCallsAsync(row, Owner.Exclusions, Cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            logger.LogWarning(exception, "Listing the calls of {Function} with {Value} failed", row.Function, row.Value);

            return [];
        }
    }

    public async Task<IReadOnlyList<ValueCallRow>> CallRowsAsync(TraceValueUse use, IReadOnlyList<TraceValueMatch> matches, int from)
    {
        try
        {
            return await Lookup.CallRowsAsync(use, matches, from, CallPage, Cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            logger.LogWarning(exception, "Reading the calls of 0x{Function:X} failed", use.Address);

            return [];
        }
    }

    public void Dispose()
    {
        Cancellation.Cancel();

        Cancellation.Dispose();
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.UI.App.Models;

/// <summary>
/// Log record list item pairing a page log record with the annotations produced when the record is applied
/// </summary>
/// <remarks>
/// Record and Annotations are internal so the XAML type info generator does not walk into the Query record types, which it cannot activate
/// due to their required/init members - the tree binds the public display properties only
/// </remarks>
public partial class LogRecordItem : ObservableObject
{
    internal PageLogRecord Record { get; set; } = null!;

    internal ObservableCollection<LogRecordAnnotation> Annotations { get; set; } = [];

    public string Lsn => Record.Lsn.ToString();

    public string Operation => Record.Operation.ToString();

    public string Context => Record.Context.ToString();

    public int SlotId => Record.SlotId;

    public string Description => Record.Description;
}

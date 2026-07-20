using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.TransactionLog.LogRecords;

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

    /// <summary>
    /// Whether the record is applied by the current replay - checked for the target record and every record
    /// before it
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Whether the record's checkbox is interactive - records before the replay target are checked but disabled,
    /// as they can only be unapplied by moving the target
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    public string Lsn => Record.Lsn.ToString();

    public string Operation => Record.Operation.ToString();

    public string Context => Record.Context.ToString();

    public int SlotId => Record.SlotId;

    public string Description => Record.Description;
}

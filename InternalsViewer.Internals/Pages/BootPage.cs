using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Pages;

/// <summary>
/// Boot Page
/// </summary>
public class BootPage : Page
{
    public static PageAddress BootPageAddress = new(1, 9);

    /// <summary>
    /// Gets or sets the last checkpoint LSN.
    /// </summary>
    public LogSequenceNumber CheckpointLsn { get; set; }
}
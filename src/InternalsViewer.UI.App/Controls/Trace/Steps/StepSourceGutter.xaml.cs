using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Trace.Steps;

/// <summary>
/// Names the operator a run of steps belongs to, in the gutter to the left of the step rows
/// </summary>
/// <remarks>
/// The gutter hangs into the left padding of the step row, so it carries its own offset and width rather than
/// taking them from where it sits.
/// </remarks>
public sealed partial class StepSourceGutter : UserControl
{
    public StepSourceGutter()
    {
        InitializeComponent();
    }

    public string NodeName
    {
        get => NodeNameText.Text;
        set => NodeNameText.Text = value;
    }

    public Brush? BlobBrush
    {
        get => Blob.Background;
        set => Blob.Background = value;
    }

    public bool IsBlobVisible
    {
        get => Blob.Visibility == Visibility.Visible;
        set => Blob.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public int NodeId { get; set; }
}

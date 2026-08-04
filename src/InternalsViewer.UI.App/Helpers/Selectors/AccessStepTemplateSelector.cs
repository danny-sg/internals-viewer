using InternalsViewer.Execution.AccessPaths.Results;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public class AccessStepTemplateSelector : DataTemplateSelector
{
    public DataTemplate ReadPageTemplate { get; set; } = null!;

    public DataTemplate ProbeStartTemplate { get; set; } = null!;

    public DataTemplate ProbeTemplate { get; set; } = null!;

    public DataTemplate ProbeRunTemplate { get; set; } = null!;

    public DataTemplate DescendTemplate { get; set; } = null!;

    public DataTemplate ProbeResultTemplate { get; set; } = null!;

    public DataTemplate RowTemplate { get; set; } = null!;

    public DataTemplate RowRunTemplate { get; set; } = null!;

    public DataTemplate RangeEndTemplate { get; set; } = null!;

    public DataTemplate LeafLinkTemplate { get; set; } = null!;

    public DataTemplate ReseekTemplate { get; set; } = null!;

    public DataTemplate RebindTemplate { get; set; } = null!;

    public DataTemplate JoinStartTemplate { get; set; } = null!;

    public DataTemplate JoinVerdictTemplate { get; set; } = null!;

    public DataTemplate HashBuildTemplate { get; set; } = null!;

    public DataTemplate HashProbeTemplate { get; set; } = null!;

    public DataTemplate HashProbeRunTemplate { get; set; } = null!;

    public DataTemplate HashCompareTemplate { get; set; } = null!;

    public DataTemplate ForwardedRecordTemplate { get; set; } = null!;

    public DataTemplate MergeCompareTemplate { get; set; } = null!;

    public DataTemplate MergeCompareRunTemplate { get; set; } = null!;

    public DataTemplate JoinEmitTemplate { get; set; } = null!;

    public DataTemplate StoppedTemplate { get; set; } = null!;

    public DataTemplate TruncatedTemplate { get; set; } = null!;

    public DataTemplate IamReadTemplate { get; set; } = null!;

    public DataTemplate IamLinkTemplate { get; set; } = null!;

    public DataTemplate PfsReadTemplate { get; set; } = null!;

    public DataTemplate PfsCheckTemplate { get; set; } = null!;

    public DataTemplate AdvanceTemplate { get; set; } = null!;

    public DataTemplate ExtentStartTemplate { get; set; } = null!;

    public DataTemplate PageSkippedTemplate { get; set; } = null!;

    public DataTemplate DefaultTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return item switch
        {
            AccessStep.ReadPage => ReadPageTemplate,
            AccessStep.ProbeStart => ProbeStartTemplate,
            AccessStep.Probe => ProbeTemplate,
            AccessStep.ProbeRun => ProbeRunTemplate,
            AccessStep.Descend => DescendTemplate,
            AccessStep.ProbeResult => ProbeResultTemplate,
            AccessStep.Row => RowTemplate,
            AccessStep.RowRun => RowRunTemplate,
            AccessStep.RangeEnd => RangeEndTemplate,
            AccessStep.LeafLink => LeafLinkTemplate,
            AccessStep.Reseek => ReseekTemplate,
            AccessStep.Rebind => RebindTemplate,
            AccessStep.JoinStart => JoinStartTemplate,
            AccessStep.JoinVerdict => JoinVerdictTemplate,
            AccessStep.HashBuild => HashBuildTemplate,
            AccessStep.HashProbe => HashProbeTemplate,
            AccessStep.HashProbeRun => HashProbeRunTemplate,
            AccessStep.HashCompare => HashCompareTemplate,
            AccessStep.ForwardedRecord => ForwardedRecordTemplate,
            AccessStep.MergeCompare => MergeCompareTemplate,
            AccessStep.MergeCompareRun => MergeCompareRunTemplate,
            AccessStep.JoinEmit => JoinEmitTemplate,
            AccessStep.Stopped => StoppedTemplate,
            AccessStep.Truncated => TruncatedTemplate,
            AccessStep.IamRead => IamReadTemplate,
            AccessStep.IamLink => IamLinkTemplate,
            AccessStep.PfsRead => PfsReadTemplate,
            AccessStep.PfsCheck => PfsCheckTemplate,
            AccessStep.Advance => AdvanceTemplate,
            AccessStep.ExtentStart => ExtentStartTemplate,
            AccessStep.PageSkipped => PageSkippedTemplate,
            _ => DefaultTemplate
        };
    }
}

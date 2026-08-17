using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.UI.App.Models.Query.Trace.Steps;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public class AccessStepTemplateSelector : DataTemplateSelector
{
    public DataTemplate OpenTemplate { get; set; } = null!;

    public DataTemplate CloseTemplate { get; set; } = null!;

    public DataTemplate OutputTemplate { get; set; } = null!;

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

    public DataTemplate HashBuildRunTemplate { get; set; } = null!;

    public DataTemplate HashProbeSpanTemplate { get; set; } = null!;

    public DataTemplate HashMatchSpanTemplate { get; set; } = null!;

    public DataTemplate TopStartTemplate { get; set; } = null!;

    public DataTemplate InputStartTemplate { get; set; } = null!;

    public DataTemplate ConcatRowTemplate { get; set; } = null!;

    public DataTemplate SortCollectSpanTemplate { get; set; } = null!;

    public DataTemplate SortCollectTemplate { get; set; } = null!;

    public DataTemplate SortedTemplate { get; set; } = null!;

    public DataTemplate SortRowTemplate { get; set; } = null!;

    public DataTemplate SortDuplicateTemplate { get; set; } = null!;

    public DataTemplate AggregateStartTemplate { get; set; } = null!;

    public DataTemplate AggregateGroupTemplate { get; set; } = null!;

    public DataTemplate AggregateRowTemplate { get; set; } = null!;

    public DataTemplate StreamAggregateSpanTemplate { get; set; } = null!;

    public DataTemplate AggregateEmitTemplate { get; set; } = null!;

    public DataTemplate HashAggregateTemplate { get; set; } = null!;

    public DataTemplate ComputeRowTemplate { get; set; } = null!;

    public DataTemplate FilterRowTemplate { get; set; } = null!;

    public DataTemplate SegmentSpanTemplate { get; set; } = null!;

    public DataTemplate RankSpanTemplate { get; set; } = null!;

    public DataTemplate TopRowTemplate { get; set; } = null!;

    public DataTemplate RowCountSpanTemplate { get; set; } = null!;

    public DataTemplate HashProbeTemplate { get; set; } = null!;

    public DataTemplate HashProbeRunTemplate { get; set; } = null!;

    public DataTemplate HashCompareTemplate { get; set; } = null!;

    public DataTemplate ForwardedRecordTemplate { get; set; } = null!;

    public DataTemplate MergeCompareTemplate { get; set; } = null!;

    public DataTemplate MergeCompareRunTemplate { get; set; } = null!;

    public DataTemplate MergeCompareSpanTemplate { get; set; } = null!;

    public DataTemplate MergeMatchSpanTemplate { get; set; } = null!;

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
            AccessStep.Open => OpenTemplate,
            AccessStep.Close => CloseTemplate,
            AccessStep.Output => OutputTemplate,
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
            HashBuildSpan => HashBuildRunTemplate,
            HashProbeSpan => HashProbeSpanTemplate,
            HashMatchSpan => HashMatchSpanTemplate,
            AccessStep.TopStart => TopStartTemplate,
            AccessStep.InputStart => InputStartTemplate,
            AccessStep.ConcatRow => ConcatRowTemplate,
            SortCollectSpan => SortCollectSpanTemplate,
            AccessStep.SortCollect => SortCollectTemplate,
            AccessStep.Sorted => SortedTemplate,
            AccessStep.SortRow => SortRowTemplate,
            AccessStep.SortDuplicate => SortDuplicateTemplate,
            AccessStep.AggregateStart => AggregateStartTemplate,
            AccessStep.AggregateGroup => AggregateGroupTemplate,
            StreamAggregateSpan => StreamAggregateSpanTemplate,
            AccessStep.AggregateRow => AggregateRowTemplate,
            AccessStep.AggregateEmit => AggregateEmitTemplate,
            AccessStep.HashAggregate => HashAggregateTemplate,
            AccessStep.ComputeRow => ComputeRowTemplate,
            AccessStep.FilterRow => FilterRowTemplate,
            SegmentSpan => SegmentSpanTemplate,
            RankSpan => RankSpanTemplate,
            AccessStep.TopRow => TopRowTemplate,
            RowCountSpan => RowCountSpanTemplate,
            AccessStep.HashProbe => HashProbeTemplate,
            AccessStep.HashProbeRun => HashProbeRunTemplate,
            AccessStep.HashCompare => HashCompareTemplate,
            AccessStep.ForwardedRecord => ForwardedRecordTemplate,
            AccessStep.MergeCompare => MergeCompareTemplate,
            AccessStep.MergeCompareRun => MergeCompareRunTemplate,
            MergeCompareSpan => MergeCompareSpanTemplate,
            MergeMatchSpan => MergeMatchSpanTemplate,
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

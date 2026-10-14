using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed partial class SourceStep(CallStackNode node,
                                       ulong id,
                                       int depth,
                                       MemoryMapOperation? creation = null,
                                       bool isOnStack = false,
                                       int call = -1) : ObservableObject
{
    private const int IndentWidth = 12;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpanderGlyph))]
    [NotifyPropertyChangedFor(nameof(VisibleStack))]
    private bool _isExpanded;

    public CallStackNode Node { get; } = node;

    public ulong Id { get; } = id;

    public int Depth { get; } = depth;

    public MemoryMapOperation? Creation { get; } = creation;

    public bool IsOnStack { get; } = isOnStack;

    public int Call { get; } = call;

    public bool IsCreation => Creation is not null;

    public string CreationText => Creation == MemoryMapOperation.Allocated ? "Allocated Here" : "Constructed Here";

    public IReadOnlyList<SourceFrame> Stack
        => field ??= [.. Node.Ancestors().Skip(1).Where(n => n.Symbol.Length > 0).Select(n => new SourceFrame(n, Id))];

    public IReadOnlyList<SourceFrame> VisibleStack => IsExpanded ? Stack : [];

    public Thickness Indent => new(Depth * IndentWidth, 0, 0, 0);

    public string ExpanderGlyph => ((char)(IsExpanded ? 0xE70D : 0xE76C)).ToString();

    public bool CanExpand => !IsOnStack && Stack.Count > 0;

    public Visibility ExpanderVisibility => CanExpand ? Visibility.Visible : Visibility.Collapsed;

    public void Toggle()
    {
        if (CanExpand)
        {
            IsExpanded = !IsExpanded;
        }
    }
}

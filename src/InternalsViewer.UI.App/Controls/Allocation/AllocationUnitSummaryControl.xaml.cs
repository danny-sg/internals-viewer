using System.ComponentModel;
using System.Runtime.CompilerServices;
using InternalsViewer.Internals.Engine.Database;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IndexTypeEnum = InternalsViewer.Internals.Engine.Database.Enums.IndexType;

namespace InternalsViewer.UI.App.Controls.Allocation;

public sealed partial class AllocationUnitSummaryControl : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty AllocationUnitProperty =
        DependencyProperty.Register(nameof(AllocationUnit),
                                     typeof(AllocationUnit),
                                     typeof(AllocationUnitSummaryControl),
                                     new PropertyMetadata(null, OnAllocationUnitChanged));

    public event PropertyChangedEventHandler? PropertyChanged;

    public AllocationUnit? AllocationUnit
    {
        get => (AllocationUnit?)GetValue(AllocationUnitProperty);
        set => SetValue(AllocationUnitProperty, value);
    }

    public string ObjectName => AllocationUnit is { } allocationUnit
        ? $"{allocationUnit.SchemaName}.{allocationUnit.TableName}"
        : string.Empty;

    public int ObjectId => AllocationUnit?.ObjectId ?? 0;

    public string IndexName => AllocationUnit?.IndexName ?? string.Empty;

    public int IndexId => AllocationUnit?.IndexId ?? 0;

    public string IndexType => AllocationUnit?.IndexType == IndexTypeEnum.NonClustered
        ? "Non-Clustered"
        : string.Empty;

    public string ObjectIndexType => AllocationUnit?.ParentIndexType == IndexTypeEnum.Clustered
        ? "Clustered"
        : "Heap";

    public AllocationUnitSummaryControl()
    {
        InitializeComponent();
    }

    private static void OnAllocationUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AllocationUnitSummaryControl)d;

        control.OnPropertyChanged(nameof(ObjectName));
        control.OnPropertyChanged(nameof(ObjectId));
        control.OnPropertyChanged(nameof(IndexName));
        control.OnPropertyChanged(nameof(IndexId));
        control.OnPropertyChanged(nameof(IndexType));
        control.OnPropertyChanged(nameof(ObjectIndexType));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

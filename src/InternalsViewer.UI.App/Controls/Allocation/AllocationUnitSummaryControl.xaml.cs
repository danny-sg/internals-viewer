using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using InternalsViewer.Internals.Engine.Database;
using Microsoft.UI.Xaml.Controls;
using IndexTypeEnum = InternalsViewer.Internals.Engine.Database.Enums.IndexType;
using FontWeight = Windows.UI.Text.FontWeight;
using FontWeights = Microsoft.UI.Text.FontWeights;

namespace InternalsViewer.UI.App.Controls.Allocation;

public sealed partial class AllocationUnitSummaryControl : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty AllocationUnitProperty =
        DependencyProperty.Register(nameof(AllocationUnit),
                                     typeof(AllocationUnit),
                                     typeof(AllocationUnitSummaryControl),
                                     new PropertyMetadata(null, OnAllocationUnitChanged));

    public static readonly DependencyProperty IsIndexProperty =
        DependencyProperty.Register(nameof(IsIndex),
                                     typeof(bool),
                                     typeof(AllocationUnitSummaryControl),
                                     new PropertyMetadata(false, OnIsIndexChanged));

    public event PropertyChangedEventHandler? PropertyChanged;

    public AllocationUnit? AllocationUnit
    {
        get => (AllocationUnit?)GetValue(AllocationUnitProperty);
        set => SetValue(AllocationUnitProperty, value);
    }

    public bool IsIndex
    {
        get => (bool)GetValue(IsIndexProperty);
        set => SetValue(IsIndexProperty, value);
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

    public string ObjectIndexType => AllocationUnit?.ParentIndexType switch
    {
        IndexTypeEnum.Clustered => "Clustered",
        IndexTypeEnum.Heap => "Heap",
        IndexTypeEnum.NonClustered => "Non-Clustered",
        IndexTypeEnum.Xml => "XML",
        IndexTypeEnum.ClusteredColumnStore => "Clustered Columnstore",
        IndexTypeEnum.NonClusteredColumnStore => "Non-Clustered Columnstore",
        IndexTypeEnum.NonClusteredHash => "Heap",
        _ => "Unknown"
    };


    public FontWeight ObjectNameFontWeight => IsIndex ? FontWeights.Normal : FontWeights.Bold;

    public FontWeight IndexNameFontWeight => IsIndex ? FontWeights.Bold : FontWeights.Normal;

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

    private static void OnIsIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AllocationUnitSummaryControl)d;

        control.OnPropertyChanged(nameof(ObjectNameFontWeight));
        control.OnPropertyChanged(nameof(IndexNameFontWeight));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

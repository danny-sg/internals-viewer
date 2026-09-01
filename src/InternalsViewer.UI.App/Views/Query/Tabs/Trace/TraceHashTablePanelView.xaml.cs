using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

/// <summary>
/// The hash table of one hash match, shown under the build side that fills it
/// </summary>
public sealed partial class TraceHashTablePanelView : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title),
                                    typeof(string),
                                    typeof(TraceHashTablePanelView),
                                    new PropertyMetadata("Hash Table"));

    public static readonly DependencyProperty SubTitleProperty =
        DependencyProperty.Register(nameof(SubTitle),
                                    typeof(string),
                                    typeof(TraceHashTablePanelView),
                                    new PropertyMetadata(string.Empty));

    public TraceHashTablePanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public TraceHashTableViewModel? ViewModel => DataContext as TraceHashTableViewModel;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string SubTitle
    {
        get => (string)GetValue(SubTitleProperty);
        set => SetValue(SubTitleProperty, value);
    }
}

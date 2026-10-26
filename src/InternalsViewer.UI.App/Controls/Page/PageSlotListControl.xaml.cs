using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Page;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Page;

public sealed partial class PageSlotListControl : UserControl
{
    public static readonly DependencyProperty SlotsProperty =
        DependencyProperty.Register(nameof(Slots),
                                    typeof(IEnumerable<PageSlot>),
                                    typeof(PageSlotListControl),
                                    new PropertyMetadata(null));

    public IEnumerable<PageSlot>? Slots
    {
        get => (IEnumerable<PageSlot>?)GetValue(SlotsProperty);
        set => SetValue(SlotsProperty, value);
    }

    public static readonly DependencyProperty SelectedSlotProperty =
        DependencyProperty.Register(nameof(SelectedSlot),
                                    typeof(PageSlot),
                                    typeof(PageSlotListControl),
                                    new PropertyMetadata(null));

    public PageSlot? SelectedSlot
    {
        get => (PageSlot?)GetValue(SelectedSlotProperty);
        set => SetValue(SelectedSlotProperty, value);
    }

    public PageSlotListControl()
    {
        InitializeComponent();
    }

    private void OnSlotClick(object sender, ItemClickEventArgs e)
    {
        if (SlotListView.SelectedItem == e.ClickedItem)
        {
            SlotListView.SelectedItem = null;

            SelectedSlot = null;
        }
    }
}

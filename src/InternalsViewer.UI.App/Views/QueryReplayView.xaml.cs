using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.QueryReplay;

namespace InternalsViewer.UI.App.Views;

public sealed partial class QueryReplayView : Page
{
    public QueryReplayViewModel ViewModel => (QueryReplayViewModel)DataContext;

    public QueryReplayView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();

        AllocationItemRepeater.SizeChanged += OnParentSizeChanged;

        EventGrid.PageClicked += OnPageSelected;
    }

    private void OnParentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var itemCount = ViewModel.DatabaseFiles.Length;

        if (itemCount > 0)
        {
            ViewModel.AllocationMapHeight = AllocationItemRepeater.ActualHeight / itemCount;
        }
    }

    private void OnPageSelected(object? sender, PageAddressEventArgs e)
    {
        var pageAddress = new PageAddress(e.FileId, e.PageId);

        WeakReferenceMessenger.Default
                              .Send(new OpenPageMessage(new OpenPageRequest(ViewModel.Database, pageAddress)));
    }

    private void OnSqlTextChanged(object? sender, string sql)
    {
        ViewModel.Sql = sql;
    }
}

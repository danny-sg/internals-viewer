﻿using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class TabViewModel : ObservableObject
{
    public MainViewModel Parent { get; }

    public virtual TabType TabType { get; }

    [ObservableProperty]
    private string tabId = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private bool isLoading = true;

    public virtual ImageSource ImageSource => new BitmapImage(new Uri("ms-appx:///Assets/TabIcons/Database16.png"));

    public TabViewModel(MainViewModel parent, TabType tabType)
    {
        Parent = parent;
        TabType = tabType;
        TabId = Guid.NewGuid().ToString();
    }
}
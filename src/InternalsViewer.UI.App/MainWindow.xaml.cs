using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using System;
using InternalsViewer.UI.App.Views;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Connections.Backup;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.Views.Connect;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using InternalsViewer.UI.App.Models.Connections;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Database;
using InternalsViewer.UI.App.ViewModels.Page;
using InternalsViewer.UI.App.ViewModels.Connections;
using InternalsViewer.UI.App.Controls;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Tabs;
using WinUIEx;
using InternalsViewer.UI.App.ViewModels.Index;

namespace InternalsViewer.UI.App;

public sealed partial class MainWindow
{
    private IDatabaseService DatabaseService { get; }

    private TabViewItem? ConnectTab { get; set; }

    private MainViewModel ViewModel { get; }

    private PageTabViewModelFactory PageTabViewModelFactory { get; }

    private DatabaseTabViewModelFactory DatabaseTabViewModelFactory { get; }

    private IndexTabViewModelFactory IndexTabViewModelFactory { get; }

    private ConnectServerViewModelFactory ConnectServerViewModelFactory { get; }

    public MainWindow(IDatabaseService databaseService,
                      MainViewModel mainViewModel,
                      PageTabViewModelFactory pageTabViewModelFactory,
                      DatabaseTabViewModelFactory databaseTabViewModelFactory,
                      ConnectServerViewModelFactory connectServerViewModelFactory,
                      IndexTabViewModelFactory indexTabViewModelFactory)
    {
        Title = "Internals Viewer";

        DatabaseService = databaseService;

        ViewModel = mainViewModel;
        PageTabViewModelFactory = pageTabViewModelFactory;
        DatabaseTabViewModelFactory = databaseTabViewModelFactory;
        ConnectServerViewModelFactory = connectServerViewModelFactory;
        IndexTabViewModelFactory = indexTabViewModelFactory;

        ExtendsContentIntoTitleBar = true;

        InitializeComponent();

        this.SetIcon("Assets/InternalsViewer.ico");

        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };

        WeakReferenceMessenger.Default.Register<ConnectServerMessage>(this, (_, m)
            => m.Reply(ConnectServer(m.ConnectionString, m.Recent, m.IsPasswordRequired)));

        WeakReferenceMessenger.Default.Register<ConnectFileMessage>(this, (_, m)
            => m.Reply(ConnectFile(m.Filename, m.Recent)));

        WeakReferenceMessenger.Default.Register<OpenPageMessage>(this, (_, m)
            => m.Reply(OpenPage(m.Request.Database, m.Request.PageAddress)));

        WeakReferenceMessenger.Default.Register<OpenIndexMessage>(this, (_, m)
            => m.Reply(OpenIndex(m.Request.Database, m.Request.RootPageAddress)));

        WeakReferenceMessenger.Default.Register<ExceptionMessage>(this, (_, m)
                       => ShowExceptionDialog(m.Value));

        SetTitleBar(CustomDragRegion);
    }

    private async void ShowExceptionDialog(Exception exception)
    {
        var dialog = new ExceptionDialog();

        dialog.Message = exception.Message;
        dialog.StackTrace = exception.StackTrace ?? string.Empty;

        dialog.XamlRoot = Content.XamlRoot;
        dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;

        await dialog.ShowAsync();
    }

    private async Task<string> ShowPasswordDialog()
    {
        var dialog = new PasswordDialog();

        dialog.XamlRoot = Content.XamlRoot;
        dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return dialog.Password;
        }

        return string.Empty;
    }

    private async Task<bool> ConnectServer(string connectionString, RecentConnection recent, bool isPasswordRequired)
    {
        // Recent Connections don't store the password so if required it will prompt and update the connection string
        if (isPasswordRequired)
        {
            var result = await ShowPasswordDialog();

            if (string.IsNullOrEmpty(result))
            {
                return false;
            }

            connectionString = ConnectionHelper.SetPassword(connectionString, result);
        }

        var connection = ServerConnectionFactory.Create(c => c.ConnectionString = connectionString);

        await AddConnection(connection);

        await ViewModel.AddRecentConnectionCommand.ExecuteAsync(recent);

        return true;
    }

    private async Task<bool> ConnectFile(string filename, RecentConnection recent)
    {
        var connection = FileConnectionFactory.Create(c => c.Filename = filename);

        await AddConnection(connection);

        await ViewModel.AddRecentConnectionCommand.ExecuteAsync(recent);

        return true;
    }

    private async Task ConnectBackup(string filename)
    {
        var connection = BackupConnectionFactory.Create(c => c.Filename = filename);

        await AddConnection(connection);
    }

    private async Task<bool> OpenPage(DatabaseSource database, PageAddress pageAddress)
    {
        var viewModel = PageTabViewModelFactory.Create(database);

        await viewModel.LoadPage(pageAddress);

        var content = new PageView();

        content.DataContext = viewModel;

        var svg = new SvgImageSource(new Uri("ms-appx:///Assets/TabIcons/PageTabIcon.svg"));

        var title = $"Page {pageAddress.PageId}";

        var tab = new TabViewItem
        {
            Name = title,
            Content = content,
            IconSource = new ImageIconSource { ImageSource = svg },
        };

        BindTabTitle(viewModel, tab);

        WindowTabView.TabItems.Add(tab);
        WindowTabView.SelectedItem = tab;

        return true;
    }

    private async Task<bool> OpenIndex(DatabaseSource database, PageAddress rootPageAddress)
    {
        var viewModel = IndexTabViewModelFactory.Create(database);

        viewModel.RootPage = rootPageAddress;

        var content = new IndexView();

        content.DataContext = viewModel;

        var title = $"Index TODO";

        var svg = new SvgImageSource(new Uri("ms-appx:///Assets/TabIcons/PageTabIcon.svg"));

        var tab = new TabViewItem
        {
            Name = title,
            Content = content,
            IconSource = new ImageIconSource { ImageSource = svg },
        };

        BindTabTitle(viewModel, tab);

        WindowTabView.TabItems.Add(tab);
        WindowTabView.SelectedItem = tab;

        return true;
    }

    private void BindTabTitle(TabViewModel viewModel, TabViewItem tab)
    {
        var titleBinding = new Binding { Mode = BindingMode.OneWay };

        titleBinding.Source = viewModel;

        tab.Style = RootGrid.Resources["MainWindowTabStyle"] as Style;

        tab.SetBinding(TabViewItem.HeaderProperty, titleBinding);
    }

    private async Task AddConnection(IConnectionType connection)
    {
        var database = await DatabaseService.Load(connection.Name, connection);

        var viewModel = DatabaseTabViewModelFactory.Create(database);

        viewModel.Load(connection.Name);

        var content = new DatabaseView();

        content.DataContext = viewModel;

        var svg = new SvgImageSource(new Uri("ms-appx:///Assets/TabIcons/DatabaseTabIcon.svg"));

        var tab = new TabViewItem
        {
            Name = connection.Name,
            IconSource = new ImageIconSource { ImageSource = svg },
            Content = content
        };

        BindTabTitle(viewModel, tab);

        WindowTabView.TabItems.Add(tab);
        WindowTabView.SelectedItem = tab;
    }

    private void TabView_OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        // Close tab if it's not the connect tab
        if (args.Tab != ConnectTab)
        {
            sender.TabItems.Remove(args.Tab);
        }
    }

    private void TabView_Loaded(object sender, RoutedEventArgs e)
    {
        var tabView = sender as TabView;

        if (tabView != null)
        {
            ConnectTab = AddConnectTab(tabView);
            ConnectTab.IsClosable = false;
        }
    }

    private TabViewItem AddConnectTab(TabView tabView)
    {
        var content = new ConnectView(ConnectServerViewModelFactory);

        content.DataContext = ViewModel;

        ViewModel.Name = "Internals Viewer";

        var icon = new ImageIconSource { ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon16.png")) };

        var connectTab = new TabViewItem
        {
            Content = content,
            IconSource = icon,
            IsClosable = false
        };

        BindTabTitle(ViewModel, connectTab);

        tabView.TabItems.Add(connectTab);

        return connectTab;
    }

    public async Task InitializeAsync()
    {
        await ViewModel.InitializeAsync();
    }
}

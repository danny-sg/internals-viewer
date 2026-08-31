using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Connection.BackupFile.Connection;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.UI.App.Controls;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Connections;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Connections;
using InternalsViewer.UI.App.ViewModels.Database;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.ViewModels.Page;
using InternalsViewer.UI.App.ViewModels.Query;
using InternalsViewer.UI.App.ViewModels.Tabs;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using InternalsViewer.UI.App.Views;
using InternalsViewer.UI.App.Views.Columnstore;
using InternalsViewer.UI.App.Views.Connect;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinUIEx;
using QueryView = InternalsViewer.UI.App.Views.Query.QueryView;
using InternalsViewer.Connection.BackupFile.Media;
using InternalsViewer.Internals.Engine.Loading;
using LogRecordItem = InternalsViewer.UI.App.Models.Logging.LogRecordItem;

namespace InternalsViewer.UI.App;

public sealed partial class MainWindow
{
    private ILogger? _logger;

    public MainWindow(IDatabaseService databaseService,
                      IEnumerable<IConnectionTypeFactory> connectionFactories,
                      MainViewModel mainViewModel,
                      AppLogViewModel appLogViewModel,
                      PageTabViewModelFactory pageTabViewModelFactory,
                      DatabaseTabViewModelFactory databaseTabViewModelFactory,
                      ConnectServerViewModelFactory connectServerViewModelFactory,
                      IndexTabViewModelFactory indexTabViewModelFactory,
                      ColumnstoreTabViewModelFactory columnstoreTabViewModelFactory,
                      QueryViewModelFactory queryViewModelFactory)
    {
        Title = "Internals Viewer";

        DatabaseService = databaseService;
        ConnectionFactories = connectionFactories;

        ViewModel = mainViewModel;
        AppLogViewModel = appLogViewModel;
        PageTabViewModelFactory = pageTabViewModelFactory;
        DatabaseTabViewModelFactory = databaseTabViewModelFactory;
        ConnectServerViewModelFactory = connectServerViewModelFactory;
        IndexTabViewModelFactory = indexTabViewModelFactory;
        ColumnstoreTabViewModelFactory = columnstoreTabViewModelFactory;
        QueryViewModelFactory = queryViewModelFactory;

        ExtendsContentIntoTitleBar = true;

        InitializeComponent();

        Closed += (_, _) =>
        {
            WindowCts.Cancel();
            WindowCts.Dispose();
        };

        this.SetIcon("Assets/InternalsViewer.ico");

        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };

        WeakReferenceMessenger.Default.Register<ConnectServerMessage>(this, (_, m)
            => m.Reply(ConnectServer(m.ConnectionString, m.Recent, m.IsPasswordRequired)));

        WeakReferenceMessenger.Default.Register<ConnectFileMessage>(this, (_, m)
            => m.Reply(ConnectFile(m.Filename, m.Recent)));

        WeakReferenceMessenger.Default.Register<ConnectBackupMessage>(this, (_, m)
            => m.Reply(ConnectBackup(m)));

        WeakReferenceMessenger.Default.Register<OpenPageMessage>(this, (_, m)
            => m.Reply(OpenPage(m.Request)));

        WeakReferenceMessenger.Default.Register<OpenIndexMessage>(this, (_, m)
            => m.Reply(OpenIndex(m.Request.Database, m.Request.RootPageAddress)));

        WeakReferenceMessenger.Default.Register<OpenColumnstoreMessage>(this, (_, m)
            => m.Reply(OpenColumnstore(m.Request.Database, m.Request.AllocationUnitId)));

        WeakReferenceMessenger.Default.Register<ExceptionMessage>(this, (_, m)
                       => m.Reply(ShowExceptionDialog(m.Exception)));

        WeakReferenceMessenger.Default.Register<OpenQueryMessage>(this, (_, m)
            => m.Reply(OpenQuery(m.Database)));

        WeakReferenceMessenger.Default.Register<OpenLogMessage>(this, (_, m)
            => m.Reply(OpenLogTab()));

        SetTitleBar(CustomDragRegion);
    }

    private IDatabaseService DatabaseService { get; }

    private IEnumerable<IConnectionTypeFactory> ConnectionFactories { get; }

    private TabViewItem? ConnectTab { get; set; }

    private TabViewItem? LogTab { get; set; }

    private MainViewModel ViewModel { get; }

    private AppLogViewModel AppLogViewModel { get; }

    private PageTabViewModelFactory PageTabViewModelFactory { get; }

    private DatabaseTabViewModelFactory DatabaseTabViewModelFactory { get; }

    private IndexTabViewModelFactory IndexTabViewModelFactory { get; }

    private ColumnstoreTabViewModelFactory ColumnstoreTabViewModelFactory { get; }

    private QueryViewModelFactory QueryViewModelFactory { get;  }

    private ConnectServerViewModelFactory ConnectServerViewModelFactory { get; }

    private CancellationTokenSource WindowCts { get; } = new();

    private ILogger Logger => _logger ??= App.GetService<ILoggerFactory>().CreateLogger<MainWindow>();

    public async Task InitializeAsync()
    {
        await ViewModel.InitializeAsync();
    }

    private async Task<bool> ShowExceptionDialog(Exception exception)
    {
        var dialog = new ExceptionDialog();

        dialog.Message = exception.Message;
        dialog.StackTrace = exception.StackTrace ?? string.Empty;

        dialog.XamlRoot = Content.XamlRoot;
        dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;

        await dialog.ShowAsync();

        return true;
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

        var factory = (IConnectionTypeFactory<ServerConnectionConfig>)ConnectionFactories
            .Single(f => f.Identifier == ServerConnectionFactory.ServerIdentifier);

        var connection = factory.Create(c => c.ConnectionString = connectionString);

        if (!await AddConnection(connection))
        {
            return false;
        }

        await ViewModel.AddRecentConnectionCommand.ExecuteAsync(recent);

        return true;
    }

    private async Task<bool> ConnectFile(string filename, RecentConnection recent)
    {
        var factory = (IConnectionTypeFactory<FileConnectionTypeConfig>)ConnectionFactories
            .Single(f => f.Identifier == FileConnectionFactory.FileIdentifier);

        var connection = factory.Create(c => c.Filename = filename);

        if (!await AddConnection(connection))
        {
            return false;
        }

        await ViewModel.AddRecentConnectionCommand.ExecuteAsync(recent);

        return true;
    }

    private async Task<bool> ConnectBackup(ConnectBackupMessage message)
    {
        var factory = (IConnectionTypeFactory<BackupConnectionTypeConfig>)ConnectionFactories
            .Single(f => f.Identifier == BackupConnectionFactory.BackupIdentifier);

        var filenames = message.Filename.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var connection = factory.Create(c => c.Filenames = [.. filenames]);

        var error = await TryAddConnection(connection, message.Progress);

        if (error is null)
        {
            await ViewModel.AddRecentConnectionCommand.ExecuteAsync(message.Recent);

            return true;
        }

        await connection.DisposeAsync();

        if (IsExpectedBackupError(error))
        {
            message.ErrorMessage = error.Message;
        }
        else
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(error));
        }

        return false;
    }

    private static bool IsExpectedBackupError(Exception exception) =>
        exception is NotSupportedException
            or InvalidDataException
            or BackupMediaSetException
            or MissingDataFileException
            or FileNotFoundException
            or EndOfStreamException;

    private async Task<bool> OpenPage(OpenPageRequest request)
    {
        try
        {
            var viewModel = PageTabViewModelFactory.Create(request.Database);

            await viewModel.LoadPage(request.PageAddress, request.Slot);

            viewModel.LogRecords = new ObservableCollection<LogRecordItem>(
                request.LogRecords.Select(r => new LogRecordItem { Record = r }));

            var content = new PageView();

            content.DataContext = viewModel;

            var svg = new SvgImageSource(new Uri("ms-appx:///Assets/TabIcons/PageTabIcon.svg"));

            var title = $"Page {request.PageAddress.PageId}";

            var tab = new TabViewItem
            {
                Name = title,
                IconSource = new ImageIconSource { ImageSource = svg },
            };

            BindTabTitle(viewModel, tab);

            AddWindowTab(tab, content);
        }
        catch (Exception ex)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(ex));

            return false;
        }

        return true;
    }

    private bool OpenQuery(DatabaseSource database)
    {
        var viewModel = QueryViewModelFactory.Create(database);

        var content = new QueryView();

        content.DataContext = viewModel;

        var svg = new SvgImageSource(new Uri("ms-appx:///Assets/TabIcons/QueryTabIcon.svg"))
        {
            RasterizePixelHeight = 32, RasterizePixelWidth = 32
        };

        var title = $"Query";

        var tab = new TabViewItem
        {
            Name = title,
            IconSource = new ImageIconSource { ImageSource = svg },
        };

        BindTabTitle(viewModel, tab);

        AddWindowTab(tab, content);

        return true;
    }

    private bool OpenColumnstore(DatabaseSource database, long allocationUnitId)
    {
        try
        {
            var viewModel = ColumnstoreTabViewModelFactory.Create(database, allocationUnitId);

            var content = new ColumnstoreView { DataContext = viewModel };

            var svg = new SvgImageSource(new Uri("ms-appx:///Assets/TabIcons/ColumnstoreTabIcon.svg"));

            var tab = new TabViewItem
            {
                Name = "Columnstore",
                IconSource = new ImageIconSource { ImageSource = svg }
            };

            BindTabTitle(viewModel, tab);

            AddWindowTab(tab, content);

            return true;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Could not open the tab");

            return false;
        }
    }

    private bool OpenIndex(DatabaseSource database, PageAddress rootPageAddress)
    {
        try
        {
            var viewModel = IndexTabViewModelFactory.Create(database);

            viewModel.RootPage = rootPageAddress;

            var content = new IndexView();

            content.DataContext = viewModel;

            var title = $"Index";

            var svg = new SvgImageSource(new Uri("ms-appx:///Assets/TabIcons/IndexTabIcon.svg"));

            var tab = new TabViewItem
            {
                Name = title,
                IconSource = new ImageIconSource { ImageSource = svg },
            };


            BindTabTitle(viewModel, tab);

            AddWindowTab(tab, content);
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new ExceptionMessage(ex));
        }

        return true;
    }

    private void BindTabTitle(TabViewModel viewModel, TabViewItem tab)
    {
        var titleBinding = new Binding { Mode = BindingMode.OneWay };

        titleBinding.Source = viewModel;

        tab.Style = RootGrid.Resources["MainWindowTabStyle"] as Style;

        tab.SetBinding(TabViewItem.HeaderProperty, titleBinding);
    }

    private void AddWindowTab(TabViewItem tab, FrameworkElement content)
    {
        tab.Tag = content;

        content.Visibility = Visibility.Collapsed;

        TabContentHost.Children.Add(content);

        WindowTabView.TabItems.Add(tab);
        WindowTabView.SelectedItem = tab;
    }

    private void TabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = (WindowTabView.SelectedItem as TabViewItem)?.Tag;

        foreach (var child in TabContentHost.Children)
        {
            child.Visibility = ReferenceEquals(child, selected) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async Task<bool> AddConnection(IConnectionType connection)
    {
        var error = await TryAddConnection(connection);

        if (error is null)
        {
            return true;
        }

        await WeakReferenceMessenger.Default.Send(new ExceptionMessage(error));

        return false;
    }

    private async Task<Exception?> TryAddConnection(IConnectionType connection, IProgress<ProgressDetail>? progress = null)
    {
        try
        {
            DatabaseSource database = null!;

            await Task.Run(async () =>
            {
                database = await DatabaseService.LoadAsync(connection.Name, connection, WindowCts.Token, progress);
            });

            var viewModel = DatabaseTabViewModelFactory.Create(database);

            viewModel.Load(connection.Name);

            DispatcherQueue.TryEnqueue(() =>
            {
                var content = new DatabaseView();

                content.DataContext = viewModel;

                var svg = new SvgImageSource(new Uri("ms-appx:///Assets/TabIcons/DatabaseTabIcon.svg"));

                var tab = new TabViewItem
                {
                    Name = connection.Name,
                    IconSource = new ImageIconSource { ImageSource = svg }
                };

                BindTabTitle(viewModel, tab);

                AddWindowTab(tab, content);
            });

            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private void TabView_OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        // Close tab if it's not the connect tab
        if (args.Tab != ConnectTab)
        {
            // Dispose here rather than on Unloaded: TabView fires Unloaded on every tab switch
            // (the non-selected tab's content leaves the visual tree), which would tear down the
            // tab's content while it is still open. Disposing on actual close avoids that.
            if (args.Tab.Tag is FrameworkElement content)
            {
                TabContentHost.Children.Remove(content);

                if (content is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            sender.TabItems.Remove(args.Tab);
        }
    }

    private void TabView_Loaded(object sender, RoutedEventArgs e)
    {
        var tabView = sender as TabView;

        if (tabView != null)
        {
            ConnectTab = AddConnectTab();
            ConnectTab.IsClosable = false;
        }
    }

    private TabViewItem AddConnectTab()
    {
        var content = new ConnectView(ConnectServerViewModelFactory);

        content.DataContext = ViewModel;

        ViewModel.Name = "Internals Viewer";

        var icon = new ImageIconSource { ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon16.png")) };

        var connectTab = new TabViewItem
        {
            IconSource = icon,
            IsClosable = false
        };

        BindTabTitle(ViewModel, connectTab);

        AddWindowTab(connectTab, content);

        return connectTab;
    }

    private Task<bool> OpenLogTab()
    {
        if (LogTab != null && WindowTabView.TabItems.Contains(LogTab))
        {
            WindowTabView.SelectedItem = LogTab;
            return Task.FromResult(true);
        }

        var content = new AppLogView();

        var svg = new SvgImageSource(new Uri("ms-appx:///Assets/TabIcons/PageTabIcon.svg"));

        LogTab = new TabViewItem
        {
            Header = "Log",
            IconSource = new ImageIconSource { ImageSource = svg },
            IsClosable = true
        };

        AddWindowTab(LogTab, content);

        return Task.FromResult(true);
    }
}

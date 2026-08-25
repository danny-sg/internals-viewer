using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Query;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Schema;
using InternalsViewer.UI.App.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;

namespace InternalsViewer.UI.App.Controls.SqlEditor;

public sealed partial class SqlEditorControl : UserControl, IDisposable
{
    private const string FontSizeSettingKey = "SqlEditorFontSize";

    private const string ResultsVisibleSettingKey = "SqlEditorResultsVisible";

    public static readonly DependencyProperty ExecuteCommandProperty =
        DependencyProperty.Register(
            nameof(ExecuteCommand),
            typeof(ICommand),
            typeof(SqlEditorControl),
            new PropertyMetadata(null));

    public ICommand? ExecuteCommand
    {
        get => (ICommand)GetValue(ExecuteCommandProperty);
        set => SetValue(ExecuteCommandProperty, value);
    }

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register(
            nameof(CancelCommand),
            typeof(ICommand),
            typeof(SqlEditorControl),
            new PropertyMetadata(null));

    public ICommand CancelCommand
    {
        get => (ICommand)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public static readonly DependencyProperty SchemaProperty =
        DependencyProperty.Register(nameof(Schema), typeof(DatabaseSchema), typeof(SqlEditorControl),
            new PropertyMetadata(null, OnSchemaChanged));

    public DatabaseSchema? Schema
    {
        get => (DatabaseSchema?)GetValue(SchemaProperty);
        set => SetValue(SchemaProperty, value);
    }

    public static readonly DependencyProperty SqlTextProperty =
        DependencyProperty.Register(nameof(SqlText), typeof(string), typeof(SqlEditorControl),
            new PropertyMetadata(string.Empty, OnSqlTextChanged));

    public string SqlText
    {
        get => (string)GetValue(SqlTextProperty);
        set => SetValue(SqlTextProperty, value);
    }

    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(nameof(Theme), typeof(string), typeof(SqlEditorControl),
            new PropertyMetadata("vs-dark", OnThemeChanged));

    public string Theme
    {
        get => (string)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(SqlEditorControl),
            new PropertyMetadata(string.Empty));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public static readonly DependencyProperty IsErrorProperty =
        DependencyProperty.Register(nameof(IsError), typeof(bool), typeof(SqlEditorControl),
            new PropertyMetadata(false, OnIsErrorChanged));

    public bool IsError
    {
        get => (bool)GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    public static readonly DependencyProperty IsMessagesVisibleProperty =
        DependencyProperty.Register(nameof(IsMessagesVisible), typeof(bool), typeof(SqlEditorControl),
            new PropertyMetadata(false, OnIsMessagesVisibleChanged));

    public bool IsMessagesVisible
    {
        get => (bool)GetValue(IsMessagesVisibleProperty);
        set => SetValue(IsMessagesVisibleProperty, value);
    }

    public static readonly DependencyProperty IsResultsVisibleProperty =
        DependencyProperty.Register(nameof(IsResultsVisible), typeof(bool), typeof(SqlEditorControl),
            new PropertyMetadata(false, OnIsResultsVisibleChanged));

    public bool IsResultsVisible
    {
        get => (bool)GetValue(IsResultsVisibleProperty);
        set => SetValue(IsResultsVisibleProperty, value);
    }

    public static readonly DependencyProperty AdditionalContentProperty =
        DependencyProperty.Register(nameof(AdditionalContent), typeof(object), typeof(SqlEditorControl),
            new PropertyMetadata(null));

    public object? AdditionalContent
    {
        get => GetValue(AdditionalContentProperty);
        set => SetValue(AdditionalContentProperty, value);
    }

    public static readonly DependencyProperty ResultSetProperty =
        DependencyProperty.Register(nameof(ResultSet), typeof(QueryResultSet), typeof(SqlEditorControl),
            new PropertyMetadata(null, OnResultSetChanged));

    public QueryResultSet? ResultSet
    {
        get => (QueryResultSet?)GetValue(ResultSetProperty);
        set => SetValue(ResultSetProperty, value);
    }

    public static readonly DependencyProperty QueryOptionsProperty =
        DependencyProperty.Register(nameof(QueryOptions), typeof(QueryOptions), typeof(SqlEditorControl),
            new PropertyMetadata(new QueryOptions()));

    public QueryOptions QueryOptions
    {
        get => (QueryOptions)GetValue(QueryOptionsProperty);
        set => SetValue(QueryOptionsProperty, value);
    }

    public static readonly DependencyProperty IsExecutingProperty =
        DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(SqlEditorControl),
            new PropertyMetadata(false, OnIsExecutingChanged));

    public bool IsExecuting
    {
        get => (bool)GetValue(IsExecutingProperty);
        set => SetValue(IsExecutingProperty, value);
    }

    public static readonly DependencyProperty TrackedSelectionProperty =
        DependencyProperty.Register(nameof(TrackedSelection), typeof(TrackedSelectionRange), typeof(SqlEditorControl),
            new PropertyMetadata(null));

    public TrackedSelectionRange? TrackedSelection
    {
        get => (TrackedSelectionRange?)GetValue(TrackedSelectionProperty);
        set => SetValue(TrackedSelectionProperty, value);
    }

    private bool _editorReady;

    private bool _initialized;

    private bool _applyingEditorChange;

    private string _selectedText = string.Empty;

    public SqlEditorControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        ApplyBottomPanelVisibility();
        ApplyResultsTabVisibility();
    }

    public event EventHandler<string>? SqlTextChanged;

    public event EventHandler<TrackedSelectionRange?>? TrackedSelectionChanged;

    public bool IsEditorLoading { get; private set; } = true;

    public string ExecuteLabel => IsExecuting ? "Executing" : "Execute";

    public bool IsNotExecuting => !IsExecuting;

    public Visibility ExecutingVisibility => IsExecuting ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NotExecutingVisibility => IsExecuting ? Visibility.Collapsed : Visibility.Visible;

    public SolidColorBrush ResultBrush => IsError
        ? new SolidColorBrush(Colors.Red)
        : (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];

    private StatementParser StatementParser { get; } = new();

    public void Dispose()
    {
        Loaded -= OnLoaded;

        if (WebView.CoreWebView2 is { } core)
        {
            core.WebMessageReceived -= OnWebMessageReceived;
        }

        WebView.Close();
    }

    private void ApplyBottomPanelVisibility()
    {
        var hasResults = ResultSet is not null && IsResultsVisible;
        var show = IsMessagesVisible || hasResults;

        if (show)
        {
            if (MessagesRow.Height.GridUnitType == GridUnitType.Pixel && MessagesRow.Height.Value == 0)
            {
                MessagesRow.Height = new GridLength(1, GridUnitType.Star);
            }

            MessagesSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            MessagesRow.Height = new GridLength(0);
            MessagesSplitter.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyResultsTabVisibility()
    {
        ResultsTab.Visibility = ResultSet is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HandleExecuteClick()
    {
        var text = string.IsNullOrEmpty(_selectedText) ? SqlText : _selectedText;

        var payload = new ExecuteSqlPayload(text,
                                            QueryOptions,
                                            StatementParser.GetStatementType(text),
                                            TrackedSelection);

        if (ExecuteCommand != null && ExecuteCommand.CanExecute(payload))
        {
            IsMessagesVisible = true;

            ExecuteCommand.Execute(payload);
        }
    }

#pragma warning disable VSTHRD100 // Avoid async void methods - Required for event and using try/catch for safety
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_initialized)
            {
                IsEditorLoading = !_editorReady;
                LoadingOverlay.Visibility = _editorReady ? Visibility.Collapsed : Visibility.Visible;
                PushSqlTextToEditor();
                return;
            }

            _initialized = true;
            IsEditorLoading = true;
            LoadingOverlay.Visibility = Visibility.Visible;

            await ApplySavedResultsVisibilityAsync();

            await WebView.EnsureCoreWebView2Async();

            WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Monaco");

            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "monaco.localhost",
                assetsPath,
                CoreWebView2HostResourceAccessKind.Allow);

            WebView.CoreWebView2.Navigate("http://monaco.localhost/index.html");
        }
        catch (Exception ex)
        {
            IsEditorLoading = false;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            await WeakReferenceMessenger.Default.Send(
                new ExceptionMessage(ex) { Message = "Exception initializing Monaco editor" });
        }
    }

    // Pushes the current SqlText into the Monaco editor (when it is ready).
    private void PushSqlTextToEditor()
    {
        if (!_editorReady || _applyingEditorChange)
        {
            return;
        }

        var escaped = JsonSerializer.Serialize(SqlText);

        _ = WebView.ExecuteScriptAsync($"window.setEditorValue({escaped})");
    }

#pragma warning restore VSTHRD100

#pragma warning disable VSTHRD100 // Avoid async void methods - Required for event and using try/catch for safety
    private async void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();

            if (json is not null)
            {
                var msg = JsonSerializer.Deserialize<EditorMessage>(json);

                switch (msg?.Type)
                {
                    case "ready":
                        _editorReady = true;
                        IsEditorLoading = false;
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                        await PushSchemaToEditorAsync();
                        PushSqlTextToEditor();
                        await ApplySavedFontSizeAsync();
                        break;

                    case "fontSizeChanged":
                        if (msg.Value is { ValueKind: JsonValueKind.Number } fontSize)
                        {
                            await App.GetService<SettingsService>()
                                     .SaveSettingAsync(FontSizeSettingKey, fontSize.GetDouble());
                        }

                        break;

                    case "execute":
                        HandleExecuteClick();
                        break;

                    case "selectionChanged":
                        _selectedText = msg.Value?.GetString() ?? string.Empty;
                        break;

                    case "contentChanged":
                        var newContent = msg.Value?.GetString();

                        if (SqlText != newContent)
                        {
                            _applyingEditorChange = true;
                            try
                            {
                                SqlText = newContent ?? string.Empty;
                                SqlTextChanged?.Invoke(this, SqlText);
                            }
                            finally
                            {
                                _applyingEditorChange = false;
                            }
                        }

                        break;

                    case "trackedSelectionChanged":
                        TrackedSelectionRange? range = null;

                        if (msg.Value is { ValueKind: JsonValueKind.Object } rangeElement)
                        {
                            range = new TrackedSelectionRange(
                                rangeElement.GetProperty("start").GetInt32(),
                                rangeElement.GetProperty("end").GetInt32());
                        }

                        if (range != TrackedSelection)
                        {
                            TrackedSelection = range;
                            TrackedSelectionChanged?.Invoke(this, range);
                        }

                        break;
                }
            }
        }
        catch (Exception ex)
        {
            IsEditorLoading = false;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            await WeakReferenceMessenger.Default.Send(
                new ExceptionMessage(ex) { Message = "Exception on receive web message" });
        }
    }

#pragma warning restore VSTHRD100

    private async Task ApplySavedResultsVisibilityAsync()
    {
        var saved = await App.GetService<SettingsService>().ReadSettingAsync<bool?>(ResultsVisibleSettingKey) ?? false;

        IsResultsVisible = saved;

        QueryOptions = QueryOptions with { IncludeResults = saved };
    }

    // Applies the persisted editor font size (set by Ctrl+wheel zoom) once the editor is ready
    private async Task ApplySavedFontSizeAsync()
    {
        if (!_editorReady)
        {
            return;
        }

        var fontSize = await App.GetService<SettingsService>().ReadSettingAsync<double?>(FontSizeSettingKey);

        if (fontSize is > 0)
        {
            var value = fontSize.Value.ToString(CultureInfo.InvariantCulture);

            await WebView.ExecuteScriptAsync($"window.setEditorFontSize({value})");
        }
    }

    private async Task PushSchemaToEditorAsync()
    {
        if (!_editorReady || Schema == null)
        {
            return;
        }
        ;

        var jsonSchema = JsonSerializer.Serialize(Schema, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var script = $"window.setSqlSchema({jsonSchema});";

        await WebView.ExecuteScriptAsync(script);
    }

    private sealed record EditorMessage([property: JsonPropertyName("type")] string Type,
                                        [property: JsonPropertyName("value")] JsonElement? Value);

    private static void OnIsErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            var control = (SqlEditorControl)d;
            control.IsMessagesVisible = true;
            control.ApplyBottomPanelVisibility();
        }
    }

    private static void OnIsMessagesVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SqlEditorControl)d).ApplyBottomPanelVisibility();

    private static void OnIsResultsVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SqlEditorControl)d;
        control.QueryOptions = control.QueryOptions with { IncludeResults = (bool)e.NewValue };
        control.ApplyBottomPanelVisibility();
        control.ApplyResultsTabVisibility();

        _ = App.GetService<SettingsService>().SaveSettingAsync(ResultsVisibleSettingKey, (bool)e.NewValue);
    }

    private static void OnResultSetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SqlEditorControl)d;
        control.ApplyBottomPanelVisibility();
        control.ApplyResultsTabVisibility();

        if (e.NewValue is not null && control.IsResultsVisible && !control.IsError)
        {
            control.ResultsTabView.SelectedItem = control.ResultsTab;
        }
    }

    private static void OnIsExecutingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            var control = (SqlEditorControl)d;

            control.IsMessagesVisible = true;
            control.ResultsTabView.SelectedItem = control.MessagesTab;
        }
    }

    private static void OnSchemaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SqlEditorControl)d;

        if (control._editorReady)
        {
            _ = control.PushSchemaToEditorAsync();
        }
    }

    private static void OnSqlTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SqlEditorControl)d).PushSqlTextToEditor();

    private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SqlEditorControl)d;

        if (control._editorReady)
        {
            var theme = JsonSerializer.Serialize((string)(e.NewValue ?? "vs-dark"));
            _ = control.WebView.ExecuteScriptAsync($"monaco.editor.setTheme({theme})");
        }
    }
}
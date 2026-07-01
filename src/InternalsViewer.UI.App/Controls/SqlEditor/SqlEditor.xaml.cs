using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using InternalsViewer.Query.Parsing;
using InternalsViewer.UI.App.Models.Query;
using InternalsViewer.UI.App.Models.Schema;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;

namespace InternalsViewer.UI.App.Controls.SqlEditor;

public sealed record ExecuteSqlPayload(string SqlText, QueryOptions QueryOptions, StatementType StatementType);

public sealed partial class SqlEditorControl : UserControl
{
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

    public static readonly DependencyProperty SqlTextProperty =
        DependencyProperty.Register(nameof(SqlText), typeof(string), typeof(SqlEditorControl),
            new PropertyMetadata(string.Empty, OnSqlTextChanged));

    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(nameof(Theme), typeof(string), typeof(SqlEditorControl),
            new PropertyMetadata("vs-dark", OnThemeChanged));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(SqlEditorControl),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsErrorProperty =
        DependencyProperty.Register(nameof(IsError), typeof(bool), typeof(SqlEditorControl),
            new PropertyMetadata(false, OnIsErrorChanged));

    public static readonly DependencyProperty IsMessagesVisibleProperty =
        DependencyProperty.Register(nameof(IsMessagesVisible), typeof(bool), typeof(SqlEditorControl),
            new PropertyMetadata(false, OnIsMessagesVisibleChanged));

    public static readonly DependencyProperty AdditionalContentProperty =
        DependencyProperty.Register(nameof(AdditionalContent), typeof(object), typeof(SqlEditorControl),
            new PropertyMetadata(null));

    private static void OnIsErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            ((SqlEditorControl)d).IsMessagesVisible = true;
        }
    }

    private static void OnIsMessagesVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SqlEditorControl)d).ApplyMessagesVisibility();

    public static readonly DependencyProperty QueryOptionsProperty =
        DependencyProperty.Register(nameof(QueryOptions), typeof(QueryOptions), typeof(SqlEditorControl),
            new PropertyMetadata(new QueryOptions()));

    public static readonly DependencyProperty IsExecutingProperty =
        DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(SqlEditorControl),
            new PropertyMetadata(false));

    public string SqlText
    {
        get => (string)GetValue(SqlTextProperty);
        set => SetValue(SqlTextProperty, value);
    }

    public DatabaseSchema? Schema
    {
        get => (DatabaseSchema?)GetValue(SchemaProperty);
        set => SetValue(SchemaProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool IsError
    {
        get => (bool)GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    public bool IsExecuting
    {
        get => (bool)GetValue(IsExecutingProperty);
        set => SetValue(IsExecutingProperty, value);
    }

    public bool IsMessagesVisible
    {
        get => (bool)GetValue(IsMessagesVisibleProperty);
        set => SetValue(IsMessagesVisibleProperty, value);
    }

    public object? AdditionalContent
    {
        get => GetValue(AdditionalContentProperty);
        set => SetValue(AdditionalContentProperty, value);
    }

    public QueryOptions QueryOptions
    {
        get => (QueryOptions)GetValue(QueryOptionsProperty);
        set => SetValue(QueryOptionsProperty, value);
    }

    public string Theme
    {
        get => (string)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public event EventHandler<string>? SqlTextChanged;

    public string ExecuteLabel => IsExecuting ? "Executing" : "Execute";

    public bool IsNotExecuting => !IsExecuting;

    public Visibility ExecutingVisibility => IsExecuting ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NotExecutingVisibility => IsExecuting ? Visibility.Collapsed : Visibility.Visible;

    public SolidColorBrush ResultBrush => IsError
        ? new SolidColorBrush(Colors.Red)
        : (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];

    private bool _editorReady;

    private bool _initialized;

    private bool _applyingEditorChange;

    private string _selectedText = string.Empty;

    private StatementParser StatementParser { get; } = new();

    public SqlEditorControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        ApplyMessagesVisibility();
    }

    private void ApplyMessagesVisibility()
    {
        MessagesRow.Height = IsMessagesVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        MessagesSplitter.Visibility = IsMessagesVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HandleExecuteClick()
    {
        var text = string.IsNullOrEmpty(_selectedText) ? SqlText : _selectedText;
        var payload = new ExecuteSqlPayload(text, QueryOptions, StatementParser.GetStatementType(text));

        if (ExecuteCommand != null && ExecuteCommand.CanExecute(payload))
        {
            IsMessagesVisible = true;

            ExecuteCommand.Execute(payload);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            PushSqlTextToEditor();
            return;
        }

        _initialized = true;

        await WebView.EnsureCoreWebView2Async();

        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Monaco");

        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "monaco.local",
            assetsPath,
            CoreWebView2HostResourceAccessKind.Allow);

        WebView.CoreWebView2.Navigate("http://monaco.local/index.html");
    }

    // Pushes the current SqlText into the Monaco editor (when it is ready).
    private void PushSqlTextToEditor()
    {
        if (!_editorReady || _applyingEditorChange)
        {
            return;
        }

        var escaped = JsonSerializer.Serialize(SqlText ?? string.Empty);

        _ = WebView.ExecuteScriptAsync($"window.setEditorValue({escaped})");
    }

    private async void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var json = e.TryGetWebMessageAsString();

        if (json is not null)
        {
            var msg = JsonSerializer.Deserialize<EditorMessage>(json);

            switch (msg?.Type)
            {
                case "ready":
                    _editorReady = true;
                    await PushSchemaToEditorAsync();
                    PushSqlTextToEditor();
                    break;

                case "execute":
                    HandleExecuteClick();
                    break;

                case "selectionChanged":
                    _selectedText = msg.Value ?? string.Empty;
                    break;

                case "contentChanged":
                    if (SqlText != msg.Value)
                    {
                        _applyingEditorChange = true;
                        try
                        {
                            SqlText = msg.Value ?? string.Empty;
                            SqlTextChanged?.Invoke(this, SqlText);
                        }
                        finally
                        {
                            _applyingEditorChange = false;
                        }
                    }

                    break;
            }
        }
    }

    private async Task PushSchemaToEditorAsync()
    {
        if (!_editorReady || Schema == null)
        {
            return;
        };

        var jsonSchema = JsonSerializer.Serialize(Schema, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var script = $"window.setSqlSchema({jsonSchema});";

        await WebView.ExecuteScriptAsync(script);
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

    private sealed record EditorMessage([property: JsonPropertyName("type")] string Type,
                                        [property: JsonPropertyName("value")] string? Value);
}
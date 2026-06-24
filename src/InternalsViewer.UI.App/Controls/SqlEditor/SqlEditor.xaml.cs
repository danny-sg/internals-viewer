using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Models.Query;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using InternalsViewer.Replay.Parsing;

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
            new PropertyMetadata(false));

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

    private StatementParser statementParser { get; } = new();

    public SqlEditorControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void HandleExecuteClick()
    {
        var payload = new ExecuteSqlPayload(SqlText, QueryOptions, statementParser.GetStatementType(SqlText));

        if (this.ExecuteCommand != null && this.ExecuteCommand.CanExecute(payload))
        {
            this.ExecuteCommand.Execute(payload);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();

        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        WebView.NavigateToString(BuildHtml(SqlText, Theme));
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var json = e.TryGetWebMessageAsString();

        if (json is not null)
        {
            var msg = JsonSerializer.Deserialize<EditorMessage>(json);

            switch (msg?.Type)
            {
                case "ready":
                    _editorReady = true;
                    break;

                case "contentChanged":
                    if (SqlText != msg.Value)
                    {
                        SqlText = msg.Value ?? string.Empty;
                        SqlTextChanged?.Invoke(this, SqlText);
                    }

                    break;
            }
        }
    }

    private static void OnSqlTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SqlEditorControl)d;
        var newValue = (string)(e.NewValue ?? string.Empty);

        if (control._editorReady)
        {
            // Push new content into Monaco without reloading
            var escaped = JsonSerializer.Serialize(newValue);
            _ = control.WebView.ExecuteScriptAsync($"setEditorValue({escaped})");
        }
    }

    private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SqlEditorControl)d;

        if (control._editorReady)
        {
            var theme = JsonSerializer.Serialize((string)(e.NewValue ?? "vs-dark"));
            _ = control.WebView.ExecuteScriptAsync($"monaco.editor.setTheme({theme})");
        }
    }

    private static string BuildHtml(string initialSql, string theme)
    {
        var escapedSql = JsonSerializer.Serialize(initialSql);
        var escapedTheme = JsonSerializer.Serialize(theme);

        return $$"""
            <!DOCTYPE html>
            <html style="height:100%;margin:0;padding:0;">
            <head>
                <meta charset="UTF-8" />
                <style>
                    * { box-sizing: border-box; }
                    html, body { height: 100%; margin: 0; padding: 0; overflow: hidden; }
                    #container { height: 100%; }
                </style>
            </head>
            <body>
                <div id="container"></div>

                <script src="https://cdn.jsdelivr.net/npm/monaco-editor@0.52.0/min/vs/loader.js"></script>
                <script>
                    require.config({
                        paths: { vs: 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.0/min/vs' }
                    });

                    require(['vs/editor/editor.main'], function () {
                        const editor = monaco.editor.create(document.getElementById('container'), {
                            value: {{escapedSql}},
                            language: 'sql',
                            theme: {{escapedTheme}},
                            fontSize: 13,
                            fontFamily: "'Cascadia Code', 'Consolas', monospace",
                            fontLigatures: true,
                            minimap: { enabled: false },
                            automaticLayout: true,
                            scrollBeyondLastLine: false,
                            wordWrap: 'off',
                            lineNumbers: 'on',
                            renderLineHighlight: 'gutter',
                            suggestOnTriggerCharacters: true,
                            quickSuggestions: true
                        });

                        // Notify the host when content changes
                        editor.onDidChangeModelContent(() => {
                            window.chrome.webview.postMessage(
                                JSON.stringify({ type: 'contentChanged', value: editor.getValue() })
                            );
                        });

                        // Allow the host to push new content in
                        window.setEditorValue = (val) => {
                            if (editor.getValue() !== val) {
                                editor.setValue(val);
                            }
                        };

                        window._editorReady = true;
                        window.chrome.webview.postMessage(JSON.stringify({ type: 'ready' }));
                    });
                </script>
            </body>
            </html>
            """;
    }

    private sealed record EditorMessage([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("value")] string? Value);
}
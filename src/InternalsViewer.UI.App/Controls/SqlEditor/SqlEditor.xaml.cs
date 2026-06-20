using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace InternalsViewer.UI.App.Controls.SqlEditor;

public sealed partial class SqlEditorControl : UserControl
{
    public static readonly DependencyProperty SqlTextProperty =
        DependencyProperty.Register(nameof(SqlText), typeof(string), typeof(SqlEditorControl),
            new PropertyMetadata(string.Empty, OnSqlTextChanged));

    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(nameof(Theme), typeof(string), typeof(SqlEditorControl),
            new PropertyMetadata("vs-dark", OnThemeChanged));

    public string SqlText
    {
        get => (string)GetValue(SqlTextProperty);
        set => SetValue(SqlTextProperty, value);
    }

    /// <summary>
    /// Monaco theme: "vs", "vs-dark", or "hc-black"
    /// </summary>
    public string Theme
    {
        get => (string)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public event EventHandler<string>? SqlTextChanged;

    private bool _editorReady;

    public SqlEditorControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
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

        if (json is null)
            return;

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

    private string BuildHtml(string initialSql, string theme)
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

    private record EditorMessage([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("value")] string? Value);
}
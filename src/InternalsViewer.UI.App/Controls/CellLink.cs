using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls;

/// <summary>
/// Something that a link drawn in one of its cells can hand a click back to
/// </summary>
/// <remarks>
/// A grid holds several kinds of link at once, so the kind says which one was followed and the parameter says
/// what row it was on.
/// </remarks>
public interface ICellLinkNavigator
{
    void OnLinkInvoked(string kind, object? parameter);
}

/// <summary>
/// Draws a link into a text block, as text rather than as a button
/// </summary>
/// <remarks>
/// A cell is built for every row on screen and again as rows recycle, so a button in one pays for its template
/// and its visual states every time. A hyperlink is an inline, which costs a text layout and nothing else.
/// </remarks>
public static class CellLink
{
    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value);

    public static readonly DependencyProperty TextProperty
        = DependencyProperty.RegisterAttached("Text",
                                              typeof(string),
                                              typeof(CellLink),
                                              new PropertyMetadata(null, OnChanged));

    /// <summary>
    /// Which of the grid's links this is, the host having more than one kind to tell apart
    /// </summary>
    public static string? GetKind(DependencyObject element) => (string?)element.GetValue(KindProperty);

    public static void SetKind(DependencyObject element, string? value) => element.SetValue(KindProperty, value);

    public static readonly DependencyProperty KindProperty
        = DependencyProperty.RegisterAttached("Kind",
                                              typeof(string),
                                              typeof(CellLink),
                                              new PropertyMetadata(null, OnChanged));

    public static object? GetParameter(DependencyObject element) => element.GetValue(ParameterProperty);

    public static void SetParameter(DependencyObject element, object? value)
        => element.SetValue(ParameterProperty, value);

    public static readonly DependencyProperty ParameterProperty
        = DependencyProperty.RegisterAttached("Parameter",
                                              typeof(object),
                                              typeof(CellLink),
                                              new PropertyMetadata(null, OnChanged));

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock text)
        {
            Build(text);
        }
    }

    private static void Build(TextBlock text)
    {
        text.Inlines.Clear();

        if (GetText(text) is not { Length: > 0 } content)
        {
            return;
        }

        var link = new Hyperlink
        {
            UnderlineStyle = UnderlineStyle.None,
            Foreground = Accent(text)
        };

        link.Inlines.Add(new Run { Text = content });

        link.Click += (sender, _) => Navigator(sender)?.OnLinkInvoked(GetKind(text) ?? string.Empty,
                                                                     GetParameter(text));

        text.Inlines.Add(link);
    }

    /// <summary>
    /// Whatever is hosting the cell the link was drawn in, found by walking up from the link itself
    /// </summary>
    private static ICellLinkNavigator? Navigator(Hyperlink link)
    {
        DependencyObject? current = link.ContentStart.VisualParent;

        while (current is not null)
        {
            if (current is ICellLinkNavigator navigator)
            {
                return navigator;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static Brush? _accent;

    private static ElementTheme _accentTheme = ElementTheme.Default;

    /// <summary>
    /// The colour a link is written in, held against the theme it was read for
    /// </summary>
    private static Brush? Accent(TextBlock text)
    {
        if (_accent is not null && _accentTheme == text.ActualTheme)
        {
            return _accent;
        }

        _accentTheme = text.ActualTheme;

        return _accent = Application.Current.Resources.TryGetValue("AccentTextFillColorSecondaryBrush", out var resource)
            ? resource as Brush
            : null;
    }
}

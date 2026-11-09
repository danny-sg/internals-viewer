using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Controls;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Models.Query.Plan;

public sealed partial class GuideStepItem(int index, string name, string description, string? codeColour, GuideStepGroup group)
    : ObservableObject
{
    private const double CodeFontSize = 12;

    [ObservableProperty]
    private bool _isCurrent;

    public int Index => index;

    public string Name => name;

    public string Description => description;

    public MarkdownConfig Config => GetConfig(codeColour);

    public GuideStepGroup Group => group;

    private static Dictionary<string, MarkdownConfig> Configs { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static MarkdownConfig GetConfig(string? colour)
    {
        var key = colour ?? string.Empty;

        if (!Configs.TryGetValue(key, out var config))
        {
            config = CreateConfig(colour);

            Configs[key] = config;
        }

        return config;
    }

    private static MarkdownConfig CreateConfig(string? colour)
    {
        var background = ColourHelpers.FromHex(colour) ?? Colors.Gainsboro;

        var foreground = System.Drawing.Color.White.ContrastingWith(background.ToColor());

        var themes = new MarkdownThemes
        {
            ParagraphMargin = new Thickness(0),
            InlineCodeFontSize = CodeFontSize,
            InlineCodeBorderThickness = new Thickness(0),
            InlineCodeBorderBrush = new SolidColorBrush(Colors.Transparent),
            InlineCodePadding = new Thickness(3, 0, 3, 1),
            InlineCodeBackground = new SolidColorBrush(background),
            InlineCodeForeground = new SolidColorBrush(foreground.ToWindowsColor()),
            LinkForeground = new SolidColorBrush(Colors.Navy)
        };

        return new MarkdownConfig { Themes = themes };
    }
}

using System.Collections.Generic;
using System.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls;

/// <summary>
/// A SQL data type written the way it would be declared, coloured the way the editor colours it
/// </summary>
/// <remarks>
/// The arguments a type carries depend on the type, so precision, scale and length are all taken and only the ones
/// that type declares are shown. A length is not a precision, however the two are given as one number in the
/// metadata for every type that takes either, so both are accepted rather than the caller having to choose.
/// </remarks>
public sealed partial class DataTypeTextBlock
{
    /// <summary>
    /// Types whose length is declared in characters, the metadata holding it in bytes
    /// </summary>
    private static readonly HashSet<SqlDbType> WideTypes = [SqlDbType.NChar, SqlDbType.NVarChar, SqlDbType.NText];

    private static readonly HashSet<SqlDbType> LengthTypes =
    [
        SqlDbType.Char, 
        SqlDbType.VarChar, 
        SqlDbType.NChar,
        SqlDbType.NVarChar,
        SqlDbType.Binary, 
        SqlDbType.VarBinary
    ];

    private static readonly HashSet<SqlDbType> ScaleTypes =
    [
        SqlDbType.DateTime2, 
        SqlDbType.Time, SqlDbType.DateTimeOffset
    ];

    public DataTypeTextBlock()
    {
        InitializeComponent();

        ActualThemeChanged += (_, _) => Render();

        Loaded += (_, _) => Render();
    }

    public SqlDbType? DataType
    {
        get => (SqlDbType?)GetValue(DataTypeProperty);
        set => SetValue(DataTypeProperty, value);
    }

    public static readonly DependencyProperty DataTypeProperty
        = DependencyProperty.Register(nameof(DataType),
                                      typeof(SqlDbType?),
                                      typeof(DataTypeTextBlock),
                                      new PropertyMetadata(null, OnSourceChanged));

    public int Precision
    {
        get => (int)GetValue(PrecisionProperty);
        set => SetValue(PrecisionProperty, value);
    }

    public static readonly DependencyProperty PrecisionProperty
        = DependencyProperty.Register(nameof(Precision),
                                      typeof(int),
                                      typeof(DataTypeTextBlock),
                                      new PropertyMetadata(0, OnSourceChanged));

    public int DataScale
    {
        get => (int)GetValue(DataScaleProperty);
        set => SetValue(DataScaleProperty, value);
    }

    public static readonly DependencyProperty DataScaleProperty
        = DependencyProperty.Register(nameof(DataScale),
                                      typeof(int),
                                      typeof(DataTypeTextBlock),
                                      new PropertyMetadata(0, OnSourceChanged));

    /// <summary>
    /// Length in bytes as the metadata holds it, which a wide type declares as half of
    /// </summary>
    public int Length
    {
        get => (int)GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    public static readonly DependencyProperty LengthProperty
        = DependencyProperty.Register(nameof(Length),
                                      typeof(int),
                                      typeof(DataTypeTextBlock),
                                      new PropertyMetadata(0, OnSourceChanged));

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DataTypeTextBlock)d).Render();

    private void Render()
    {
        TextHost.Inlines.Clear();

        if (DataType is not { } type)
        {
            return;
        }

        TextHost.Inlines.Add(Run(GetName(type), "SqlKeywordBrush"));

        var arguments = GetArguments(type);

        if (arguments.Count == 0)
        {
            return;
        }

        TextHost.Inlines.Add(Run("(", "SqlPunctuationBrush"));

        for (var i = 0; i < arguments.Count; i++)
        {
            if (i > 0)
            {
                TextHost.Inlines.Add(Run(", ", "SqlPunctuationBrush"));
            }

            // max is a keyword rather than a number, which is how the editor colours it
            TextHost.Inlines.Add(Run(arguments[i], arguments[i] == "max" ? "SqlKeywordBrush" : "SqlNumberBrush"));
        }

        TextHost.Inlines.Add(Run(")", "SqlPunctuationBrush"));
    }

    /// <summary>
    /// The arguments the type declares, which is none for most of them
    /// </summary>
    private List<string> GetArguments(SqlDbType type)
    {
        if (type is SqlDbType.Decimal)
        {
            return [$"{Precision}", $"{DataScale}"];
        }

        if (ScaleTypes.Contains(type))
        {
            return [$"{DataScale}"];
        }

        if (!LengthTypes.Contains(type))
        {
            return [];
        }

        if (Length < 0)
        {
            return ["max"];
        }

        return Length == 0 ? [] : [$"{(WideTypes.Contains(type) ? Length / 2 : Length)}"];
    }

    /// <summary>
    /// The name as it is written in a declaration, which is the enum name lowered but for a couple of exceptions
    /// </summary>
    private static string GetName(SqlDbType type) => type switch
    {
        SqlDbType.Variant => "SQL_VARIANT",
        SqlDbType.UniqueIdentifier => "UNIQUEIDENTIFIER",
        SqlDbType.DateTimeOffset => "DATETIMEOFFSET",
        _ => type.ToString().ToUpperInvariant()
    };

    private Run Run(string text, string brush) => new()
    {
        Text = text,
        Foreground = (Brush)Application.Current.Resources[brush]
    };
}

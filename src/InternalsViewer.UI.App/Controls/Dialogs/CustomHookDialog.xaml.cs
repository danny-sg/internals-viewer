using System.Globalization;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Dialogs;

public sealed partial class CustomHookDialog
{
    private const double DefaultLength = 256;

    private const string HexPrefix = "0x";

    public CustomHookDialog(string function, string argument, bool isReturnValue)
    {
        Description = $"{function}, {argument}";

        HookName = $"{function} {argument}";

        ReadAtIndex = isReturnValue ? 1 : 0;

        InitializeComponent();

        ReadAtButtons.IsEnabled = !isReturnValue;
    }

    public string Description { get; }

    public string HookName { get; set; }

    public double Length { get; set; } = DefaultLength;

    public string OffsetText { get; set; } = string.Empty;

    public int ReadAtIndex { get; set; }

    public bool IsReadAtReturn => ReadAtIndex == 1;

    public int Offset { get; private set; }

    private static bool TryParseOffset(string text, out int offset)
    {
        var value = text.Trim();

        if (value.Length == 0)
        {
            offset = 0;

            return true;
        }

        var isNegative = value.StartsWith('-');

        if (isNegative)
        {
            value = value[1..];
        }

        var isParsed = value.StartsWith(HexPrefix, System.StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(value[HexPrefix.Length..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset)
            : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out offset);

        offset = isNegative ? -offset : offset;

        return isParsed;
    }

    private void OnAddClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var isOffsetValid = TryParseOffset(OffsetText, out var offset);

        var error = string.IsNullOrWhiteSpace(HookName) ? "Enter a name for the hook"
                    : double.IsNaN(Length) || Length < 1 ? "Enter how many bytes to read"
                    : !isOffsetValid ? "Enter the offset as a number, such as 24 or 0x18"
                    : null;

        if (error is not null)
        {
            ErrorText.Text = error;

            ErrorText.Visibility = Visibility.Visible;

            args.Cancel = true;

            return;
        }

        Offset = offset;
    }
}

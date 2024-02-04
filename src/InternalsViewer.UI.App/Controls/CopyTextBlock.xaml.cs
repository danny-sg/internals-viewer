using Windows.ApplicationModel.DataTransfer;

namespace InternalsViewer.UI.App.Controls;

public sealed partial class CopyTextBlock
{
    public string Value
    {
        get { return (string)GetValue(ValueProperty); }
        set { SetValue(ValueProperty, value); }
    }

    private static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(CopyTextBlock), new PropertyMetadata(string.Empty));

    public CopyTextBlock()
    {
        InitializeComponent();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var value = (sender as CopyButton)?.Tag.ToString() ?? string.Empty;

        var package = new DataPackage();

        package.SetText(value);

        Clipboard.SetContent(package);
    }
}

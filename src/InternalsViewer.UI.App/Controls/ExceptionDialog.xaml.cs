using Microsoft.UI.Xaml.Controls;
using System.Text;
using Windows.ApplicationModel.DataTransfer;

namespace InternalsViewer.UI.App.Controls;

public sealed partial class ExceptionDialog
{
    public string Message
    {
        get { return (string)GetValue(MessageProperty); }
        set { SetValue(MessageProperty, value); }
    }

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ExceptionDialog), new PropertyMetadata(null));

    public string StackTrace
    {
        get { return (string)GetValue(StackTraceProperty); }
        set { SetValue(StackTraceProperty, value); }
    }

    public static readonly DependencyProperty StackTraceProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ExceptionDialog), new PropertyMetadata(null));

    public ExceptionDialog()
    {
        InitializeComponent();
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var text = new StringBuilder();

        text.AppendLine(Message);
        text.AppendLine();
        text.AppendLine(StackTrace);

        var package = new DataPackage();

        package.SetText(text.ToString());
        
        Clipboard.SetContent(package);
    }
}

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace InternalsViewer.UI.App.vNext.Controls;

public sealed class CopyButton : Button
{
    public CopyButton()
    {
        DefaultStyleKey = typeof(CopyButton);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetTemplateChild("CopyToClipboardSuccessAnimation") is Storyboard storyBoard)
        {
            storyBoard.Begin();
        }
    }

    protected override void OnApplyTemplate()
    {
        Click -= CopyButton_Click;

        base.OnApplyTemplate();
        
        Click += CopyButton_Click;
    }
}

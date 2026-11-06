using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Calls.Symbols;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Calls;

public sealed partial class ClassMemberLine : UserControl
{
    public ClassMemberLine()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public ClassMemberRow? Member => DataContext as ClassMemberRow;

    private void OnCopySignatureClick(object sender, RoutedEventArgs e)
    {
        if (Member is { } member)
        {
            ClipboardText.Copy(member.Signature);
        }
    }

    private void OnCopySymbolClick(object sender, RoutedEventArgs e)
    {
        if (Member is { } member)
        {
            ClipboardText.Copy(member.Reference.QualifiedName);
        }
    }
}

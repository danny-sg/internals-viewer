using InternalsViewer.Query.CallStack;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Symbols;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Query.Trace.CallStack;

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
            ClipboardText.Copy(QualifiedSymbol.Of(member.Reference));
        }
    }
}

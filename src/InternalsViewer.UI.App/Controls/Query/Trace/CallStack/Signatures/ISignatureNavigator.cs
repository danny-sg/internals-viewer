namespace InternalsViewer.UI.App.Controls.Query.Trace.CallStack.Signatures;

/// <summary>
/// Something that a type followed from a signature can hand the click back to
/// </summary>
public interface ISignatureNavigator
{
    void OnTypeInvoked(string typeName);
}

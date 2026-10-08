namespace InternalsViewer.Query.Debugging.Interfaces;

public interface IWinDbgSettings
{
    string WinDbgPath { get; }

    string WinDbgPassword { get; }
}

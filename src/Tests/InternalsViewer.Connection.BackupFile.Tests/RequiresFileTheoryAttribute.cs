namespace InternalsViewer.Connection.BackupFile.Tests;

/// <summary>
/// xUnit Theory attribute that skips the test when any required file does not exist
/// </summary>
public sealed class RequiresFileTheoryAttribute(params string[] filePaths) : TheoryAttribute
{
    public override string? Skip
    {
        get
        {
            var missing = filePaths.FirstOrDefault(f => !File.Exists(f));

            return missing is null ? null : $"Requires file '{missing}'";
        }
    }
}

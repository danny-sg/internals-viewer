namespace InternalsViewer.Connection.BackupFile.Tests;

/// <summary>
/// xUnit Fact attribute that skips the test when the required file does not exist
/// </summary>
public sealed class RequiresFileFactAttribute(params string[] filePaths) : FactAttribute
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

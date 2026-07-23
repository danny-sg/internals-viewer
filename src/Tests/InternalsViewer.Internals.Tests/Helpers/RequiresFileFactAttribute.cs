namespace InternalsViewer.Internals.Tests.Helpers;

/// <summary>
/// xUnit Fact attribute that skips the test when the required file does not exist
/// </summary>
public sealed class RequiresFileFactAttribute(string filePath) : FactAttribute
{
    public override string? Skip
    {
        get
        {
            return File.Exists(filePath) ? null : $"Requires file '{filePath}'";
        }
    }
}

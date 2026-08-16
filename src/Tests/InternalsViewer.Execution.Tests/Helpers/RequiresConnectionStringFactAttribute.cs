namespace InternalsViewer.Execution.Tests.Helpers;

/// <summary>
/// Fact that skips when the named connection string is not in user secrets, so a machine without the demo server still runs the suite
/// </summary>
public sealed class RequiresConnectionStringFactAttribute(string name) : FactAttribute
{
    public override string? Skip
    {
        get
        {
            var connectionString = ConnectionStringHelper.GetConnectionString(name);

            return string.IsNullOrWhiteSpace(connectionString)
                ? $"Requires connection string '{name}' in user secrets"
                : null;
        }
    }
}

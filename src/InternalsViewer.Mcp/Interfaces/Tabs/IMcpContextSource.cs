using InternalsViewer.Mcp.Models.Context;

namespace InternalsViewer.Mcp.Interfaces.Tabs;

public interface IMcpContextSource
{
    TabContext GetContext();
}

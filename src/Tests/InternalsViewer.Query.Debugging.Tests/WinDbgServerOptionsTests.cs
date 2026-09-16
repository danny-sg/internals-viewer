using InternalsViewer.Query.Debugging;

namespace InternalsViewer.Query.Debugging.Tests;

public class WinDbgServerOptionsTests
{
    [Fact]
    public void Client_Server_And_Launch_Forms_Agree_On_Pipe_And_Password()
    {
        var options = new WinDbgServerOptions("InternalsViewer", "iv");

        Assert.Equal("npipe:Pipe=InternalsViewer,Server=localhost,Password=iv", options.RemoteOptions);
        Assert.Equal(".server npipe:pipe=InternalsViewer,password=iv", options.ServerCommand);
        Assert.Equal("-server npipe:pipe=InternalsViewer,password=iv", options.LaunchSwitch);
    }

    [Fact]
    public void Password_Is_Left_Out_When_Blank()
    {
        Assert.Equal("npipe:Pipe=InternalsViewer,Server=localhost", new WinDbgServerOptions("InternalsViewer", null).RemoteOptions);
        Assert.Equal(".server npipe:pipe=InternalsViewer", new WinDbgServerOptions("InternalsViewer", " ").ServerCommand);
    }
}

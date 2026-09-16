using InternalsViewer.Query.Debugging.Exceptions;
using InternalsViewer.Query.Debugging.Interfaces;

namespace InternalsViewer.Query.Debugging.Tests;

public class DebuggerHostTests
{
    [Fact]
    public async Task Connect_Then_Send_Are_Answered_In_Order_And_Reach_The_Session()
    {
        var session = new FakeSession();

        var output = new StringWriter();

        var input = new StringReader("connect\tengine.dll\tnpipe:Pipe=X\nsend\tbp sqlmin!Foo::Bar\nsend\tg\nquit\n");

        var exitCode = await DebuggerHost.RunAsync(input, output, (engine, remote, _) =>
        {
            session.Engine = engine;
            session.Remote = remote;

            return Task.FromResult<IDebuggerSession>(session);
        });

        Assert.Equal(0, exitCode);
        Assert.Equal(["ok", "ok", "ok"], output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("engine.dll", session.Engine);
        Assert.Equal("npipe:Pipe=X", session.Remote);
        Assert.Equal(["bp sqlmin!Foo::Bar", "g"], session.Commands);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task Engine_Failures_Are_Reported_As_Engine_Errors_And_Others_As_Session_Errors()
    {
        var output = new StringWriter();

        var input = new StringReader("connect\te\tr\nsend\tx\nbogus\n");

        await DebuggerHost.RunAsync(input, output, (_, _, _) => throw new DebuggerException("no engine\r\nhere\ttoo", isEngineFailure: true));

        string[] expected =
        [
            "error\tengine\tno engine here too",
            "error\tsession\tNo debugger session is connected",
            "error\tsession\tUnknown request bogus"
        ];

        Assert.Equal(expected, output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed class FakeSession : IDebuggerSession
    {
        public string? Engine { get; set; }

        public string? Remote { get; set; }

        public List<string> Commands { get; } = [];

        public bool Disposed { get; private set; }

        public Task SendAsync(string command, CancellationToken cancellationToken)
        {
            Commands.Add(command);

            return Task.CompletedTask;
        }

        public void Dispose() => Disposed = true;
    }
}

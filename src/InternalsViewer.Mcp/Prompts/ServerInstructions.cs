namespace InternalsViewer.Mcp.Prompts;

public static class ServerInstructions
{
    public static string Text => PromptStore.Get("server-instructions");
}
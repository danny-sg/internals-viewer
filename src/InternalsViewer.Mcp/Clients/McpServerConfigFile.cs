using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using InternalsViewer.Mcp.Clients.Enums;
using InternalsViewer.Mcp.Hosting;

namespace InternalsViewer.Mcp.Clients;

public static class McpServerConfigFile
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static McpClientState GetState(string path, string serversKey, McpHostSettings settings)
        => GetState(path, serversKey, settings, IsCurrent);

    public static McpClientState GetState(string path,
                                          string serversKey,
                                          McpHostSettings settings,
                                          Func<JsonObject, McpHostSettings, bool> isCurrent)
    {
        JsonObject? entry;

        try
        {
            entry = (Read(path)?[serversKey] as JsonObject)?[McpHostSettings.ServerName] as JsonObject;
        }
        catch (JsonException exception)
        {
            return new McpClientState(McpClientStatus.NotRegistered, $"{path} could not be read: {exception.Message}");
        }

        if (entry is null)
        {
            return new McpClientState(McpClientStatus.NotRegistered, "Not registered");
        }

        return isCurrent(entry, settings)
            ? new McpClientState(McpClientStatus.Registered, $"Registered in {path}")
            : new McpClientState(McpClientStatus.NeedsUpdate, "Registered with a previous address or token");
    }

    public static bool IsCurrent(JsonObject entry, McpHostSettings settings)
    {
        var url = entry["url"]?.GetValue<string>();

        var authorization = entry["headers"]?["Authorization"]?.GetValue<string>();

        return url == settings.Endpoint && authorization == settings.AuthorizationHeader;
    }

    public static void Update(string path, string serversKey, Action<JsonObject> update)
    {
        var root = Read(path) ?? new JsonObject();

        if (root[serversKey] is not JsonObject servers)
        {
            servers = new JsonObject();

            root[serversKey] = servers;
        }

        update(servers);

        var directory = Path.GetDirectoryName(path);

        if (directory is { Length: > 0 })
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, root.ToJsonString(WriteOptions) + Environment.NewLine);
    }

    public static JsonObject CreateEntry(McpHostSettings settings, string? type = null)
    {
        var entry = new JsonObject();

        if (type is not null)
        {
            entry["type"] = type;
        }

        entry["url"] = settings.Endpoint;

        entry["headers"] = new JsonObject { ["Authorization"] = settings.AuthorizationHeader };

        return entry;
    }

    private static JsonObject? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var text = File.ReadAllText(path);

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        return JsonNode.Parse(text, documentOptions: options) as JsonObject;
    }
}

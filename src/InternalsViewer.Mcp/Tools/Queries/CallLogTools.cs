using System.ComponentModel;
using System.Text;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Queries;

[McpServerToolType]
public sealed class CallLogTools(McpDocumentResolver documents)
{
    private const string DocumentDescription = "Query tab id from list_tabs; the active Query tab when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "query_calls", ReadOnly = true)]
    [Description("Full Trace call log: the functions logged with their call counts, or the calls of one function with their " +
                 "arguments numbered from 1 with their types, the return value and the pages and rows they touched")]
    public async Task<string> Calls([Description("Regex to filter the function list")] string? grep = null,
                                    [Description("Regex naming the function whose calls to dump; omitted lists the logged functions")]
                                    string? function = null,
                                    [Description("Hex instance (this) address to narrow the dump to one object")] string? instance = null,
                                    [Description("Only calls on this thread")] uint? thread = null,
                                    [Description("Only calls with this value in a register or the return, decimal or 0x hex")]
                                    string? value = null,
                                    [Description("First call to show")] int offset = 0,
                                    [Description("Calls to show")] int limit = 50,
                                    [Description("Only calls starting at or after this position, hex Sequence:Steps")] string? from = null,
                                    [Description("Only calls starting at or before this position, hex Sequence:Steps")] string? to = null,
                                    [Description(DocumentDescription)] string? document = null,
                                    CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var store = scope.RequireTrace().CallStore;

        var context = scope.Context;

        var fromPosition = from is null ? (TracePosition?)null : Parse.Position(from);

        var toPosition = to is null ? (TracePosition?)null : Parse.Position(to);

        var builder = new StringBuilder();

        if (function is null)
        {
            WriteFunctions(builder, context, store, grep, limit);

            return builder.ToString();
        }

        var pattern = Parse.Pattern(function);

        var keys = store.Functions.Keys.Where(k => pattern.IsMatch(context.GetFunctionName(k.FunctionAddress, k.InstanceAddress))).ToList();

        if (instance is not null)
        {
            var wanted = Parse.Hex(instance);

            keys = [.. keys.Where(k => k.InstanceAddress == wanted)];
        }

        if (keys.Count == 0)
        {
            return "no function in the call log matches";
        }

        var wantedValue = value is null ? (ulong?)null : Parse.Integer(value);

        foreach (var key in keys)
        {
            var list = store.GetCallList(key.FunctionAddress, key.InstanceAddress);

            if (list is null)
            {
                continue;
            }

            var name = context.GetFunctionName(key.FunctionAddress, key.InstanceAddress);

            var signature = await ResolveSignatureAsync(scope, context, key.FunctionAddress, key.InstanceAddress);

            builder.AppendLine();

            builder.AppendLine($"{name} address {Format.Hex(key.FunctionAddress)} instance {Format.Hex(key.InstanceAddress)}: " +
                               $"{Format.Number(list.Count)} calls");

            if (signature is { Text.Length: > 0 })
            {
                builder.AppendLine($"signature: {signature.Text}");
            }

            WriteCalls(builder, context, store, list, signature?.Layout, thread, wantedValue, fromPosition, toPosition, offset, limit);
        }

        return builder.ToString();
    }

    [McpServerTool(Name = "query_references", ReadOnly = true)]
    [Description("Full Trace references to a value, usually an object address, as the Call Tree's Find shows them: the value's " +
                 "type and every function it was called on, passed to or returned by, with the calls of each and the first and " +
                 "last of them. query_calls with function and value then lists the calls of one function")]
    public async Task<string> References([Description("The value, usually a memory address, decimal or 0x hex")] string value,
                                         [Description("Functions to list in each group")] int limit = 20,
                                         [Description(DocumentDescription)] string? document = null,
                                         CancellationToken cancellationToken = default)
    {
        var wanted = Parse.Integer(value);

        var scope = await Documents.ResolveAsync(document, cancellationToken);

        scope.RequireOperatorFrames();

        scope.RequireTrace();

        TraceValueReferences? references = null;

        await scope.RunAsync(async c => references = await c.FindReferencesAsync(wanted), cancellationToken);

        if (references is null)
        {
            throw new McpException($"Document {scope.Query.TabId} has no Full Trace call log loaded.");
        }

        var builder = new StringBuilder();

        builder.AppendLine(references.Type is { } type ? $"{Format.Hex(wanted)}: {type}" : Format.Hex(wanted));

        if (references.Groups.Count == 0)
        {
            builder.AppendLine("No logged call was passed this value or returned it.");

            return builder.ToString();
        }

        foreach (var group in references.Groups)
        {
            builder.AppendLine();

            var functions = group.Uses.Count == 1 ? "function" : "functions";

            var calls = group.Calls == 1 ? "call" : "calls";

            builder.AppendLine($"{GetGroupTitle(group.Role)}: {Format.Number(group.Uses.Count)} {functions}, " +
                               $"{Format.Number(group.Calls)} {calls}");

            var table = new TextTable("Function", "Location", "Calls", "First", "Last");

            foreach (var use in group.Uses.Take(limit))
            {
                table.Add(scope.Context.GetFunctionName(use.FunctionAddress, use.InstanceAddress),
                          use.Location,
                          Format.Number(use.Calls),
                          $"#{use.First.CallIndex} {Format.Position(use.First.Position)}",
                          $"#{use.Last.CallIndex} {Format.Position(use.Last.Position)}");
            }

            table.Write(builder);

            builder.AppendIfNotEmpty(Format.More(group.Uses.Count, limit, "raise limit"));
        }

        return builder.ToString();
    }

    private static string GetGroupTitle(TraceValueRole role) => role switch
    {
        TraceValueRole.CalledOn => "Called by",
        TraceValueRole.Returned => "Returned by",
        _ => "Referenced by"
    };

    private static async Task<McpFunctionSignature?> ResolveSignatureAsync(McpDocumentScope scope,
                                                                          DocumentContext context,
                                                                          ulong functionAddress,
                                                                          ulong instanceAddress)
    {
        if (context.GetFrame(functionAddress, instanceAddress) is not { } frame)
        {
            return null;
        }

        McpFunctionSignature? signature = null;

        try
        {
            await scope.RunAsync(async c => signature = await c.ResolveSignatureAsync(frame), CancellationToken.None);
        }
        catch (ModelContextProtocol.McpException)
        {
        }

        return signature;
    }

    private static void WriteFunctions(StringBuilder builder, DocumentContext context, TraceCallStore store, string? grep, int limit)
    {
        var pattern = grep is null ? null : Parse.Pattern(grep);

        var rows = new List<(string Name, ulong Address, ulong Instance, int Calls, int Locators)>();

        foreach (var key in store.Functions.Keys)
        {
            var name = context.GetFunctionName(key.FunctionAddress, key.InstanceAddress);

            if (pattern is not null && !pattern.IsMatch(name))
            {
                continue;
            }

            var calls = store.GetCallList(key.FunctionAddress, key.InstanceAddress)?.Count ?? 0;

            var locators = store.Locators.TryGetValue(key, out var table) ? table.Count : 0;

            rows.Add((name, key.FunctionAddress, key.InstanceAddress, calls, locators));
        }

        rows.Sort((left, right) => right.Calls.CompareTo(left.Calls));

        builder.AppendLine($"{store.FunctionCount} (function, instance) call lists in the call log, " +
                           $"{Format.Number(rows.Sum(r => (long)r.Calls))} calls in the {rows.Count} listed");

        var text = new TextTable("Function", "Address", "Instance", "Calls", "Locators");

        foreach (var row in rows.Take(limit))
        {
            var instance = row.Instance != 0 ? Format.Hex(row.Instance) : string.Empty;

            text.Add(row.Name, Format.Hex(row.Address), instance, row.Calls, row.Locators);
        }

        text.Write(builder);
    }

    private static void WriteCalls(StringBuilder builder,
                                   DocumentContext context,
                                   TraceCallStore store,
                                   TraceCallList list,
                                   ArgumentLayout? layout,
                                   uint? thread,
                                   ulong? wantedValue,
                                   TracePosition? fromPosition,
                                   TracePosition? toPosition,
                                   int offset,
                                   int limit)
    {
        var shown = 0;

        for (var index = 0; index < list.Count; index++)
        {
            var call = list[index];

            if (thread is { } wantedThread && call.ThreadId != wantedThread)
            {
                continue;
            }

            if (wantedValue is { } wanted && !HasValue(call, wanted))
            {
                continue;
            }

            if ((fromPosition is { } first && call.FromPosition < first) || (toPosition is { } last && call.FromPosition > last))
            {
                continue;
            }

            if (index < offset)
            {
                continue;
            }

            var where = store.GetNode(call) is { } node ? context.GetFunctionName(node) : $"raw node {call.CallNodeIndex}";

            var positions = $"{Format.Position(call.FromPosition)} to {Format.Position(call.ToPosition)}";

            if (layout is null)
            {
                var returned = call.Returned ? $" RAX={Format.Hex(call.ReturnValue)}" : " (no return)";

                var registers = $"RCX={Format.Hex(call.Slot(0))} RDX={Format.Hex(call.Slot(1))} " +
                                $"R8={Format.Hex(call.Slot(2))} R9={Format.Hex(call.Slot(3))}";

                builder.AppendLine($"  #{index} thread {call.ThreadId} {positions} {registers}{returned}  in {where}");

                foreach (var locator in call.Locators)
                {
                    builder.AppendLine($"      {Format.Locator(locator)}");
                }
            }
            else
            {
                var returned = call.Returned ? string.Empty : " (no return)";

                builder.AppendLine($"  #{index} thread {call.ThreadId} {positions}{returned}  in {where}");

                foreach (var argument in Format.Arguments(layout, call))
                {
                    builder.AppendLine($"      {argument}");
                }
            }

            shown++;

            if (shown >= limit)
            {
                builder.AppendLine($"  (stopped after {limit} calls; use offset or raise limit)");

                break;
            }
        }
    }

    private static bool HasValue(TraceArgumentCall call, ulong wanted)
    {
        for (var slot = 0; slot < 4; slot++)
        {
            if (call.Slot(slot) == wanted)
            {
                return true;
            }
        }

        return call.ReturnValue == wanted;
    }
}

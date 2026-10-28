using System.ComponentModel;
using System.Text;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
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

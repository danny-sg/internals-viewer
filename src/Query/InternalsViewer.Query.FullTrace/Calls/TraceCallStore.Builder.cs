using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed partial class TraceCallStore
{
    internal sealed class Builder(IEnumerable<LocatorRead>? locatorReads = null)
    {
        private Dictionary<(ulong Address, ulong Instance), List<Task<Chunk>>> Pending { get; } = new();

        private Dictionary<(ulong Function, int Register, int Offset, bool IsReadAtReturn, uint Mode), LocatorRead> Reads { get; }
            = (locatorReads ?? []).DistinctBy(KeyOf).ToDictionary(KeyOf);

        private Dictionary<(ulong Function, ulong Instance, uint Call, int Register, uint IsReadAtReturn), ulong[]> ObjectKeys { get; }
            = new();

        private Dictionary<(ulong Address, ulong Instance), List<(int Call, Locator Locator, bool IsReadAtReturn)>> Found { get; }
            = new();

        private Dictionary<(ulong Function, ulong Instance, uint Call, int Register, uint IsReadAtReturn),
                           (LocatorValueType ValueType, SortedList<uint, ulong> Parts)> TextParts { get; } = new();

        private Dictionary<(ulong Function, ulong Instance, uint Call, int Register, uint IsReadAtReturn),
                           (ulong Header, ulong Details, bool HasHeader)> TypeInfoParts { get; } = new();

        private SemaphoreSlim Compressing { get; } = new(Math.Max(2, Environment.ProcessorCount));

        public void Add(ulong address, ulong instance, IntPtr columns, int calls)
        {
            var count = calls * TraceArgumentCall.ValueCount;

            var buffer = ArrayPool<long>.Shared.Rent(count);

            Marshal.Copy(columns, buffer, 0, count);

            Compressing.Wait();

            var chunk = Task.Run(() =>
            {
                try
                {
                    return Compress(MemoryMarshal.Cast<long, ulong>(buffer.AsSpan(0, count)), calls);
                }
                finally
                {
                    ArrayPool<long>.Shared.Return(buffer);

                    Compressing.Release();
                }
            });

            ChunksOf(address, instance).Add(chunk);
        }

        public void Add(ulong address, ulong instance, ReadOnlySpan<ulong> columns, int calls)
            => ChunksOf(address, instance).Add(Task.FromResult(Compress(columns, calls)));

        public void AddLocators(IntPtr values, int count)
        {
            var size = count * Marshal.SizeOf<PointerValue>();

            var buffer = ArrayPool<byte>.Shared.Rent(size);

            try
            {
                Marshal.Copy(values, buffer, 0, size);

                AddLocators(MemoryMarshal.Cast<byte, PointerValue>(buffer.AsSpan(0, size)));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void AddLocators(ReadOnlySpan<PointerValue> values)
        {
            foreach (var value in values)
            {
                var isReadAtReturn = value.IsReadAtReturn != 0;

                if (!Reads.TryGetValue((value.Function, value.Register, (int)value.Offset, isReadAtReturn, value.Mode), out var read))
                {
                    continue;
                }

                if (read.ValueType is LocatorValueType.Text or LocatorValueType.WideText)
                {
                    AddTextPart(value, read.ValueType);

                    continue;
                }

                if (read.ValueType is LocatorValueType.TypeInfo)
                {
                    AddTypeInfoPart(value);

                    continue;
                }

                var parts = LocatorReadResolver.GetParts(read.ValueType);

                if (parts == 1)
                {
                    FoundFor(value).Add(((int)value.Call, new Locator(read.Register, read.ValueType, value.Value), isReadAtReturn));

                    continue;
                }

                if (AddKeyPart(value, parts) is not { } key)
                {
                    continue;
                }

                var locator = read.ValueType == LocatorValueType.LogSequenceNumber
                    ? Locator.CreateLogSequenceNumber(read.Register, key[0], key[1])
                    : Locator.ColumnStoreObject(read.Register, key[0], key[1], key[2]);

                FoundFor(value).Add(((int)value.Call, locator, isReadAtReturn));
            }
        }

        public TraceCallStore Build(ImageText? images = null)
        {
            Task.WaitAll([.. Pending.Values.SelectMany(c => c)]);

            var texts = new Dictionary<(ulong Function, ulong Instance, uint Call, int Register, uint IsReadAtReturn), Locator>();

            foreach (var (identity, (valueType, parts)) in TextParts)
            {
                var text = TextDecoder.Decode(parts, valueType == LocatorValueType.WideText, out var isTruncated);

                texts[identity] = new Locator(identity.Register, valueType, (ulong)text.Length, isTruncated ? 1ul : 0, text);
            }

            if (images is not null)
            {
                AddImageTexts(images, texts);
            }

            foreach (var (identity, (header, details, hasHeader)) in TypeInfoParts)
            {
                if (hasHeader)
                {
                    texts[identity] = new Locator(identity.Register, LocatorValueType.TypeInfo, header, details);
                }
            }

            TypeInfoParts.Clear();

            foreach (var ((function, instance, call, _, isReadAtReturn), locator) in texts)
            {
                FoundFor(function, instance).Add(((int)call, locator, isReadAtReturn != 0));
            }

            TextParts.Clear();

            return new TraceCallStore(Pending.ToDictionary(p => p.Key, p => p.Value.Select(c => c.Result).ToArray()),
                                    Found.ToDictionary(f => f.Key, f => LocatorTable.Build(GetPreferred(f.Value))));
        }

        private void AddImageTexts(ImageText images,
                                   Dictionary<(ulong Function, ulong Instance, uint Call, int Register, uint IsReadAtReturn), Locator> texts)
        {
            var functions = Pending.Keys.ToLookup(k => k.Address);

            foreach (var read in Reads.Values.Where(IsImageText))
            {
                foreach (var (address, instance) in functions[read.Function])
                {
                    var stackValues = read.Register is >= TraceArgumentCall.RegisterCount and not LocatorReadResolver.ReturnRegister
                        ? GetStackValues(address, instance, read.Register)
                        : null;

                    var call = 0u;

                    foreach (var chunk in Pending[(address, instance)].Select(c => c.Result))
                    {
                        var columns = Decompress(chunk);

                        for (var index = 0; index < chunk.Calls; index++, call++)
                        {
                            var identity = (address, instance, call, read.Register, read.IsReadAtReturn ? 1u : 0u);

                            if (texts.TryGetValue(identity, out var existing) && existing.Detail == 0)
                            {
                                continue;
                            }

                            var pointer = GetPointer(read, columns, chunk.Calls, index, stackValues, call);

                            if (pointer is not 0
                                && images.Read(pointer + (ulong)read.Offset, read.ValueType == LocatorValueType.WideText) is { } text)
                            {
                                texts[identity] = new Locator(read.Register, read.ValueType, (ulong)text.Length, 0, text);
                            }
                        }
                    }
                }
            }
        }

        private static bool IsImageText(LocatorRead read)
            => read.ValueType is LocatorValueType.Text or LocatorValueType.WideText && read.Path is not { Length: > 0 };

        private static ulong GetPointer(LocatorRead read,
                                        ulong[] columns,
                                        int calls,
                                        int index,
                                        Dictionary<uint, ulong>? stackValues,
                                        uint call)
        {
            if (read.Register == LocatorReadResolver.ReturnRegister)
            {
                var returned = (columns[TraceArgumentCall.FlagsIndex * calls + index] & TraceArgumentCall.ReturnedFlag) != 0;

                return returned ? columns[TraceArgumentCall.ReturnValueIndex * calls + index] : 0;
            }

            if (read.Register < TraceArgumentCall.RegisterCount)
            {
                return columns[(TraceArgumentCall.IntegerOffset + read.Register) * calls + index];
            }

            return stackValues?.GetValueOrDefault(call) ?? 0;
        }

        private Dictionary<uint, ulong> GetStackValues(ulong address, ulong instance, int register)
            => FoundFor(address, instance).Where(f => f.Locator is { ValueType: LocatorValueType.Argument }
                                                      && f.Locator.Register == register)
                                          .GroupBy(f => (uint)f.Call)
                                          .ToDictionary(g => g.Key, g => g.First().Locator.Value);

        private static List<(int Call, Locator Locator)> GetPreferred(List<(int Call, Locator Locator, bool IsReadAtReturn)> found)
        {
            var returned = found.Where(f => f.IsReadAtReturn).Select(f => (f.Call, f.Locator.Register, f.Locator.ValueType)).ToHashSet();

            return [.. found.Where(f => f.IsReadAtReturn || !returned.Contains((f.Call, f.Locator.Register, f.Locator.ValueType)))
                            .Select(f => (f.Call, f.Locator))];
        }

        private static (ulong Function, int Register, int Offset, bool IsReadAtReturn, uint Mode) KeyOf(LocatorRead read)
            => (read.Function, read.Register, read.Offset, read.IsReadAtReturn, PointerRead.ModeOf(read.ValueType));

        private static Chunk Compress(ReadOnlySpan<ulong> columns, int calls)
        {
            using var compressed = new MemoryStream();

            using (var stream = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                stream.Write(MemoryMarshal.AsBytes(columns));
            }

            return new Chunk(calls, compressed.ToArray());
        }

        private List<(int Call, Locator Locator, bool IsReadAtReturn)> FoundFor(PointerValue value)
            => FoundFor(value.Function, value.Instance);

        private List<(int Call, Locator Locator, bool IsReadAtReturn)> FoundFor(ulong function, ulong instance)
        {
            if (!Found.TryGetValue((function, instance), out var found))
            {
                found = [];

                Found[(function, instance)] = found;
            }

            return found;
        }

        private void AddTextPart(PointerValue value, LocatorValueType valueType)
        {
            var identity = (value.Function, value.Instance, value.Call, value.Register, value.IsReadAtReturn);

            if (!TextParts.TryGetValue(identity, out var text))
            {
                text = (valueType, new SortedList<uint, ulong>());

                TextParts[identity] = text;
            }

            text.Parts[value.Part] = value.Value;
        }

        private void AddTypeInfoPart(PointerValue value)
        {
            var identity = (value.Function, value.Instance, value.Call, value.Register, value.IsReadAtReturn);

            var parts = TypeInfoParts.GetValueOrDefault(identity);

            TypeInfoParts[identity] = value.Offset < LocatorReadResolver.TypeInfoDetailOffset
                ? parts with { Header = value.Value, HasHeader = true }
                : parts with { Details = value.Value };
        }

        private ulong[]? AddKeyPart(PointerValue value, int parts)
        {
            var identity = (value.Function, value.Instance, value.Call, value.Register, value.IsReadAtReturn);

            if (!ObjectKeys.TryGetValue(identity, out var key))
            {
                key = new ulong[parts + 1];

                ObjectKeys[identity] = key;
            }

            var part = (int)value.Offset / sizeof(ulong);

            if (part >= parts)
            {
                return null;
            }

            key[part] = value.Value;

            key[parts] |= 1ul << part;

            if (key[parts] != (1ul << parts) - 1)
            {
                return null;
            }

            ObjectKeys.Remove(identity);

            return key;
        }

        private List<Task<Chunk>> ChunksOf(ulong address, ulong instance)
        {
            if (!Pending.TryGetValue((address, instance), out var chunks))
            {
                chunks = [];

                Pending[(address, instance)] = chunks;
            }

            return chunks;
        }
    }
}

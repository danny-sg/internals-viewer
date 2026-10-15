using System.Buffers;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Batches;

internal sealed partial class BatchTable
{
    private BatchTable(ulong function,
                       ulong instance,
                       Capture[] captures,
                       Vector[] vectors,
                       ushort[] selection,
                       ulong[] slots,
                       DeepValue[] deepValues,
                       byte[] data)
    {
        Function = function;
        Instance = instance;
        Captures = captures;
        Calls = [.. captures.Select(c => c.Call)];
        Vectors = vectors;
        Selection = selection;
        Slots = slots;
        DeepValues = deepValues;
        Data = data;
    }

    public ulong Function { get; }

    public ulong Instance { get; }

    public int Count => Captures.Length;

    private Capture[] Captures { get; }

    private int[] Calls { get; }

    private Vector[] Vectors { get; }

    private ushort[] Selection { get; }

    private ulong[] Slots { get; }

    private DeepValue[] DeepValues { get; }

    private byte[] Data { get; }

    public bool HasBatch(int call) => Array.BinarySearch(Calls, call) >= 0;

    public BatchCapture? GetBatch(int call)
    {
        var index = Array.BinarySearch(Calls, call);

        return index < 0 ? null : CreateBatch(index);
    }

    public IReadOnlyList<int> GetCalls() => Calls;

    public IReadOnlyList<BatchSummary> GetSummaries() => [.. Captures.Select(GetSummary)];

    public (ulong Batch, ulong Consumer, ulong Id) GetHandoff(int index)
        => (Captures[index].Batch, Captures[index].Consumer, Captures[index].Id);

    public int GetSelectedCount(int index) => Captures[index].SelectedCount;


    public IEnumerable<BatchCapture> GetBatches()
    {
        for (var index = 0; index < Captures.Length; index++)
        {
            yield return CreateBatch(index);
        }
    }

    public BatchCapture CreateBatch(int index)
    {
        var capture = Captures[index];

        var vectors = new BatchCaptureVector[capture.VectorCount];

        for (var ordinal = 0; ordinal < vectors.Length; ordinal++)
        {
            vectors[ordinal] = CreateVector(Vectors[capture.FirstVector + ordinal]);
        }

        return new BatchCapture(Function,
                                Instance,
                                capture.Call,
                                capture.Id,
                                capture.Iterator,
                                capture.Batch,
                                capture.Consumer,
                                capture.Flags,
                                Selection.AsSpan(capture.FirstSelected, capture.SelectedCount).ToArray(),
                                vectors);
    }

    private BatchSummary GetSummary(Capture capture)
    {
        var rowCount = 0;

        foreach (var row in Selection.AsSpan(capture.FirstSelected, capture.SelectedCount))
        {
            rowCount = Math.Max(rowCount, row + 1);
        }

        var pureCount = 0;

        foreach (var vector in Vectors.AsSpan(capture.FirstVector, capture.VectorCount))
        {
            if (vector.Flags.HasFlag(BatchVectorFlags.Pure))
            {
                pureCount++;
            }
        }

        return new BatchSummary(capture.Call,
                                capture.Iterator,
                                capture.Flags,
                                capture.SelectedCount,
                                rowCount,
                                capture.VectorCount,
                                pureCount);
    }

    private BatchCaptureVector CreateVector(Vector vector)
    {
        var deepValues = new Dictionary<int, BatchDeepValue>(vector.DeepValueCount);

        for (var index = 0; index < vector.DeepValueCount; index++)
        {
            var deep = DeepValues[vector.FirstDeepValue + index];

            deepValues[deep.Slot] = new BatchDeepValue(deep.Length, Data.AsSpan(deep.Offset, deep.Captured).ToArray());
        }

        return new BatchCaptureVector(vector.VectorId,
                                      vector.Flags,
                                      Slots.AsSpan(vector.FirstSlot, vector.SlotCount).ToArray(),
                                      deepValues);
    }

    internal readonly record struct Capture(int Call,
                                            ulong Id,
                                            ulong Iterator,
                                            ulong Batch,
                                            ulong Consumer,
                                            BatchCaptureFlags Flags,
                                            int FirstSelected,
                                            int SelectedCount,
                                            int FirstVector,
                                            int VectorCount);

    internal readonly record struct Vector(int VectorId,
                                           BatchVectorFlags Flags,
                                           int FirstSlot,
                                           int SlotCount,
                                           int FirstDeepValue,
                                           int DeepValueCount);

    internal readonly record struct DeepValue(int Slot, int Offset, int Length, int Captured);

    internal sealed class Builder(ulong function, ulong instance)
    {
        private List<Capture> Captures { get; } = [];

        private List<Vector> Vectors { get; } = [];

        private List<ushort> Selection { get; } = [];

        private List<ulong> Slots { get; } = [];

        private List<DeepValue> DeepValues { get; } = [];

        private ArrayBufferWriter<byte> Data { get; } = new();

        public void Add(CapturedBatch batch,
                        ReadOnlySpan<CapturedVector> vectors,
                        ReadOnlySpan<ushort> selection,
                        ReadOnlySpan<ulong> slots,
                        ReadOnlySpan<CapturedDeepValue> deepValues,
                        ReadOnlySpan<byte> data)
        {
            Captures.Add(new Capture((int)batch.Call,
                                     batch.Id,
                                     batch.Iterator,
                                     batch.Batch,
                                     batch.Consumer,
                                     (BatchCaptureFlags)batch.Flags,
                                     Selection.Count,
                                     (int)batch.SelectedCount,
                                     Vectors.Count,
                                     (int)batch.VectorCount));

            Selection.AddRange(selection.Slice((int)batch.FirstSelected, (int)batch.SelectedCount));

            foreach (var vector in vectors.Slice((int)batch.FirstVector, (int)batch.VectorCount))
            {
                Vectors.Add(new Vector(vector.VectorId,
                                       (BatchVectorFlags)vector.Flags,
                                       Slots.Count,
                                       (int)vector.SlotCount,
                                       DeepValues.Count,
                                       (int)vector.DeepValueCount));

                Slots.AddRange(slots.Slice((int)vector.FirstSlot, (int)vector.SlotCount));

                foreach (var deep in deepValues.Slice((int)vector.FirstDeepValue, (int)vector.DeepValueCount))
                {
                    DeepValues.Add(new DeepValue((int)deep.Slot, Data.WrittenCount, (int)deep.Length, (int)deep.Captured));

                    Data.Write(data.Slice((int)deep.Data, (int)deep.Captured));
                }
            }
        }

        public BatchTable Build()
            => new(function,
                   instance,
                   [.. Captures.OrderBy(c => c.Call)],
                   [.. Vectors],
                   [.. Selection],
                   [.. Slots],
                   [.. DeepValues],
                   [.. Data.WrittenSpan]);
    }
}

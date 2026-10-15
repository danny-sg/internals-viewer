namespace InternalsViewer.Query.FullTrace.Batches;

internal sealed partial class BatchTable
{
    internal void Write(BinaryWriter writer)
    {
        writer.Write(Captures.Length);

        foreach (var capture in Captures)
        {
            writer.Write(capture.Call);

            writer.Write(capture.Id);

            writer.Write(capture.Iterator);

            writer.Write(capture.Batch);

            writer.Write(capture.Consumer);

            writer.Write((byte)capture.Flags);

            writer.Write(capture.FirstSelected);

            writer.Write(capture.SelectedCount);

            writer.Write(capture.FirstVector);

            writer.Write(capture.VectorCount);
        }

        writer.Write(Vectors.Length);

        foreach (var vector in Vectors)
        {
            writer.Write(vector.VectorId);

            writer.Write((byte)vector.Flags);

            writer.Write(vector.FirstSlot);

            writer.Write(vector.SlotCount);

            writer.Write(vector.FirstDeepValue);

            writer.Write(vector.DeepValueCount);
        }

        writer.Write(Selection.Length);

        foreach (var row in Selection)
        {
            writer.Write(row);
        }

        writer.Write(Slots.Length);

        foreach (var slot in Slots)
        {
            writer.Write(slot);
        }

        writer.Write(DeepValues.Length);

        foreach (var deep in DeepValues)
        {
            writer.Write(deep.Slot);

            writer.Write(deep.Offset);

            writer.Write(deep.Length);

            writer.Write(deep.Captured);
        }

        writer.Write(Data.Length);

        writer.Write(Data);
    }

    internal static BatchTable Read(BinaryReader reader, ulong function, ulong instance)
    {
        var captures = new Capture[reader.ReadInt32()];

        for (var index = 0; index < captures.Length; index++)
        {
            captures[index] = new Capture(reader.ReadInt32(),
                                          reader.ReadUInt64(),
                                          reader.ReadUInt64(),
                                          reader.ReadUInt64(),
                                          reader.ReadUInt64(),
                                          (BatchCaptureFlags)reader.ReadByte(),
                                          reader.ReadInt32(),
                                          reader.ReadInt32(),
                                          reader.ReadInt32(),
                                          reader.ReadInt32());
        }

        var vectors = new Vector[reader.ReadInt32()];

        for (var index = 0; index < vectors.Length; index++)
        {
            vectors[index] = new Vector(reader.ReadInt32(),
                                        (BatchVectorFlags)reader.ReadByte(),
                                        reader.ReadInt32(),
                                        reader.ReadInt32(),
                                        reader.ReadInt32(),
                                        reader.ReadInt32());
        }

        var selection = new ushort[reader.ReadInt32()];

        for (var index = 0; index < selection.Length; index++)
        {
            selection[index] = reader.ReadUInt16();
        }

        var slots = new ulong[reader.ReadInt32()];

        for (var index = 0; index < slots.Length; index++)
        {
            slots[index] = reader.ReadUInt64();
        }

        var deepValues = new DeepValue[reader.ReadInt32()];

        for (var index = 0; index < deepValues.Length; index++)
        {
            deepValues[index] = new DeepValue(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        var data = reader.ReadBytes(reader.ReadInt32());

        return new BatchTable(function, instance, captures, vectors, selection, slots, deepValues, data);
    }
}

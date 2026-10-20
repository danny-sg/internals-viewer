using InternalsViewer.Query.FullTrace.Evaluations;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceEvaluations
{
    internal void Write(BinaryWriter writer)
    {
        writer.Write(Captures.Count);

        foreach (var capture in Captures)
        {
            writer.Write(capture.Function);

            writer.Write(capture.Call);

            writer.Write((byte)capture.Result);
        }
    }

    internal static EvaluationCapture[] ReadCaptures(BinaryReader reader)
    {
        var captures = new EvaluationCapture[reader.ReadInt32()];

        for (var index = 0; index < captures.Length; index++)
        {
            captures[index] = new EvaluationCapture(reader.ReadUInt64(), reader.ReadInt32(), (TraceEvaluationResult)reader.ReadByte());
        }

        return captures;
    }
}

using InternalsViewer.Query.FullTrace.InstructionHistory.Replay.Enums;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Replay;

public sealed class TraceRegisterState
{
    private const int RegisterCount = (int)TraceRegister.Xmm15 + 1;

    private ulong[] Low { get; } = new ulong[RegisterCount];

    private ulong[] High { get; } = new ulong[RegisterCount];

    public ulong GetValue(TraceRegister register) => Low[(int)register];

    public (ulong Low, ulong High) GetVector(TraceRegister register) => (Low[(int)register], High[(int)register]);

    public void Apply(TraceRegisterChange change)
    {
        var index = (int)change.Register;

        if ((uint)index >= RegisterCount)
        {
            return;
        }

        Low[index] = change.Low;

        High[index] = change.High;
    }
}

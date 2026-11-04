namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

internal readonly record struct ArgumentRegisterUse(int Read, int Written)
{
    public bool IsRead(int slot) => (Read & (1 << slot)) != 0;

    public bool IsWritten(int slot) => (Written & (1 << slot)) != 0;
}

namespace InternalsViewer.Query.FullTrace.Output.Enums;

internal enum OutputFunctionType : byte
{
    Evaluation,
    ValueRead,
    VariantRead,
    Send,
    Creator,
    Iterator,
    SlotWriter
}

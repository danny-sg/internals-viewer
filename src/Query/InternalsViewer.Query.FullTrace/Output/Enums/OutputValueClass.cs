namespace InternalsViewer.Query.FullTrace.Output.Enums;

internal enum OutputValueClass : byte
{
    OutputRow,
    Constant,
    Table,
    Hash,
    HashSwitch,
    HashCachedSwitch,
    BatchRedirect,
    VariantTable,
    VariantTableRow
}

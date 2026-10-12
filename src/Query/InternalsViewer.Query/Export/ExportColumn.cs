using InternalsViewer.Query.Results;

namespace InternalsViewer.Query.Export;

internal sealed record ExportColumn(int Ordinal,
                                     string Name,
                                     string ClrType,
                                     bool IsNullable,
                                     string? TypeName,
                                     string? Detail,
                                     int? Width,
                                     ResultAlignment Alignment);

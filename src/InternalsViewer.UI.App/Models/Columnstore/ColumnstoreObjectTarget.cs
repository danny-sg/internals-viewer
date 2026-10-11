using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.UI.App.Models.Columnstore;

public sealed record ColumnstoreObjectTarget(ColumnStoreObjectType ObjectType, int ColumnId, int ObjectId);

using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IBootPageLoader
{
    Task<BootPage> GetBootPage(DatabaseDetail databaseDetail);
}
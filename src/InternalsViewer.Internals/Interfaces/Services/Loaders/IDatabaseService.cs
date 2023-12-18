using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IDatabaseService
{
    Task<Database> Load(string name);
}
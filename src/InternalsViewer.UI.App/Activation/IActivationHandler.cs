using System.Threading.Tasks;

namespace InternalsViewer.UI.App.Activation;

public interface IActivationHandler
{
    bool CanHandle(object args);

    Task HandleAsync(object args);
}

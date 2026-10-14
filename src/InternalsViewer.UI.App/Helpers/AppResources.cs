namespace InternalsViewer.UI.App.Helpers;

public static class AppResources
{
    public static T? Get<T>(string key) where T : class
        => Application.Current.Resources.TryGetValue(key, out var resource) ? resource as T : null;
}

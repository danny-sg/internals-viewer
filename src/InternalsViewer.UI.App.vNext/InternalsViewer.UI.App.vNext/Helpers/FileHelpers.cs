using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.vNext.Helpers;

public static class FileHelpers
{
    public static async Task<T?> ReadFile<T>(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);

        if (File.Exists(path))
        {
            var json = await File.ReadAllTextAsync(path);

            return JsonSerializer.Deserialize<T>(json);
        }

        return default;
    }

    public static async Task SaveFile<T>(string folderPath, string fileName, T content)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var fileContent = JsonSerializer.Serialize(content);

        await File.WriteAllTextAsync(Path.Combine(folderPath, fileName), fileContent, Encoding.UTF8);
    }

    public static void DeleteFile(string folderPath, string? filename)
    {
        if (filename != null && File.Exists(Path.Combine(folderPath, filename)))
        {
            File.Delete(Path.Combine(folderPath, filename));
        }
    }
}

public static class LayoutHelpers
{
    public static T? FindParent<T>(DependencyObject? source) where T : DependencyObject
    {
        var target = source;

        while (target != null && target is not T)
        {
            target = VisualTreeHelper.GetParent(target);
        }

        return target as T;
    }
}
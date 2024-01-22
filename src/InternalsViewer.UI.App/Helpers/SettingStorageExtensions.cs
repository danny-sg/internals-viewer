//using System;
//using System.IO;
//using System.Threading.Tasks;
//using Windows.Storage.Streams;
//using Windows.Storage;
//using System.Text.Json;


//namespace InternalsViewer.UI.App.Helpers;

//public static class SettingsStorageExtensions
//{
//    private const string FileExtension = ".json";

//    public static bool IsRoamingStorageAvailable(this ApplicationData appData)
//    {
//        return appData.RoamingStorageQuota == 0;
//    }

//    public static async Task SaveAsync<T>(this StorageFolder folder, string name, T? content)
//    {
//        var file = await folder.CreateFileAsync(GetFileName(name), CreationCollisionOption.ReplaceExisting);
//        var fileContent = JsonSerializer.Serialize(content);

//        await FileIO.WriteTextAsync(file, fileContent);
//    }

//    public static async Task<T?> ReadAsync<T>(this StorageFolder folder, string name)
//    {
//        if (!File.Exists(Path.Combine(folder.Path, GetFileName(name))))
//        {
//            return default;
//        }

//        var file = await folder.GetFileAsync($"{name}.json");

//        var fileContent = await FileIO.ReadTextAsync(file);

//        return JsonSerializer.Deserialize<T>(fileContent);
//    }

//    public static Task SaveAsync<T>(this ApplicationDataContainer settings, string key, T value)
//    {
//        settings.SaveString(key, JsonSerializer.Serialize(value));

//        return Task.CompletedTask;
//    }

//    public static void SaveString(this ApplicationDataContainer settings, string key, string value)
//    {
//        settings.Values[key] = value;
//    }

//    public static Task<T?> ReadAsync<T>(this ApplicationDataContainer settings, string key)
//    {
//        if (settings.Values.TryGetValue(key, out var result))
//        {
//            return Task.FromResult(JsonSerializer.Deserialize<T>((string)result));
//        }

//        return Task.FromResult<T?>(default);
//    }

//    public static async Task<StorageFile> SaveFileAsync(this StorageFolder folder, byte[] content, string fileName, CreationCollisionOption options = CreationCollisionOption.ReplaceExisting)
//    {
//        if (content == null)
//        {
//            throw new ArgumentNullException(nameof(content));
//        }

//        if (string.IsNullOrEmpty(fileName))
//        {
//            throw new ArgumentException("File name is null or empty. Specify a valid file name", nameof(fileName));
//        }

//        var storageFile = await folder.CreateFileAsync(fileName, options);
//        await FileIO.WriteBytesAsync(storageFile, content);
//        return storageFile;
//    }

//    public static async Task<byte[]?> ReadFileAsync(this StorageFolder folder, string fileName)
//    {
//        var item = await folder.TryGetItemAsync(fileName).AsTask().ConfigureAwait(false);

//        if (item != null && item.IsOfType(StorageItemTypes.File))
//        {
//            var storageFile = await folder.GetFileAsync(fileName);

//            var content = await storageFile.ReadBytesAsync();
            
//            return content;
//        }

//        return null;
//    }

//    public static async Task<byte[]?> ReadBytesAsync(this StorageFile? file)
//    {
//        if (file != null)
//        {
//            using IRandomAccessStream stream = await file.OpenReadAsync();
//            using var reader = new DataReader(stream.GetInputStreamAt(0));
//            await reader.LoadAsync((uint)stream.Size);
//            var bytes = new byte[stream.Size];
//            reader.ReadBytes(bytes);
//            return bytes;
//        }

//        return null;
//    }

//    private static string GetFileName(string name)
//    {
//        return string.Concat(name, FileExtension);
//    }
//}
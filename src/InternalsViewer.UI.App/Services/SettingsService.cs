using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace InternalsViewer.UI.App.Services;

public class SettingsService
{
    public ILogger<SettingsService> Logger { get; }

    private readonly string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public string ApplicationDataFolder { get; }

    public string LocalSettingsFile { get; }

    private IDictionary<string, object> settings;

    private bool isInitialized;

    public SettingsService(ILogger<SettingsService> logger, IOptions<SettingsOptions> options)
    {
        Logger = logger;
        var value = options.Value;

        ApplicationDataFolder = Path.Combine(localApplicationData, value.ApplicationDataFolder);
        LocalSettingsFile = value.SettingsFile;

        settings = new Dictionary<string, object>();
    }

    public async Task InitializeAsync()
    {
        if (!isInitialized)
        {
            settings = await FileHelpers.ReadFile<IDictionary<string, object>>(ApplicationDataFolder, LocalSettingsFile)
                       ?? new Dictionary<string, object>();

            isInitialized = true;
        }
    }

    [DebuggerStepThrough]
    public async Task<T?> ReadSettingAsync<T>(string key)
    {
        object? value;
        bool isFound;

        if (RuntimeHelper.IsMsix)
        {
            isFound = ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out value);
          
        }
        else
        {
            await InitializeAsync();

            isFound = settings.TryGetValue(key, out value);

        }
        if (isFound && value != null)
        {
            try
            {
                return JsonSerializer.Deserialize<T>((string)value);
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "Error deserializing setting {Key} - {value}", key, value);

                return default;
            }
        }

        return default;
    }

    public async Task SaveSettingAsync<T>(string key, T value)
    {
        if (RuntimeHelper.IsMsix)
        {
            ApplicationData.Current.LocalSettings.Values[key] = JsonSerializer.Serialize(value);
        }
        else
        {
            await InitializeAsync();

            settings[key] = JsonSerializer.Serialize(value);

            await FileHelpers.SaveFile(ApplicationDataFolder, LocalSettingsFile, settings);
        }
    }
}
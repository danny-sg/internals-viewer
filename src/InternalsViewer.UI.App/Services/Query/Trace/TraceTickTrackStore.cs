using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.UI.App.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InternalsViewer.UI.App.Services.Query.Trace;

public sealed partial class TraceTickTrackStore : ObservableObject
{
    private const string FileName = "OperatorTracks.json";

    [ObservableProperty]
    private IReadOnlyList<TraceTrackRule> _definitions = [];

    public TraceTickTrackStore(ILogger<TraceTickTrackStore> logger, IOptions<SettingsOptions> options)
    {
        Logger = logger;

        FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                options.Value.ApplicationDataFolder,
                                FileName);

        Predefined = TraceTrackRule.GetPredefined();

        Added = Load();

        Definitions = [.. Predefined, .. Added];
    }

    public IReadOnlyList<TraceTrackRule> AddedDefinitions => Added;

    private ILogger<TraceTickTrackStore> Logger { get; }

    private string FilePath { get; }

    private IReadOnlyList<TraceTrackRule> Predefined { get; set; }

    private IReadOnlyList<TraceTrackRule> Added { get; set; }

    public bool IsAdded(TraceTrackRule definition) => Added.Contains(definition, ReferenceEqualityComparer.Instance);

    public void Add(TraceTrackRule definition) => SetAdded([.. Added, definition]);

    public void AddRange(IEnumerable<TraceTrackRule> definitions) => SetAdded([.. Added, .. definitions]);

    public void Replace(TraceTrackRule existing, TraceTrackRule definition)
    {
        if (IsAdded(existing))
        {
            SetAdded([.. Added.Select(d => ReferenceEquals(d, existing) ? definition : d)]);
        }
        else
        {
            SetPredefined([.. Predefined.Select(d => ReferenceEquals(d, existing) ? definition : d)]);
        }
    }

    public void Remove(TraceTrackRule definition)
    {
        if (IsAdded(definition))
        {
            SetAdded([.. Added.Where(d => !ReferenceEquals(d, definition))]);
        }
        else
        {
            SetPredefined([.. Predefined.Where(d => !ReferenceEquals(d, definition))]);
        }
    }

    public int RemoveTrack(string name)
    {
        var count = Added.Count(d => IsTrack(d, name));

        if (count > 0)
        {
            SetAdded([.. Added.Where(d => !IsTrack(d, name))]);
        }

        return count;
    }

    public void Clear() => SetAdded([]);

    public void Reset() => SetPredefined(TraceTrackRule.GetPredefined());

    private static bool IsTrack(TraceTrackRule definition, string name)
        => string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase);

    private void SetPredefined(IReadOnlyList<TraceTrackRule> definitions)
    {
        Predefined = definitions;

        Definitions = [.. Predefined, .. Added];
    }

    private void SetAdded(IReadOnlyList<TraceTrackRule> definitions)
    {
        Added = definitions;

        Save();

        Definitions = [.. Predefined, .. Added];
    }

    private IReadOnlyList<TraceTrackRule> Load()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(FilePath);

            return TraceTrackRule.Read(stream);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            Logger.LogError(exception, "Error reading the operator tracks from {Path}", FilePath);

            return [];
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            using var stream = File.Create(FilePath);

            TraceTrackRule.Write(stream, Added);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.LogError(exception, "Error saving the operator tracks to {Path}", FilePath);
        }
    }
}

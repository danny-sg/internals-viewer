using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Synthesizes short pitched clicks for timeline scrubbing feedback
/// </summary>
internal sealed class TimelineAudioPlayer : IDisposable
{
    // Pentatonic scale note frequencies (Hz) spanning two octaves
    private static readonly double[] PentatonicNotes =
    [
        261.63,  // C4
        293.66,  // D4
        329.63,  // E4
        392.00,  // G4
        440.00,  // A4
        523.25,  // C5
        587.33,  // D5
        659.25,  // E5
        783.99,  // G5
        880.00,  // A5
    ];

    // Index level 0 = data page (leaf) = lowest pitch; each higher level multiplied by 4/3 (perfect fourth).
    private const double IndexLevelBaseHz = 261.63;
    private const double IndexLevelRatio = 1.3333;
    private const int MaxIndexLevel = 6;

    private const int SampleRate = 44100;

    private const double AttackSeconds = 0.001;
    private const double DecaySeconds  = 0.014;

    private const float Amplitude = 0.30f;

    // Slots per frequency: each note is ~15 ms so 3 slots is enough to absorb rapid same-pitch bursts.
    private const int PerFrequencyPoolSize = 3;

    private readonly Dictionary<double, MediaPlayer[]> _playersByFrequency = new();
    private readonly Dictionary<double, int> _poolIndexByFrequency = new();

    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Returns the frequency (Hz) for the given object id, mapped to a pentatonic scale
    /// </summary>
    public static double FrequencyForObject(int objectId)
    {
        var index = Math.Abs(objectId) % PentatonicNotes.Length;

        return PentatonicNotes[index];
    }

    /// <summary>
    /// Returns the frequency (Hz) for the given B-tree page level (0 = leaf)
    /// </summary>
    public static double FrequencyForIndexLevel(int level)
    {
        var clamped = Math.Max(0, level);

        return IndexLevelBaseHz * Math.Pow(IndexLevelRatio, clamped);
    }

    public async Task EnsureInitializedAsync()
    {
        if (_disposed || _initialized)
        {
            return;
        }

        foreach (var freq in AllFrequencies())
        {
            var wavBytes = BuildWav(freq);
            var players = new MediaPlayer[PerFrequencyPoolSize];

            for (var i = 0; i < PerFrequencyPoolSize; i++)
            {
                var stream = new InMemoryRandomAccessStream();

                using (var dataWriter = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    dataWriter.WriteBytes(wavBytes);
                    await dataWriter.StoreAsync();
                }

                players[i] = new MediaPlayer
                {
                    Volume = 1.0,
                    AudioCategory = MediaPlayerAudioCategory.GameEffects,
                    Source = MediaSource.CreateFromStream(stream, "audio/wav"),
                };
            }

            _playersByFrequency[freq] = players;
            _poolIndexByFrequency[freq] = 0;
        }

        _initialized = true;
    }

    public void PlayPlink(double frequencyHz)
    {
        if (!_initialized || _disposed)
        {
            return;
        }

        if (!_playersByFrequency.TryGetValue(frequencyHz, out var players))
        {
            return;
        }

        var index = _poolIndexByFrequency[frequencyHz];

        _poolIndexByFrequency[frequencyHz] = (index + 1) % PerFrequencyPoolSize;

        var player = players[index];
        
        player.PlaybackSession.Position = TimeSpan.Zero;
        
        player.Play();
    }

    private static IEnumerable<double> AllFrequencies()
    {
        foreach (var freq in PentatonicNotes)
        {
            yield return freq;
        }

        for (var level = 0; level <= MaxIndexLevel; level++)
        {
            yield return FrequencyForIndexLevel(level);
        }
    }

    private static byte[] BuildWav(double frequencyHz)
    {
        var attackSamples = (int)(SampleRate * AttackSeconds);
        var decaySamples = (int)(SampleRate * DecaySeconds);
        var totalSamples = attackSamples + decaySamples;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        const int channels      = 1;
        const int bitsPerSample = 16;

        var byteRate   = SampleRate * channels * bitsPerSample / 8;
        var blockAlign = channels * bitsPerSample / 8;
        var dataSize   = totalSamples * blockAlign;

        // RIFF header
        writer.Write(['R', 'I', 'F', 'F']);
        writer.Write(36 + dataSize);
        writer.Write(['W', 'A', 'V', 'E']);

        // fmt chunk
        writer.Write(['f', 'm', 't', ' ']);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(SampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

        // data chunk
        writer.Write(['d', 'a', 't', 'a']);
        writer.Write(dataSize);

        for (var i = 0; i < totalSamples; i++)
        {
            float envelope;

            if (i < attackSamples)
            {
                envelope = (float)i / attackSamples;
            }
            else
            {
                var decay = (float)(i - attackSamples) / decaySamples;
                envelope = 1f - decay;
            }

            var sample = Amplitude * envelope * (float)Math.Sin(2.0 * Math.PI * frequencyHz * i / SampleRate);
          
            writer.Write((short)(sample * short.MaxValue));
        }

        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var players in _playersByFrequency.Values)
        {
            foreach (var player in players)
            {
                player.Dispose();
            }
        }
    }
}
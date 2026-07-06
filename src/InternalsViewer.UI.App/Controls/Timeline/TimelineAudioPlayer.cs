using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Synthesizes short pitched clicks for timeline scrubbing feedback.
/// Each object id maps to a distinct note on a pentatonic scale so reads from different objects
/// are audibly distinguishable. All <see cref="MediaPlayer"/> instances are pre-built at init
/// time so <see cref="PlayPlink"/> is a synchronous seek-and-play with zero allocation.
/// </summary>
internal sealed class TimelineAudioPlayer : IDisposable
{
    // Two-octave pentatonic scale. Ten distinct pitches cover the most common object-id spread.
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

    private const int SampleRate = 44100;

    private const double AttackSeconds = 0.001;
    private const double DecaySeconds  = 0.025;

    private const float Amplitude = 0.30f;

    // Three slots per pitch so rapid same-object bursts don't have to wait.
    private const int PerFrequencyPoolSize = 3;

    private readonly Dictionary<double, MediaPlayer[]> _playersByFrequency = new();
    private readonly Dictionary<double, int> _poolIndexByFrequency = new();

    private bool _initialized;
    private bool _disposed;

    /// <summary>Returns the pentatonic frequency (Hz) for the given object id.</summary>
    public static double FrequencyForObject(int objectId)
    {
        var index = Math.Abs(objectId) % PentatonicNotes.Length;

        return PentatonicNotes[index];
    }

    /// <summary>
    /// Pre-builds all <see cref="MediaPlayer"/> / <see cref="MediaSource"/> pairs for every
    /// pentatonic frequency. Safe to call multiple times; subsequent calls are no-ops.
    /// </summary>
    public async Task EnsureInitializedAsync()
    {
        if (_disposed || _initialized)
        {
            return;
        }

        foreach (var freq in PentatonicNotes)
        {
            var wavBytes = BuildWav(freq);
            var players  = new MediaPlayer[PerFrequencyPoolSize];

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

    /// <summary>
    /// Plays one click at <paramref name="frequencyHz"/> on the next available pool slot.
    /// Seeks the pre-built player back to position zero and calls <see cref="MediaPlayer.Play"/>.
    /// No allocation or async I/O occurs on the calling thread.
    /// </summary>
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

    private static byte[] BuildWav(double frequencyHz)
    {
        var attackSamples = (int)(SampleRate * AttackSeconds);
        var decaySamples  = (int)(SampleRate * DecaySeconds);
        var totalSamples  = attackSamples + decaySamples;

        using var ms     = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        const int channels      = 1;
        const int bitsPerSample = 16;

        var byteRate   = SampleRate * channels * bitsPerSample / 8;
        var blockAlign = channels * bitsPerSample / 8;
        var dataSize   = totalSamples * blockAlign;

        writer.Write(['R', 'I', 'F', 'F']);
        writer.Write(36 + dataSize);
        writer.Write(['W', 'A', 'V', 'E']);

        writer.Write(['f', 'm', 't', ' ']);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(SampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

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

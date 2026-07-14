using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <remarks>
/// Synthesizes short pitched clicks for timeline scrubbing feedback.
/// Each object id maps to a distinct note on a pentatonic scale so reads from different objects
/// are audibly distinguishable. Reads and latches are additionally hard-panned to opposite stereo
/// channels (reads left, latches right) - they're independent <see cref="MediaPlayer"/> instances, so
/// when both land on the same tick they'd otherwise mask each other in a mono mix; on separate
/// channels both remain audible. All <see cref="MediaPlayer"/> instances are pre-built at init time so
/// <see cref="PlayPlink"/> is a synchronous seek-and-play with zero allocation.
/// </remarks>
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

    // Latches reuse the same pentatonic pitch-per-object mapping as reads, but as a shorter, buzzier
    // square-wave "tick" rather than a soft sine "plink" - same note, audibly different instrument, so
    // the two event kinds can be told apart by ear even when they land close together.
    private const double LatchDecaySeconds = 0.012;

    // Hard-panned to opposite channels so a read and a latch on the same tick both come through instead
    // of one masking the other.
    private const float ReadPan = -1f;
    private const float LatchPan = 1f;

    private const float Amplitude = 0.30f;

    // Three slots per pitch so rapid same-object bursts don't have to wait.
    private const int PerFrequencyPoolSize = 3;

    private enum Waveform { Sine, Square }

    private readonly Dictionary<double, MediaPlayer[]> _playersByFrequency = new();
    private readonly Dictionary<double, int> _poolIndexByFrequency = new();

    private readonly Dictionary<double, MediaPlayer[]> _latchPlayersByFrequency = new();
    private readonly Dictionary<double, int> _latchPoolIndexByFrequency = new();

    private bool _initialized;
    private bool _disposed;

    /// <summary>Returns the pentatonic frequency (Hz) for the given object id.</summary>
    public static double FrequencyForObject(int objectId)
    {
        var index = Math.Abs(objectId) % PentatonicNotes.Length;

        return PentatonicNotes[index];
    }

    /// <summary>
    /// Pre-builds all <see cref="MediaPlayer"/> / <see cref="MediaSource"/> pairs for every pentatonic frequency
    /// </summary>
    public async Task EnsureInitializedAsync()
    {
        if (_disposed || _initialized)
        {
            return;
        }

        foreach (var freq in PentatonicNotes)
        {
            _playersByFrequency[freq] = await BuildPlayerPool(BuildWav(freq, Waveform.Sine, DecaySeconds, ReadPan));
            _poolIndexByFrequency[freq] = 0;

            _latchPlayersByFrequency[freq] = await BuildPlayerPool(BuildWav(freq, Waveform.Square, LatchDecaySeconds, LatchPan));
            _latchPoolIndexByFrequency[freq] = 0;
        }

        _initialized = true;
    }

    private static async Task<MediaPlayer[]> BuildPlayerPool(byte[] wavBytes)
    {
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

        return players;
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

    public void PlayLatchTick(double frequencyHz)
    {
        if (!_initialized || _disposed)
        {
            return;
        }

        if (!_latchPlayersByFrequency.TryGetValue(frequencyHz, out var players))
        {
            return;
        }

        var index = _latchPoolIndexByFrequency[frequencyHz];
        _latchPoolIndexByFrequency[frequencyHz] = (index + 1) % PerFrequencyPoolSize;

        var player = players[index];
        player.PlaybackSession.Position = TimeSpan.Zero;
        player.Play();
    }

    private static byte[] BuildWav(double frequencyHz, Waveform waveform, double decaySeconds, float pan)
    {
        var attackSamples = (int)(SampleRate * AttackSeconds);
        var decaySamples  = (int)(SampleRate * decaySeconds);
        var totalSamples  = attackSamples + decaySamples;

        var panAngle = (pan + 1f) * (Math.PI / 4.0);
        var leftGain = (float)Math.Cos(panAngle);
        var rightGain = (float)Math.Sin(panAngle);

        using var ms     = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        const int channels      = 2;
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

            var phase = 2.0 * Math.PI * frequencyHz * i / SampleRate;

            var wave = waveform == Waveform.Square
                ? Math.Sign(Math.Sin(phase))
                : Math.Sin(phase);

            var sample = Amplitude * envelope * (float)wave;

            writer.Write((short)(sample * leftGain * short.MaxValue));
            writer.Write((short)(sample * rightGain * short.MaxValue));
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

        foreach (var players in _latchPlayersByFrequency.Values)
        {
            foreach (var player in players)
            {
                player.Dispose();
            }
        }
    }
}

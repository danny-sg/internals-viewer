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
/// channels both remain audible. A file read is a single centre-panned sub-bass rumble: it's the
/// physical disk hit underneath a read, so it wants to be felt under the pitched plinks rather than
/// compete with them for a note. All <see cref="MediaPlayer"/> instances are pre-built at init time so
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

    // Sub-bass "brrr" for a physical file read. The long attack matters: a 1ms attack on a ~50Hz tone
    // starts mid-cycle and cracks, which reads as a click rather than a rumble.
    private const double RumbleFrequency = 45.0;
    private const double RumbleAttackSeconds = 0.012;
    private const double RumbleDecaySeconds = 0.220;

    // What turns the low tone into a "brrr" rather than a hum: chopping the amplitude at a rate low enough
    // to be heard as individual pulses (~30Hz over a 220ms tail is ~6 of them) but too fast to be heard as
    // separate hits. Broadband noise was the obvious way to add texture here and it's the wrong one - it
    // reads as hiss, because at this pitch everything audible in it is an octave-plus above the tone.
    private const double RumbleModulationHz = 30.0;
    private const double RumbleModulationDepth = 0.65;

    // Low frequencies are perceived as quieter at the same amplitude, so the rumble is mixed hotter than
    // the plinks to sit level with them by ear.
    private const float RumbleAmplitude = 0.55f;

    private const float RumblePan = 0f;

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

    // The rumble is an order of magnitude longer than a plink and has only the one voice, so it needs more
    // slots to ride out a burst of reads without cutting itself off.
    private const int RumblePoolSize = 8;

    private enum Waveform { Sine, Square, Rumble }

    private readonly Dictionary<double, MediaPlayer[]> _playersByFrequency = new();
    private readonly Dictionary<double, int> _poolIndexByFrequency = new();

    private readonly Dictionary<double, MediaPlayer[]> _latchPlayersByFrequency = new();
    private readonly Dictionary<double, int> _latchPoolIndexByFrequency = new();

    private MediaPlayer[] _rumblePlayers = [];
    private int _rumblePoolIndex;

    private Task? _initializeTask;

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
    /// <remarks>
    /// The build is cached as a task rather than guarded by the <see cref="_initialized"/> flag alone: the flag is only
    /// set once the build finishes, so concurrent callers arriving mid-build would each start their own. Callers await
    /// the one build and can show progress against it.
    /// </remarks>
    public Task EnsureInitializedAsync()
    {
        if (_disposed || _initialized)
        {
            return Task.CompletedTask;
        }

        return _initializeTask ??= InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // Synthesising ~70 WAV buffers and their MediaPlayer/stream pairs takes long enough to drop frames if it runs
        // inline. MediaPlayer is agile, so the whole build moves off the UI thread; the continuation returns there to
        // publish _initialized.
        await Task.Run(BuildVoicesAsync);

        if (_disposed)
        {
            // Disposed mid-build - Dispose() couldn't see the voices finished after it ran, so clean them up here.
            DisposePlayers();

            return;
        }

        _initialized = true;
    }

    private async Task BuildVoicesAsync()
    {
        foreach (var freq in PentatonicNotes)
        {
            _playersByFrequency[freq] = await BuildPlayerPool(BuildWav(freq, Waveform.Sine, AttackSeconds, DecaySeconds, ReadPan, Amplitude),
                                                             PerFrequencyPoolSize);
            _poolIndexByFrequency[freq] = 0;

            _latchPlayersByFrequency[freq] = await BuildPlayerPool(BuildWav(freq, Waveform.Square, AttackSeconds, LatchDecaySeconds, LatchPan, Amplitude),
                                                                   PerFrequencyPoolSize);
            _latchPoolIndexByFrequency[freq] = 0;
        }

        _rumblePlayers = await BuildPlayerPool(BuildWav(RumbleFrequency, Waveform.Rumble, RumbleAttackSeconds, RumbleDecaySeconds, RumblePan, RumbleAmplitude),
                                               RumblePoolSize);
        _rumblePoolIndex = 0;
    }

    private static async Task<MediaPlayer[]> BuildPlayerPool(byte[] wavBytes, int poolSize)
    {
        var players = new MediaPlayer[poolSize];

        for (var i = 0; i < poolSize; i++)
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

    /// <summary>Plays the sub-bass rumble for a physical file read</summary>
    public void PlayFileRumble()
    {
        if (!_initialized || _disposed || _rumblePlayers.Length == 0)
        {
            return;
        }

        var index = _rumblePoolIndex;
        _rumblePoolIndex = (index + 1) % _rumblePlayers.Length;

        var player = _rumblePlayers[index];
        player.PlaybackSession.Position = TimeSpan.Zero;
        player.Play();
    }

    /// <summary>
    /// A motor-like "brrr" - a harmonic stack chopped by a low-frequency tremolo
    /// </summary>
    /// <remarks>
    /// The harmonics are what carry it on small speakers, which roll off hard below ~80Hz and would otherwise leave the
    /// fundamental inaudible rather than merely quiet.
    /// </remarks>
    private static double RumbleSample(double phase, int sampleIndex)
    {
        var tone = 0.55 * Math.Sin(phase)
                   + 0.30 * Math.Sin(2.0 * phase)
                   + 0.15 * Math.Sin(3.0 * phase);

        var modulationPhase = 2.0 * Math.PI * RumbleModulationHz * sampleIndex / SampleRate;

        var gate = 0.5 * (1.0 - Math.Cos(modulationPhase));

        return tone * (1.0 - RumbleModulationDepth * gate);
    }

    private static byte[] BuildWav(double frequencyHz, Waveform waveform, double attackSeconds, double decaySeconds, float pan, float amplitude)
    {
        var attackSamples = (int)(SampleRate * attackSeconds);
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

                // The rumble tails off on a curve rather than a ramp; a linear fade over 220ms is heard as a
                // tone being turned down, not as something being struck.
                envelope = waveform == Waveform.Rumble ? (1f - decay) * (1f - decay) : 1f - decay;
            }

            var phase = 2.0 * Math.PI * frequencyHz * i / SampleRate;

            var wave = waveform switch
            {
                Waveform.Square => Math.Sign(Math.Sin(phase)),
                Waveform.Rumble => RumbleSample(phase, i),
                _ => Math.Sin(phase),
            };

            var sample = amplitude * envelope * (float)wave;

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

        DisposePlayers();
    }

    private void DisposePlayers()
    {
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

        foreach (var player in _rumblePlayers)
        {
            player.Dispose();
        }
    }
}

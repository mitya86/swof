using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;
using Swof.Engine;

namespace Swof.Game;

internal sealed class MatchFeedbackPlayer : IDisposable
{
    private const int SampleRate = 22050;

    private readonly SoundEffect _pass;
    private readonly SoundEffect _shot;
    private readonly SoundEffect _control;
    private readonly SoundEffect _catch;
    private long _lastObservedSequence;
    private long _lastObservedTick;
    private float _vibrationSeconds;
    private float _vibrationStrength;

    public MatchFeedbackPlayer()
    {
        _pass = CreateImpactSound(62, 145f, 0.28f, 0.16f);
        _shot = CreateImpactSound(92, 92f, 0.52f, 0.3f);
        _control = CreateImpactSound(42, 210f, 0.16f, 0.08f);
        _catch = CreateImpactSound(82, 72f, 0.4f, 0.22f);
    }

    public void Update(
        MatchSnapshot snapshot,
        TeamSide localTeam,
        float intensity,
        float elapsedSeconds,
        bool gamePadConnected)
    {
        intensity = Math.Clamp(intensity, 0f, 1f);
        _vibrationSeconds = Math.Max(0f, _vibrationSeconds - Math.Clamp(elapsedSeconds, 0f, 0.05f));
        if (_vibrationSeconds <= 0f)
        {
            _vibrationStrength = 0f;
        }

        // A new match numbers its feedback from the start again.
        if (snapshot.Tick < _lastObservedTick)
        {
            _lastObservedSequence = 0;
        }

        _lastObservedTick = snapshot.Tick;
        foreach (var feedbackEvent in snapshot.FeedbackEvents)
        {
            if (feedbackEvent.Sequence <= _lastObservedSequence)
            {
                continue;
            }

            _lastObservedSequence = feedbackEvent.Sequence;
            if (intensity <= 0f || !GameplayFeedbackRouting.IsRelevantToTeam(feedbackEvent, localTeam))
            {
                continue;
            }

            Play(feedbackEvent, intensity);
        }

        if (!gamePadConnected || intensity <= 0f || _vibrationSeconds <= 0f)
        {
            GamePad.SetVibration(PlayerIndex.One, 0f, 0f);
            return;
        }

        var fade = Math.Clamp(_vibrationSeconds / 0.1f, 0f, 1f);
        GamePad.SetVibration(PlayerIndex.One, _vibrationStrength * fade * intensity, _vibrationStrength * 0.45f * fade * intensity);
    }

    public void Dispose()
    {
        GamePad.SetVibration(PlayerIndex.One, 0f, 0f);
        _catch.Dispose();
        _control.Dispose();
        _shot.Dispose();
        _pass.Dispose();
    }

    private void Play(GameplayFeedbackEvent feedbackEvent, float intensity)
    {
        var eventIntensity = Math.Clamp(feedbackEvent.Intensity * intensity, 0f, 1f);
        switch (feedbackEvent.Type)
        {
            case GameplayFeedbackType.PassContact:
                _pass.Play(0.28f + eventIntensity * 0.22f, -0.08f, GameplayFeedbackRouting.GetStereoPan(feedbackEvent));
                SetVibration(0.045f, 0.16f + eventIntensity * 0.08f);
                break;
            case GameplayFeedbackType.ShotContact:
                _shot.Play(0.38f + eventIntensity * 0.3f, -0.12f, GameplayFeedbackRouting.GetStereoPan(feedbackEvent));
                SetVibration(0.09f, 0.34f + eventIntensity * 0.18f);
                break;
            case GameplayFeedbackType.PossessionChanged:
                _control.Play(0.12f + eventIntensity * 0.12f, 0.08f, 0f);
                break;
            case GameplayFeedbackType.GoalkeeperCatch:
                _catch.Play(0.34f + eventIntensity * 0.22f, -0.15f, 0f);
                SetVibration(0.075f, 0.28f);
                break;
        }
    }

    private void SetVibration(float durationSeconds, float strength)
    {
        _vibrationSeconds = Math.Max(_vibrationSeconds, durationSeconds);
        _vibrationStrength = Math.Max(_vibrationStrength, strength);
    }

    private static SoundEffect CreateImpactSound(int durationMilliseconds, float frequency, float toneAmount, float noiseAmount)
    {
        var sampleCount = Math.Max(1, SampleRate * durationMilliseconds / 1000);
        var buffer = new byte[sampleCount * sizeof(short)];
        uint noiseState = 0x9E3779B9u;
        for (var i = 0; i < sampleCount; i++)
        {
            var time = i / (float)SampleRate;
            var progress = i / (float)sampleCount;
            var envelope = MathF.Pow(1f - progress, 2.4f);
            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            var noise = ((noiseState & 0xffff) / 32767.5f - 1f) * noiseAmount;
            var tone = MathF.Sin(MathHelper.TwoPi * frequency * time) * toneAmount;
            var click = i < SampleRate / 500 ? (1f - i / (SampleRate / 500f)) * 0.35f : 0f;
            var sample = (short)(Math.Clamp((tone + noise + click) * envelope, -1f, 1f) * short.MaxValue);
            buffer[i * 2] = (byte)(sample & 0xff);
            buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xff);
        }

        return new SoundEffect(buffer, SampleRate, AudioChannels.Mono);
    }
}

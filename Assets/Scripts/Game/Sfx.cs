using UnityEngine;

/// <summary>
/// Tiny synthesized sound effects (no audio files needed).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class Sfx : MonoBehaviour
{
    public static Sfx Instance { get; private set; }

    [Range(0f, 1f)] public float volume = 0.5f;

    [Tooltip("Squeak pitch per player: duck higher, frog lower")]
    public float[] squeakPitch = { 1.15f, 0.8f };

    AudioSource source;
    AudioSource[] squeakSources; // one per player so their pitches don't interfere
    AudioClip beepClip, goClip, tagClip, winClip, squeakClip;

    void Awake()
    {
        Instance = this;
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        beepClip = Tones("beep", 0.15f, 660f);
        goClip = Tones("go", 0.35f, 990f);
        winClip = Tones("win", 0.12f, 523f, 659f, 784f, 1047f);
        squeakClip = Squeak("squeak", 1);
        tagClip = Squeak("tagSqueak", 2); // "squee-squee"

        squeakSources = new AudioSource[squeakPitch.Length];
        for (int i = 0; i < squeakSources.Length; i++)
        {
            squeakSources[i] = gameObject.AddComponent<AudioSource>();
            squeakSources[i].playOnAwake = false;
            squeakSources[i].spatialBlend = 0f;
        }
    }

    public void PlayBeep() => Play(beepClip);
    public void PlayGo() => Play(goClip);
    public void PlayTag() => Play(tagClip);
    public void PlayWin() => Play(winClip);

    /// <summary>Rubber-toy squeak in this player's voice, slightly varied each time.</summary>
    public void PlaySqueak(int playerIndex)
    {
        if (squeakSources == null || playerIndex < 0 || playerIndex >= squeakSources.Length) return;
        var s = squeakSources[playerIndex];
        s.pitch = squeakPitch[playerIndex] * Random.Range(0.93f, 1.07f);
        s.PlayOneShot(squeakClip, volume);
    }

    void Play(AudioClip clip)
    {
        if (clip != null) source.PlayOneShot(clip, volume);
    }

    // rubber-duck squeak: a quick upward pitch sweep with wobble, squeezed `count` times
    static AudioClip Squeak(string name, int count)
    {
        const int rate = 44100;
        const float len = 0.17f, gap = 0.05f;
        int perSqueak = (int)(rate * len), perGap = (int)(rate * gap);
        var data = new float[count * perSqueak + (count - 1) * perGap];
        for (int n = 0; n < count; n++)
        {
            double phase = 0;
            int start = n * (perSqueak + perGap);
            for (int i = 0; i < perSqueak; i++)
            {
                float t = (float)i / perSqueak; // 0..1 across the squeak
                float freq = 1000f + 900f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f)) + 60f * Mathf.Sin(2f * Mathf.PI * 35f * t * len);
                phase += 2.0 * Mathf.PI * freq / rate;
                float s = Mathf.Sin((float)phase);
                float env = Mathf.Min(1f, t / 0.05f) * Mathf.Clamp01((1f - t) / 0.3f);
                // a bit of square-wave edge makes it sound rubbery
                data[start + i] = (0.7f * s + 0.3f * Mathf.Sign(s)) * env * 0.5f;
            }
        }
        var clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // a sequence of short sine notes with a quick fade in/out on each
    static AudioClip Tones(string name, float noteLength, params float[] freqs)
    {
        const int rate = 44100;
        int perNote = (int)(rate * noteLength);
        var data = new float[perNote * freqs.Length];
        for (int n = 0; n < freqs.Length; n++)
        {
            for (int i = 0; i < perNote; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Min(1f, i / (rate * 0.005f)) * Mathf.Clamp01((perNote - i) / (rate * 0.04f));
                // sine + a little square-ish harmonic for a toy-like sound
                float s = Mathf.Sin(2f * Mathf.PI * freqs[n] * t);
                data[n * perNote + i] = (0.8f * s + 0.2f * Mathf.Sign(s)) * env * 0.6f;
            }
        }
        var clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

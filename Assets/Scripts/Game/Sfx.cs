using UnityEngine;

/// <summary>
/// Tiny synthesized sound effects (no audio files needed).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class Sfx : MonoBehaviour
{
    public static Sfx Instance { get; private set; }

    [Range(0f, 1f)] public float volume = 0.5f;

    AudioSource source;
    AudioClip beepClip, goClip, tagClip, winClip;

    void Awake()
    {
        Instance = this;
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        beepClip = Tones("beep", 0.15f, 660f);
        goClip = Tones("go", 0.35f, 990f);
        tagClip = Tones("tag", 0.12f, 880f, 440f);
        winClip = Tones("win", 0.12f, 523f, 659f, 784f, 1047f);
    }

    public void PlayBeep() => Play(beepClip);
    public void PlayGo() => Play(goClip);
    public void PlayTag() => Play(tagClip);
    public void PlayWin() => Play(winClip);

    void Play(AudioClip clip)
    {
        if (clip != null) source.PlayOneShot(clip, volume);
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

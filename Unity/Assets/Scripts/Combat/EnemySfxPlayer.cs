using UnityEngine;
using Random = UnityEngine.Random;

public class EnemySfxPlayer : MonoBehaviour
{
    [Header("Clips")]
    public AudioClip[] hitClips;
    public AudioClip[] deathClips;

    [Header("Pitch Randomization")]
    [Tooltip("A random pitch in this range is picked for each sound.")]
    public Vector2 pitchRange = new Vector2(0.9f, 1.1f);

    [Header("Volume")]
    [Range(0f, 1f)] public float hitVolume = 1f;
    [Range(0f, 1f)] public float deathVolume = 1f;

    [Header("Optional Source Template")]
    [Tooltip("If assigned, one-shot sources copy 3D and mixer settings from this AudioSource.")]
    public AudioSource sourceTemplate;

    private void Awake()
    {
        if (sourceTemplate == null)
            sourceTemplate = GetComponent<AudioSource>();
    }

    public void PlayRandomHitSound()
    {
        PlayRandomFromPool(hitClips, hitVolume);
    }

    public void PlayRandomDeathSound()
    {
        PlayRandomFromPool(deathClips, deathVolume);
    }

    private void PlayRandomFromPool(AudioClip[] clipPool, float volume)
    {
        if (clipPool == null || clipPool.Length == 0) return;

        AudioClip clip = clipPool[Random.Range(0, clipPool.Length)];
        if (clip == null) return;

        float minPitch = Mathf.Min(pitchRange.x, pitchRange.y);
        float maxPitch = Mathf.Max(pitchRange.x, pitchRange.y);
        float pitch = Random.Range(minPitch, maxPitch);

        PlayOneShotAtPosition(clip, pitch, Mathf.Clamp01(volume));
    }

    private void PlayOneShotAtPosition(AudioClip clip, float pitch, float volume)
    {
        GameObject tempAudioObject = new GameObject("EnemySfxOneShot");
        tempAudioObject.transform.position = transform.position;

        AudioSource tempSource = tempAudioObject.AddComponent<AudioSource>();
        ApplyTemplateSettings(tempSource);

        tempSource.clip = clip;
        tempSource.pitch = pitch;
        tempSource.volume = sourceTemplate != null ? sourceTemplate.volume * volume : volume;
        tempSource.Play();

        float clipDuration = clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
        Destroy(tempAudioObject, clipDuration + 0.05f);
    }

    private void ApplyTemplateSettings(AudioSource target)
    {
        if (sourceTemplate == null)
        {
            target.spatialBlend = 1f;
            target.minDistance = 1f;
            target.maxDistance = 20f;
            target.rolloffMode = AudioRolloffMode.Logarithmic;
            return;
        }

        target.outputAudioMixerGroup = sourceTemplate.outputAudioMixerGroup;
        target.spatialBlend = sourceTemplate.spatialBlend;
        target.minDistance = sourceTemplate.minDistance;
        target.maxDistance = sourceTemplate.maxDistance;
        target.rolloffMode = sourceTemplate.rolloffMode;
        target.dopplerLevel = sourceTemplate.dopplerLevel;
        target.spread = sourceTemplate.spread;
        target.priority = sourceTemplate.priority;
        target.reverbZoneMix = sourceTemplate.reverbZoneMix;
        target.ignoreListenerPause = sourceTemplate.ignoreListenerPause;
        target.ignoreListenerVolume = sourceTemplate.ignoreListenerVolume;
    }
}

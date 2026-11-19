using UnityEngine;

public static class AudioVolumeSettings
{
    private const string VolumePreferenceKey = "AudioVolume";

    public static float CurrentVolume => AudioListener.volume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedVolume()
    {
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreferenceKey, 1f));
        Application.quitting -= SavePreferences;
        Application.quitting += SavePreferences;
    }

    public static void SetVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        AudioListener.volume = clampedVolume;
        PlayerPrefs.SetFloat(VolumePreferenceKey, clampedVolume);
    }

    private static void SavePreferences()
    {
        PlayerPrefs.Save();
    }
}

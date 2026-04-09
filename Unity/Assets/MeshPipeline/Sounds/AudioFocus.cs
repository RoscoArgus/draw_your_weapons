using UnityEngine;

public class AudioFocus : MonoBehaviour
{
    /// <summary>
    /// Mutes or restores global audio when app focus changes
    /// </summary>
    /// <param name="hasFocus">True when the application window in focus, false otherwise</param>
    private void OnApplicationFocus(bool hasFocus)
    {
        AudioListener.pause = !hasFocus;
        AudioListener.volume = hasFocus ? 1f : 0f;
    }

    /// <summary>
    /// Mutes or restores global audio when the app is paused or resumed
    /// </summary>
    /// <param name="isPaused">True while the application is paused, false otherwise</param>
    private void OnApplicationPause(bool isPaused)
    {
        AudioListener.pause = isPaused;
        AudioListener.volume = isPaused ? 0f : 1f;
    }
}

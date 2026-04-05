using UnityEngine;

public class AudioFocus : MonoBehaviour
{
    private void OnApplicationFocus(bool hasFocus)
    {
        AudioListener.pause = !hasFocus;
        AudioListener.volume = hasFocus ? 1f : 0f;
    }

    private void OnApplicationPause(bool isPaused)
    {
        AudioListener.pause = isPaused;
        AudioListener.volume = isPaused ? 0f : 1f;
    }
}
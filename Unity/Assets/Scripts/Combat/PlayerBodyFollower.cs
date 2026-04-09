using UnityEngine;

public class PlayerBodyFollower : MonoBehaviour
{
    public Transform headCamera;
    public float bodyHeight = 1.0f;

    /// <summary>
    /// Aligns the body transform with the headset position
    /// </summary>
    private void LateUpdate()
    {
        if (headCamera == null)
        {
            return;
        }
        transform.position = new Vector3(headCamera.position.x, bodyHeight, headCamera.position.z);
    }
}

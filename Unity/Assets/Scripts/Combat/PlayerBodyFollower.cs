using UnityEngine;

public class PlayerBodyFollower : MonoBehaviour
{
    public Transform headCamera;
    public float bodyHeight = 1.0f;

    private void LateUpdate()
    {
        if (headCamera == null) return;

        Vector3 pos = headCamera.position;
        pos.y = bodyHeight;
        transform.position = pos;
    }
}
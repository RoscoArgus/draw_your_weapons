using UnityEngine;

public class EnemyMover : MonoBehaviour
{
    public Transform target;
    public float moveSpeed = 1.5f;
    public float stopDistance = 1.25f;
    public float rotationSpeed = 8f;

    private EnemyHealth health;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    private void Update()
    {
        if (target == null) return;
        if (health != null && health.IsDead) return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        float distance = direction.magnitude;
        if (distance <= stopDistance) return;

        Vector3 moveDir = direction.normalized;
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        if (moveDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
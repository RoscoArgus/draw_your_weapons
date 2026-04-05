using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyMover : MonoBehaviour
{
    [Header("Movement")]
    public Transform target;
    public float moveSpeed = 1.5f;
    public float stopDistance = 1.25f;
    public float rotationSpeed = 8f;
    public float separationRadius = 0.6f;
    public float separationStrength = 1.5f;

    [Header("Sway Animation")]
    public float swayAmount = 0.05f;
    public float swaySpeed = 2f;
    public float bobAmount = 0.03f;
    public float bobSpeed = 4f;

    [Header("Hit Animation")]
    public float hitRotationAmount = 45f;
    public float hitRecoverySpeed = 8f;

    private EnemyHealth _health;
    private Transform _meshTransform;
    private Vector3 _meshStartLocalPos;
    private float _swayOffset;
    private bool _isMoving;

    private float _hitRotation;
    private bool _isHit;

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        if (transform.childCount > 0)
        {
            _meshTransform = transform.GetChild(0);
            _meshStartLocalPos = _meshTransform.localPosition;
        }
        else
        {
            _meshTransform = transform;
            _meshStartLocalPos = transform.localPosition;
        }

        _swayOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    // Call this from EnemyHealth when hit
    public void TriggerHitAnimation()
    {
        _hitRotation = hitRotationAmount;
        _isHit = true;
    }

    private void Update()
    {
        if (target == null) return;
        if (_health != null && _health.IsDead) return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        float distance = direction.magnitude;
        Vector3 moveDir = direction.normalized;

        _isMoving = distance > stopDistance && !IsPathBlocked(moveDir);

        if (_isMoving)
            transform.position += moveDir * moveSpeed * Time.deltaTime;

        // Separation
        Collider[] nearby = Physics.OverlapSphere(transform.position, separationRadius);
        foreach (var col in nearby)
        {
            if (col.gameObject == gameObject) continue;
            if (col.GetComponent<EnemyHealth>() == null) continue;

            Vector3 away = transform.position - col.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) continue;
            transform.position += away.normalized * separationStrength * Time.deltaTime;
        }

        if (_isMoving && moveDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(-moveDir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Animate mesh child
        if (_meshTransform != null && _meshTransform != transform)
        {
            // Hit animation takes priority
            if (_isHit)
            {
                _hitRotation = Mathf.Lerp(_hitRotation, 0f, Time.deltaTime * hitRecoverySpeed);
                _meshTransform.localRotation = Quaternion.Euler(_hitRotation, 0f, 0f);

                if (Mathf.Abs(_hitRotation) < 0.5f)
                {
                    _hitRotation = 0f;
                    _isHit = false;
                }
            }
            else if (_isMoving)
            {
                float sway = Mathf.Sin((Time.time * swaySpeed) + _swayOffset) * swayAmount;
                float bob = Mathf.Sin((Time.time * bobSpeed) + _swayOffset) * bobAmount;
                _meshTransform.localPosition = _meshStartLocalPos + new Vector3(sway, bob, 0f);
                _meshTransform.localRotation = Quaternion.Euler(0f, 0f, sway * 20f);
            }
            else
            {
                float bob = Mathf.Sin((Time.time * bobSpeed * 0.5f) + _swayOffset) * bobAmount * 1.5f;
                _meshTransform.localPosition = Vector3.Lerp(
                    _meshTransform.localPosition,
                    _meshStartLocalPos + new Vector3(0f, bob, 0f),
                    Time.deltaTime * 5f);
                _meshTransform.localRotation = Quaternion.Lerp(
                    _meshTransform.localRotation,
                    Quaternion.identity,
                    Time.deltaTime * 5f);
            }
        }
    }

    private bool IsPathBlocked(Vector3 moveDir)
    {
        // Use layermask or ignore own collider via SphereCast with offset
        float checkDistance = 1.2f;
        Ray ray = new Ray(transform.position + Vector3.up * 0.5f + moveDir * 0.3f, moveDir);
        if (Physics.Raycast(ray, out RaycastHit hit, checkDistance))
        {
            if (hit.collider.GetComponent<EnemyHealth>() != null &&
                hit.collider.gameObject != gameObject)
                return true;
        }
        return false;
    }
}
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

using Debug = UnityEngine.Debug;

public class MeshPuller : MonoBehaviour
{
    [Header("Settings")]
    public bool isRightHand = true;

    public float attractSpeed = 8f;

    public float snapDistance = 0.15f;

    public float rayDistance = 10f;

    public float grabRadius = 0.15f;

    private InputAction _gripAction;
    private InputAction _releaseAction;
    private MeshInteraction _targetMesh;
    private Coroutine _attractRoutine;

    private void Awake()
    {
        string hand = isRightHand ? "{RightHand}" : "{LeftHand}";

        _gripAction = new InputAction(
            name: "Grip",
            binding: $"<XRController>{hand}/gripPressed"
        );
        _releaseAction = new InputAction(
            name: "GripRelease",
            binding: $"<XRController>{hand}/gripPressed"
        );

        _gripAction.performed += OnGripPressed;
        _releaseAction.canceled += OnGripReleased;
    }

    private void OnEnable()
    {
        _gripAction.Enable();
        _releaseAction.Enable();
    }

    private void OnDisable()
    {
        _gripAction.Disable();
        _releaseAction.Disable();
    }

    private void OnDestroy()
    {
        _gripAction.performed -= OnGripPressed;
        _releaseAction.canceled -= OnGripReleased;
        _gripAction.Dispose();
        _releaseAction.Dispose();
    }

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            TryUpgradeHeldMesh();
    }

    private void OnGripPressed(InputAction.CallbackContext ctx)
    {
        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.SphereCast(ray, grabRadius, out RaycastHit hit, rayDistance))
        {
            var mesh = hit.collider.GetComponent<MeshInteraction>();
            if (mesh != null && !mesh.IsHeld)
            {
                _targetMesh = mesh;
                if (_attractRoutine != null) StopCoroutine(_attractRoutine);
                _attractRoutine = StartCoroutine(AttractMesh(_targetMesh));
            }
        }
    }

    private void OnGripReleased(InputAction.CallbackContext ctx)
    {
        if (_attractRoutine != null)
        {
            StopCoroutine(_attractRoutine);
            _attractRoutine = null;
        }

        if (_targetMesh != null)
        {
            _targetMesh.ReleaseAttract();
            _targetMesh = null;
        }
    }

    private IEnumerator AttractMesh(MeshInteraction mesh)
    {
        OVRInput.Controller controller = isRightHand
            ? OVRInput.Controller.RTouch
            : OVRInput.Controller.LTouch;

        mesh.BeginAttract();

        while (mesh != null)
        {
            float dist = Vector3.Distance(mesh.transform.position, transform.position);

            if (dist <= snapDistance)
            {
                mesh.BeginHold(controller);
                _targetMesh = null;
                _attractRoutine = null;
                yield break;
            }

            mesh.transform.position = Vector3.MoveTowards(
                mesh.transform.position,
                transform.position,
                attractSpeed * Time.deltaTime
            );

            yield return null;
        }
    }

    private void TryUpgradeHeldMesh()
    {
        MeshInteraction held = FindHeldMesh();
        if (held == null)
        {
            Debug.Log("[MeshPuller] No mesh currently held.");
            return;
        }

        var upgrade = held.GetComponent<PendingMeshyUpgrade>();
        if (upgrade == null)
        {
            Debug.Log("[MeshPuller] Held mesh has no pending upgrade.");
            return;
        }

        if (!upgrade.IsUpgradeReady)
        {
            Debug.Log("[MeshPuller] Meshy upgrade not ready yet — still generating.");
            return;
        }

        upgrade.UpgradeToMeshyModel();
        Debug.Log("[MeshPuller] Meshy upgrade applied.");
    }

    private MeshInteraction FindHeldMesh()
    {
        foreach (var mesh in FindObjectsByType<MeshInteraction>(FindObjectsSortMode.None))
            if (mesh.IsHeld) return mesh;
        return null;
    }
}
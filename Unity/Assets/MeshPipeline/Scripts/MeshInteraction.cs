using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(MeshCollider))]
public class MeshInteraction : MonoBehaviour
{
    [Header("Physics")]
    public float mass = 0.5f;
    public float drag = 0.5f;

    [Header("Audio")]
    public AudioClip swingClip;
    public AudioSource audioSource;
    public float swingVelocityThreshold = 1.5f;

    public bool IsHeld => _holdingController != OVRInput.Controller.None;
    public bool IsBeingAttracted { get; private set; }

    private Rigidbody _rb;
    private MeshCollider _col;
    private OVRInput.Controller _holdingController = OVRInput.Controller.None;
    private Vector3 _localGrabOffset;
    private Quaternion _localGrabRotation;
    private Vector3 _lastControllerPos;
    private float _swingCooldown;

    private void Start()
    {
        if (_rb == null)
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = GetComponentInChildren<MeshFilter>();
            }
            if (filter != null)
            {
                Initialise(filter.sharedMesh);
            }
        }
    }

    /// <summary>
    /// Initialises collision and rigidbody settings for mesh interaction
    /// </summary>
    /// <param name="mesh">Mesh data</param>
    public void Initialise(Mesh mesh)
    {
        _col = GetComponent<MeshCollider>();
        _col.sharedMesh = mesh;
        _col.convex = true;

        _rb = GetComponent<Rigidbody>();
        _rb.mass = mass;
        _rb.linearDamping = drag;
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    /// <summary>
    /// Sets the mesh to attraction mode before it is held
    /// </summary>
    public void BeginAttract()
    {
        IsBeingAttracted = true;
        _rb.isKinematic = true;
        _rb.useGravity = false;
    }

    /// <summary>
    /// Grabs the mesh with a controller and caches local grab offsets
    /// </summary>
    /// <param name="controller">Controller currently holding the mesh</param>
    public void BeginHold(OVRInput.Controller controller)
    {
        IsBeingAttracted = false;
        _holdingController = controller;
        _rb.isKinematic = true;
        _rb.useGravity = false;
        transform.SetParent(null);

        Vector3 controllerPos = OVRInput.GetLocalControllerPosition(controller);
        Quaternion controllerRot = OVRInput.GetLocalControllerRotation(controller);
        _localGrabOffset = Quaternion.Inverse(controllerRot) * (transform.position - controllerPos);
        _localGrabRotation = Quaternion.Inverse(controllerRot) * transform.rotation;
        _lastControllerPos = controllerPos;
    }

    /// <summary>
    /// Stops attraction and re-enables physics on the mesh
    /// </summary>
    public void ReleaseAttract()
    {
        IsBeingAttracted = false;
        _holdingController = OVRInput.Controller.None;
        _rb.isKinematic = false;
        _rb.useGravity = true;
        transform.SetParent(null);
    }

    private void Update()
    {
        if (_holdingController == OVRInput.Controller.None)
        {
            return;
        }

        if (!OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, _holdingController))
        {
            Release();
            return;
        }

        Vector3 controllerPos = OVRInput.GetLocalControllerPosition(_holdingController);
        Quaternion controllerRot = OVRInput.GetLocalControllerRotation(_holdingController);
        transform.position = controllerPos + controllerRot * _localGrabOffset;
        transform.rotation = controllerRot * _localGrabRotation;

        float speed = (controllerPos - _lastControllerPos).magnitude / Time.deltaTime;
        _lastControllerPos = controllerPos;
        _swingCooldown -= Time.deltaTime;

        if (speed > swingVelocityThreshold && _swingCooldown <= 0f
            && swingClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(swingClip);
            _swingCooldown = 0.2f;
        }
    }

    /// <summary>
    /// Releases the held mesh and applies controller throw velocity
    /// </summary>
    private void Release()
    {
        OVRInput.Controller released = _holdingController;
        _holdingController = OVRInput.Controller.None;
        transform.SetParent(null);

        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.linearVelocity = OVRInput.GetLocalControllerVelocity(released);
        _rb.angularVelocity = OVRInput.GetLocalControllerAngularVelocity(released);
    }
}

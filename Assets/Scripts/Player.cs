using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    //Cinemachine reference 
    [SerializeField] CinemachineOrbitalFollow OrbitalFollow;
    //Camera target 
    [SerializeField] private Transform CameraTarget;

    [Header("Movement")]
    [SerializeField] float MoveSpeed = 10f;
    //WSAD - moveInput.x = left and right - moveInput.y is WS
    Vector2 moveInput;
    Vector2 lookInput;

    //Multiplier for orbit input
    [Header("Orbit / Rotation")]
    [SerializeField] float OrbitSensitivity = 0.5f;
    [SerializeField] float OrbitSmothing = 5f;

    bool middleMInput = false;

    [Header("Zoom Zoom")]
    [SerializeField] float ZoomSpeed = 4f;
    [SerializeField] float ZoomSmoothing = 10f;
    [SerializeField] Vector2 ZoomRange = new Vector2(2f, 50f);
    float scrollInput;



    void Awake()
    {
        if (!CameraTarget) CameraTarget = transform;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        UpdateMove(dt);
        if (middleMInput)
        {
            UpdateOrbit(dt);
        }
        UpdateScroll(dt);
    }


    void UpdateMove(float dt)
    {
        var cam = Camera.main;
        if (!cam) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        //Normalize sets the length to 1 to keep speed consistant
        forward.Normalize();

        Vector3 right = cam.transform.right;
        //setting this to 0 allows the camera to not fly up into the air

        right.y = 0f;
        right.Normalize();
        //Combine WSAD into a world direction
        Vector3 motion = (right * moveInput.x + forward * moveInput.y) * MoveSpeed * dt;
        //Sets the camera here
        CameraTarget.position += motion;
    }
    void UpdateOrbit(float dt)
    {
        if (Mouse.current == null || !Mouse.current.middleButton.isPressed) return;

        Vector2 orbitInput = lookInput * OrbitSensitivity;

        var horizontal = OrbitalFollow.HorizontalAxis;
        var vertical = OrbitalFollow.VerticalAxis;

        float targetH = horizontal.Value + orbitInput.x;
        float targetV = vertical.Value - orbitInput.y;

        horizontal.Value = Mathf.Lerp(horizontal.Value, targetH, OrbitSmothing * dt);
        vertical.Value = Mathf.Lerp(vertical.Value, targetV, OrbitSmothing * dt);

        //horizontal.Value = Mathf.Clamp(horizontal.Value, horizontal.Range.x, horizontal.Range.y);
        vertical.Value = Mathf.Clamp(vertical.Value, vertical.Range.x, vertical.Range.y);

        OrbitalFollow.HorizontalAxis = horizontal;
        OrbitalFollow.VerticalAxis = vertical;
    }
    //Now zoom and rotate camera functions. I want to do middle mouse button for rotate camera
    void OnMove(InputValue v)
    {
        moveInput = v.Get<Vector2>();
    }

    void OnMiddleClick(InputValue v)
    {
        middleMInput = v.isPressed;
    }
    void OnScroll(InputValue v)
    {
        scrollInput = v.Get<float>(); // scroll Y
    }
    void OnLook(InputValue v)
    {
        lookInput = v.Get<Vector2>();
    }
    void UpdateScroll(float dt)
    {
        if (!OrbitalFollow) return;

        // scroll is "clicky", so treat it like an impulse
        float target = OrbitalFollow.Radius - scrollInput * ZoomSpeed;

        // clamp
        target = Mathf.Clamp(target, ZoomRange.x, ZoomRange.y);

        // smooth
        OrbitalFollow.Radius = Mathf.Lerp(OrbitalFollow.Radius, target, ZoomSmoothing * dt);

        // consume so it doesn't keep zooming
        scrollInput = 0f;
    }
}

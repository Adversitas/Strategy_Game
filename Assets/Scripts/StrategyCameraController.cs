using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class StrategyCameraController : MonoBehaviour
{
    [Header("Pan")]
    [SerializeField] private float panSpeed = 20f;
    [SerializeField] private float edgePanThickness = 15f;
    [SerializeField] private bool edgePanEnabled = true;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 12f;
    [SerializeField] private float minZoom = 4f;
    [SerializeField] private float maxZoom = 25f;

    private Camera _cam;
    private bool _isDragging;
    private Vector3 _dragOrigin;

    public bool IsDragging => _isDragging;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;
    }

    private void Update()
    {
        HandleMouseDrag();
        HandleKeyboardPan();
        HandleZoom();
    }

    private void HandleMouseDrag()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.middleButton.wasPressedThisFrame ||
            Mouse.current.rightButton.wasPressedThisFrame)
        {
            _isDragging = true;
            _dragOrigin = GetMouseWorldPosition();
        }

        if (Mouse.current.middleButton.wasReleasedThisFrame &&
            Mouse.current.rightButton.wasReleasedThisFrame)
        {
            _isDragging = false;
        }
        // Also stop if neither is held
        if (!Mouse.current.middleButton.isPressed &&
            !Mouse.current.rightButton.isPressed)
        {
            _isDragging = false;
        }

        if (_isDragging)
        {
            Vector3 currentMouseWorld = GetMouseWorldPosition();
            Vector3 delta = _dragOrigin - currentMouseWorld;
            transform.position += delta;
        }
    }

    private void HandleKeyboardPan()
    {
        Vector3 movement = Vector3.zero;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
            {
                movement += Vector3.up;
            }
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
            {
                movement += Vector3.down;
            }
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
            {
                movement += Vector3.left;
            }
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
            {
                movement += Vector3.right;
            }
        }

        if (edgePanEnabled && Mouse.current != null)
        {
            Vector2 mouse = Mouse.current.position.ReadValue();
            if (mouse.x >= 0f && mouse.x < edgePanThickness) movement += Vector3.left;
            if (mouse.x <= Screen.width && mouse.x > Screen.width - edgePanThickness) movement += Vector3.right;
            if (mouse.y >= 0f && mouse.y < edgePanThickness) movement += Vector3.down;
            if (mouse.y <= Screen.height && mouse.y > Screen.height - edgePanThickness) movement += Vector3.up;
        }

        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }

        transform.position += movement * (panSpeed * Time.deltaTime);
    }

    private void HandleZoom()
    {
        if (Mouse.current == null) return;

        float scrollInput = Mouse.current.scroll.ReadValue().y / 120f;
        if (Mathf.Approximately(scrollInput, 0f))
        {
            return;
        }

        _cam.orthographicSize -= scrollInput * zoomSpeed * Time.deltaTime;
        _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize, minZoom, maxZoom);
    }

    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.back, Vector3.zero);
        groundPlane.Raycast(ray, out float enter);
        return ray.GetPoint(enter);
    }
}

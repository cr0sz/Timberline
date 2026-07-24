using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

// Floating joystick: touch anywhere on the touch zone and the stick appears under
// your finger; drag to move, lift to hide. Feeds <Gamepad>/leftStick exactly like
// the old fixed OnScreenStick did, so PlayerController needs no changes.
//
// Lives on a fullscreen transparent Image (the touch zone). Because the zone is the
// FIRST child of the Canvas, every real button (shop BUY etc.) sits above it in the
// raycast order and still receives taps normally.
public class FloatingJoystick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Wiring")]
    public RectTransform background;   // the joystick ring, hidden until touch
    public RectTransform handle;       // the knob inside the ring

    [Header("Feel")]
    [Tooltip("How far (in canvas units) the knob travels from center at full tilt.")]
    public float range = 120f;

    [InputControl(layout = "Vector2")]
    [SerializeField] string m_ControlPath = "<Gamepad>/leftStick";
    protected override string controlPathInternal
    {
        get => m_ControlPath;
        set => m_ControlPath = value;
    }

    RectTransform zone;
    Canvas canvas;
    Vector2 pressScreenPos;

    void Awake()
    {
        zone = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        if (background != null) background.gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData e)
    {
        // While placing a structure, the whole screen drives the ghost, not the
        // player — swallow the touch so the character stays put.
        if (PlacementController.Placing) return;
        // park the ring exactly under the finger
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            zone, e.position, e.pressEventCamera, out Vector2 local);
        background.anchoredPosition = local;
        background.gameObject.SetActive(true);
        handle.anchoredPosition = Vector2.zero;
        pressScreenPos = e.position;
        SendValueToControl(Vector2.zero);
    }

    public void OnDrag(PointerEventData e)
    {
        if (PlacementController.Placing) return;
        // screen-pixel delta -> canvas units (scaleFactor), clamped to the ring
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 delta = (e.position - pressScreenPos) / (range * scale);
        Vector2 value = Vector2.ClampMagnitude(delta, 1f);
        handle.anchoredPosition = value * range;
        SendValueToControl(value);
    }

    public void OnPointerUp(PointerEventData e)
    {
        background.gameObject.SetActive(false);
        SendValueToControl(Vector2.zero);
    }
}

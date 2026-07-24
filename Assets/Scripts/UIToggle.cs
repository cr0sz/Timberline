using UnityEngine;

// Flips a panel on/off — wire a button's onClick to Toggle(). Used for the build
// menu (hidden until the BUILD button is tapped, like a settings panel).
public class UIToggle : MonoBehaviour
{
    public GameObject target;

    public void Toggle()
    {
        if (target != null) target.SetActive(!target.activeSelf);
    }

    public void Hide()
    {
        if (target != null) target.SetActive(false);
    }
}

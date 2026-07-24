using UnityEngine;

// Keeps one thing in the hand at a time: the axe while chopping/mining, the
// spear the rest of the time (idle + combat). Fixes the "holding both" look.
public class HeldToolSwap : MonoBehaviour
{
    public GameObject axe;     // Wrist_R/jointItemR/Hatchet (gather tool)
    public GameObject weapon;  // Wrist_R/Mace (combat weapon)
    public PlayerGatherer gatherer;
    public PlayerCombat combat;

    void Update()
    {
        // Empty-handed while idle/walking/running. Weapon only appears mid-fight,
        // axe only while chopping/mining. Combat wins ties.
        bool fighting = combat != null && combat.HasTarget;
        bool chopping = gatherer != null && gatherer.Gathering;
        bool showAxe = chopping && !fighting;
        bool showWeapon = fighting;
        if (axe != null && axe.activeSelf != showAxe) axe.SetActive(showAxe);
        if (weapon != null && weapon.activeSelf != showWeapon) weapon.SetActive(showWeapon);
    }
}

using UnityEngine;

// Keeps one thing in the hand at a time:
//   - hatchet while chopping WOOD,
//   - pickaxe while mining STONE,
//   - mace during a fight,
//   - nothing while idle/walking.
// Combat wins ties. Fixes both the "holding both" look and the "mining a rock with
// an axe" look.
public class HeldToolSwap : MonoBehaviour
{
    public GameObject axe;      // Wrist_R/jointItemR/Hatchet  — wood
    public GameObject pickaxe;  // Wrist_R/jointItemR/Pickaxe  — stone
    public GameObject weapon;   // Wrist_R/Mace                — combat
    public PlayerGatherer gatherer;
    public PlayerCombat combat;

    void Update()
    {
        bool fighting = combat != null && combat.HasTarget;
        bool gathering = gatherer != null && gatherer.Gathering;
        bool mining = gathering && gatherer.GatheringStone;

        bool showWeapon = fighting;
        bool showPick = mining && !fighting;
        bool showAxe = gathering && !mining && !fighting;

        if (axe != null && axe.activeSelf != showAxe) axe.SetActive(showAxe);
        if (pickaxe != null && pickaxe.activeSelf != showPick) pickaxe.SetActive(showPick);
        if (weapon != null && weapon.activeSelf != showWeapon) weapon.SetActive(showWeapon);
    }
}

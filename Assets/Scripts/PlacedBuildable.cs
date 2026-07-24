using UnityEngine;

// Marker on any structure placed by BuildSystem, so MOVE mode can find and grab
// it and SaveManager can rebuild it. catalogIndex is which BuildSystem.catalog
// entry it came from (-1 = unknown / not persistable).
public class PlacedBuildable : MonoBehaviour
{
    public int catalogIndex = -1;
}

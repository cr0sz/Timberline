using UnityEngine;
using UnityEngine.AI;          // NavMeshCollectGeometry
using UnityEditor;
using Unity.AI.Navigation;     // NavMeshSurface, CollectObjects

// Bakes the scene's NavMesh, which is what Creature.PathDirection routes on.
// Menu: Tools/Survival/Bake NavMesh.
//
// Uses a NavMeshSurface (com.unity.ai.navigation) rather than the old
// UnityEditor.AI.NavMeshBuilder bake — the legacy path is deprecated in Unity 6 and
// throws CS0618 all over a console this project keeps clean. The surface also removes
// the need to mark anything Navigation Static: it collects by physics colliders, which
// is exactly the set we want (ground, trees, rocks, structures all carry one).
//
// Re-run after any change to the terrain or the static world layout. Player-PLACED
// structures are spawned at runtime and can never be in a bake — Creature covers that
// with its SphereCast fallback and by re-pathing several times a second, so an animal
// still slides along a freshly built wall even though the mesh under it is stale.
public static class NavMeshSetup
{
    [MenuItem("Tools/Survival/Bake NavMesh")]
    public static void Bake()
    {
        var go = GameObject.Find("NavMesh");
        if (go == null)
        {
            go = new GameObject("NavMesh");
            Undo.RegisterCreatedObjectUndo(go, "Create NavMesh");
        }

        var surface = go.GetComponent<NavMeshSurface>();
        if (surface == null) surface = go.AddComponent<NavMeshSurface>();

        // Collect from colliders, not renderers: the trees carry trunk-shaped capsules
        // that are far narrower than their canopy meshes, and it's the trunk an animal
        // has to walk around. Baking off render geometry would carve a hole the size of
        // the whole canopy and wall the forest off.
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;

        // Agent shape: sized off the widest animal in the game (the bear, radius 0.65)
        // so every creature fits through every gap the mesh says is passable.
        surface.agentTypeID = 0;              // the built-in Humanoid agent
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.15f;            // finer than default; the fences are thin
        surface.overrideTileSize = false;
        surface.minRegionArea = 2f;           // drop slivers on top of rocks and crates

        surface.BuildNavMesh();

        EditorUtility.SetDirty(surface);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[NavMeshSetup] NavMesh baked from physics colliders onto the 'NavMesh' surface.");
    }
}

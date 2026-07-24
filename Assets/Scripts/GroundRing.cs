using UnityEngine;

// A flat circle drawn on the ground, used to show a radius while you are deciding
// where to put something. Currently: the campfire's predator-repel area, shown on the
// placement ghost and nowhere else — a permanent ring around every fire was visual
// noise (user, 2026-07-23).
//
// ponytail: a LineRenderer, not a decal or a projector — no texture, no custom shader,
// no render-feature setup, and re-sizing is one loop.
[RequireComponent(typeof(LineRenderer))]
public class GroundRing : MonoBehaviour
{
    LineRenderer line;

    /// Build (or re-size) the ring. `radius` is in WORLD metres.
    public void Setup(float radius, Color color, int segments = 48, float width = 0.14f, float lift = 0.06f)
    {
        if (line == null) line = GetComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Mathf.Max(8, segments);
        line.receiveShadows = false;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // Lie flat on the ground rather than billboarding to face the camera.
        line.alignment = LineAlignment.TransformZ;
        transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        if (line.sharedMaterial == null) line.material = MakeMat(color);
        else SetColor(line.sharedMaterial, color);
        line.startColor = line.endColor = color;

        // The parent may be scaled (the campfire scales per tier), and a LineRenderer's
        // local positions are scaled with it — so divide the scale out or the ring ends
        // up somewhere other than the radius it claims to show.
        float s = Mathf.Max(0.0001f, transform.lossyScale.x);
        float r = radius / s;
        int n = line.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = (i / (float)n) * Mathf.PI * 2f;
            // Local XY, because the transform is rotated 90 degrees onto the ground.
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, -lift / s));
        }
        line.widthMultiplier = width / s;
    }

    static Material MakeMat(Color c)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var m = new Material(shader);
        SetColor(m, c);
        m.SetFloat("_Surface", 1f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    /// Convenience: attach a ring to `parent` at the origin.
    public static GroundRing Attach(Transform parent, float radius, Color color)
    {
        var go = new GameObject("GroundRing");
        go.transform.SetParent(parent, false);
        var ring = go.AddComponent<GroundRing>();
        ring.Setup(radius, color);
        return ring;
    }
}

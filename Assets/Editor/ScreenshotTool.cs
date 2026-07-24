using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders README/store screenshots from edit mode.
///
/// Play mode does not tick reliably when the editor is driven headlessly, so these are
/// edit-mode renders: a throwaway camera is placed at the game's own rig offset
/// (8 up, 6 back, 50 deg pitch) and pointed at a world position. That matches what the
/// follow camera actually shows in play.
/// </summary>
public static class ScreenshotTool
{
    // The game camera rig, read off Main Camera under CameraFollowTarget.
    const float RigHeight = 8f;
    const float RigBack = 6f;
    const float RigPitch = 50f;
    const float RigFov = 60f;

    struct Shot
    {
        public string Name;
        public Vector3 Focus;   // world XZ the camera looks at; Y is raycast onto the ground
        public float Yaw;       // camera heading in degrees
        public float Dolly;     // multiplier on the rig distance — >1 pulls back for wide shots
        public int Width;
        public int Height;
    }

    // The player is deliberately never in frame: edit mode runs no Animator, so the rig
    // renders in its T-pose. Character shots have to come from a real device playtest.
    static readonly Shot[] Shots =
    {
        // Hero banner — pulled well back over the camp, looking north up the valley.
        new Shot { Name = "01-valley",     Focus = new Vector3(0, 0, 6),     Yaw = 0,   Dolly = 4.5f, Width = 1920, Height = 1080 },
        // Base camp: market stall, merchant, upgrade pads. Offset east so the menhir
        // frames the shot from the edge instead of filling a third of it.
        new Shot { Name = "02-camp",       Focus = new Vector3(6, 0, 3),     Yaw = 0,   Dolly = 2.0f, Width = 1600, Height = 900 },
        // Tier-1 wood.
        new Shot { Name = "03-meadow",     Focus = new Vector3(-4, 0, 38),   Yaw = 20,  Dolly = 2.4f, Width = 1600, Height = 900 },
        // Tier-1 stone, on the dedicated quarry floor.
        new Shot { Name = "04-quarry",     Focus = new Vector3(42, 0, -32),  Yaw = -30, Dolly = 2.4f, Width = 1600, Height = 900 },
        // Endgame wood zone, the densest part of the map.
        new Shot { Name = "05-deepforest", Focus = new Vector3(-28, 0, -46), Yaw = 45,  Dolly = 2.4f, Width = 1600, Height = 900 },
        // Tier-5 stone.
        new Shot { Name = "06-orefield",   Focus = new Vector3(77, 0, -21),  Yaw = -20, Dolly = 2.4f, Width = 1600, Height = 900 },
        // Portrait, at the real device aspect, so the README can show the mobile framing.
        new Shot { Name = "07-portrait",   Focus = new Vector3(4, 0, 4),     Yaw = 0,   Dolly = 1.8f, Width = 1179, Height = 2556 },
    };

    [MenuItem("Tools/Survival/Capture Screenshots")]
    public static void Capture()
    {
        var outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
        Directory.CreateDirectory(outDir);

        var src = Camera.main;
        if (src == null)
        {
            Debug.LogError("[SHOTS] No Main Camera in the scene.");
            return;
        }

        var player = GameObject.Find("Player");
        var playerPos = player != null ? player.transform.position : Vector3.zero;
        var playerRot = player != null ? player.transform.rotation : Quaternion.identity;

        var rigGo = new GameObject("~ScreenshotCam");
        rigGo.hideFlags = HideFlags.HideAndDontSave;
        var cam = rigGo.AddComponent<Camera>();
        cam.CopyFrom(src);
        cam.fieldOfView = RigFov;
        cam.targetTexture = null;
        cam.enabled = false; // we drive it with explicit Render() calls only

        var written = new List<string>();
        try
        {
            foreach (var shot in Shots)
            {
                var focus = GroundAt(shot.Focus);

                // Park the player far below the map so the T-posed rig never renders.
                if (player != null)
                    player.transform.position = new Vector3(0, -500, 0);

                var rot = Quaternion.Euler(RigPitch, shot.Yaw, 0);
                var offset = rot * new Vector3(0, 0, -RigBack * shot.Dolly);
                cam.transform.position = focus + offset + Vector3.up * (RigHeight * shot.Dolly);
                cam.transform.rotation = rot;

                var path = Path.Combine(outDir, shot.Name + ".png");
                Render(cam, shot.Width, shot.Height, path);
                written.Add(shot.Name + ".png");
            }
        }
        finally
        {
            if (player != null)
            {
                player.transform.position = playerPos;
                player.transform.rotation = playerRot;
            }
            Object.DestroyImmediate(rigGo);
        }

        Debug.Log($"[SHOTS] wrote {written.Count} to {outDir}: {string.Join(", ", written)}");
    }

    /// <summary>Drops <paramref name="p"/> onto whatever ground is under it, so shots survive terrain edits.</summary>
    static Vector3 GroundAt(Vector3 p)
    {
        var from = new Vector3(p.x, 300f, p.z);
        if (Physics.Raycast(from, Vector3.down, out var hit, 600f))
            return hit.point;
        // ponytail: flat fallback — the valley floor is y=0 everywhere the shots point at.
        return new Vector3(p.x, 0f, p.z);
    }

    static void Render(Camera cam, int width, int height, string path)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 8
        };
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        var prevActive = RenderTexture.active;
        try
        {
            cam.aspect = width / (float)height;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = prevActive;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}

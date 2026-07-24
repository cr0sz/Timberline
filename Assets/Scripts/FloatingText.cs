using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Pooled rising damage/gather popup. Fast tools + damage numbers spawn these many
// times a second, so they're recycled instead of Instantiate/Destroy'd per popup.
// Spawn through the static Spawn(); it reuses a dead one or makes a new one.
public class FloatingText : MonoBehaviour
{
    public float riseSpeed = 1f;    // how fast it drifts up
    public float lifetime = 1f;     // seconds before it vanishes

    static readonly Stack<FloatingText> pool = new Stack<FloatingText>();

    TextMeshPro tmp;
    Transform cam;
    float age;

    // Reuse a pooled instance if one is free, else clone the prefab. Handles its own
    // Show(), scatter, and reset — callers just fire and forget.
    public static FloatingText Spawn(FloatingText prefab, Vector3 pos, string message)
    {
        if (prefab == null) return null;
        FloatingText ft = null;
        // A scene reload destroys pooled instances but the static stack survives —
        // skip any that Unity has torn down (== null on a destroyed object).
        while (pool.Count > 0 && ft == null) ft = pool.Pop();

        if (ft == null) ft = Instantiate(prefab, pos, Quaternion.identity);
        else { ft.transform.SetPositionAndRotation(pos, Quaternion.identity); ft.gameObject.SetActive(true); }

        ft.Begin(message);
        return ft;
    }

    void Awake()
    {
        tmp = GetComponent<TextMeshPro>();
        if (Camera.main != null) cam = Camera.main.transform;
    }

    void Begin(string message)
    {
        age = 0f;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        // Scatter a little so rapid popups at the same spot don't stack unreadably.
        transform.position += new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0f, 0.25f), 0f);
        Show(message);
        if (tmp != null) { Color c = tmp.color; c.a = 1f; tmp.color = c; }
    }

    public void Show(string message)
    {
        if (tmp == null) tmp = GetComponent<TextMeshPro>();
        tmp.text = message;
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;
        if (cam != null) transform.rotation = cam.rotation;   // billboard

        if (tmp != null)
        {
            Color c = tmp.color;
            c.a = Mathf.Lerp(1f, 0f, age / lifetime);
            tmp.color = c;
        }

        if (age >= lifetime)
        {
            gameObject.SetActive(false);
            pool.Push(this);
        }
    }
}

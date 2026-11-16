// 오락실 기계 간판 둘레의 작은 전구들이 번갈아 깜빡이며 도는 효과 (빛나는 색만 바꿈)
using UnityEngine;

public class MarqueeLights : MonoBehaviour
{
    public Renderer[] bulbs;
    [ColorUsage(false, true)] public Color colorA = new Color(4f, 2.6f, 0.6f);
    [ColorUsage(false, true)] public Color colorB = new Color(0.35f, 0.2f, 0.05f);
    [Tooltip("1초에 몇 칸씩 도는지")]
    public float speed = 6f;
    [Tooltip("몇 개마다 하나씩 켜지는지")]
    public int spacing = 3;
    public float phase;

    MaterialPropertyBlock mpb;
    int lastStep = -1;

    void Awake() { mpb = new MaterialPropertyBlock(); }

    void Update()
    {
        if (bulbs == null || bulbs.Length == 0) return;
        int step = Mathf.FloorToInt(Time.time * speed + phase);
        if (step == lastStep) return;
        lastStep = step;
        for (int i = 0; i < bulbs.Length; i++)
        {
            var r = bulbs[i];
            if (r == null) continue;
            bool on = ((i + step) % Mathf.Max(1, spacing)) == 0;
            mpb.SetColor("_EmissionColor", on ? colorA : colorB);
            r.SetPropertyBlock(mpb);
        }
    }
}

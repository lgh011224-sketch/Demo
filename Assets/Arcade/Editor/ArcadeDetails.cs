// 오락실 기계의 반실사 부품: 인쇄 아트 시트, 스피커 그릴, 고무 링, 동전 투입구 문, 크롬 몰딩
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ArcadeDetails
{
    const string Dir = ArcadeBuilder.Dir;

    // 기계 옆면·앞면에 붙는 인쇄 아트 (그라데이션 + 사선 줄 + 망점 + 반짝이) – 코팅 시트처럼 약간 광택
    public static Material ArtMat(int variant, Color a, Color b)
    {
        string path = $"{Dir}/Art{variant}.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            const int W = 512, H = 512;
            var px = new Color[W * H];
            var rng = new System.Random(variant * 31 + 7);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = y / (float)H, u = x / (float)W;
                    Color c = Color.Lerp(a, b, v * 0.8f + 0.1f * Mathf.Sin(u * 6.28f));
                    float stripe = Mathf.Repeat((x + y) / 48f, 1f);
                    if (stripe < 0.18f) c = Color.Lerp(c, Color.white, 0.18f);              // 사선 띠
                    float dx = Mathf.Repeat(x, 12f) - 6f, dy = Mathf.Repeat(y, 12f) - 6f;      // 망점 (인쇄 느낌)
                    float dotR = 1.2f + 2.2f * (1f - v);
                    if (dx * dx + dy * dy < dotR * dotR) c = Color.Lerp(c, b * 0.7f, 0.35f);
                    px[y * W + x] = c;
                }
            for (int s = 0; s < 28; s++)   // 반짝이 별
            {
                int cx = rng.Next(W), cy = rng.Next(H), r = 4 + rng.Next(9);
                for (int k = -r; k <= r; k++)
                {
                    float w = 1f - Mathf.Abs(k) / (float)r;
                    foreach (var (xx, yy) in new[] { (cx + k, cy), (cx, cy + k) })
                        if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = Color.Lerp(px[yy * W + xx], Color.white, w);
                }
            }
            tex = Save(path, px, W, H, true);
        }
        var m = ArcadeBuilder.Mat("Art" + variant, Color.white, 0.72f, 0f, null, tex);
        return m;
    }

    // 스피커 그릴 (검은 금속판에 작은 구멍)
    public static Material GrilleMat()
    {
        string path = $"{Dir}/Grille.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            const int N = 128;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = Mathf.Repeat(x + (y / 8 % 2) * 4, 8f) - 4f, dy = Mathf.Repeat(y, 8f) - 4f;
                    px[y * N + x] = dx * dx + dy * dy < 5f ? new Color(0.01f, 0.01f, 0.01f) : new Color(0.16f, 0.16f, 0.18f);
                }
            tex = Save(path, px, N, N, true);
        }
        var m = ArcadeBuilder.Mat("Grille", Color.white, 0.55f, 0.6f, null, tex);
        m.SetTextureScale("_BaseMap", new Vector2(4f, 4f));
        return m;
    }

    static Texture2D Save(string path, Color[] px, int w, int h, bool mips)
    {
        var t = new Texture2D(w, h, TextureFormat.RGB24, mips);
        t.SetPixels(px); t.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.anisoLevel = 4; ti.wrapMode = TextureWrapMode.Repeat; ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 도넛 모양 (구멍 둘레 고무 링)
    public static Mesh Torus(float R, float r)
    {
        string key = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Torus_{0:F3}_{1:F3}", R, r);
        string path = $"{Dir}/Meshes/{key}.asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex != null) return ex;
        if (!AssetDatabase.IsValidFolder(Dir + "/Meshes")) AssetDatabase.CreateFolder(Dir, "Meshes");
        const int seg = 40, ring = 12;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector3 c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int j = 0; j <= ring; j++)
            {
                float b = j / (float)ring * Mathf.PI * 2f;
                Vector3 nn = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                v.Add(c * R + nn * r); n.Add(nn); uv.Add(new Vector2(i / (float)seg, j / (float)ring));
            }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < ring; j++)
            {
                int a0 = i * (ring + 1) + j, a1 = a0 + 1, b0 = a0 + ring + 1, b1 = b0 + 1;
                t.Add(a0); t.Add(a1); t.Add(b0); t.Add(b0); t.Add(a1); t.Add(b1);
            }
        var mesh = new Mesh { name = key };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        // 앞면 방향 확인 (바깥을 향하게)
        var tris = mesh.triangles; var verts = mesh.vertices; var norms = mesh.normals;
        if (Vector3.Dot(Vector3.Cross(verts[tris[1]] - verts[tris[0]], verts[tris[2]] - verts[tris[0]]), norms[tris[0]]) < 0f)
        { for (int k = 0; k < tris.Length; k += 3) { int tmp = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = tmp; } mesh.triangles = tris; }
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    public static Transform MeshObj(Transform parent, string name, Mesh mesh, Material m, Vector3 pos)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g.transform;
    }

    // 동전 투입구 문 (금속판 + 동전 구멍 2개 + 빛나는 '500원' + 열쇠 구멍). 원점 = 문 가운데, 앞 = +Z
    public static void CoinDoor(Transform parent, Vector3 pos, float w, float h)
    {
        var g = ArcadeBuilder.Group(parent, "CoinDoor", pos);
        var plate = ArcadeBuilder.Mat("CoinDoorMetal", new Color(0.62f, 0.64f, 0.7f), 0.75f, 0.85f, null, ArcadeBuilder.PH("metal_plate_02", "diff"));
        ArcadeBuilder.SetNormal(plate, ArcadeBuilder.PH("metal_plate_02", "nor_gl"), 0.5f);
        var chrome = ArcadeBuilder.Mat("Chrome", new Color(0.85f, 0.86f, 0.9f), 0.9f, 1f);
        var black = ArcadeBuilder.Mat("CabinetDark", new Color(0.06f, 0.06f, 0.08f), 0.5f);
        ArcadeBuilder.S(ArcadeBuilder.RBox(g, "Plate", Vector3.zero, new Vector3(w, h, 0.012f), plate, false, 0.006f));
        for (int i = 0; i < 2; i++)
        {
            float x = (i - 0.5f) * w * 0.45f;
            ArcadeBuilder.S(ArcadeBuilder.RBox(g, "SlotBezel", new Vector3(x, h * 0.18f, 0.01f), new Vector3(0.05f, 0.075f, 0.012f), chrome, false, 0.005f));
            ArcadeBuilder.S(ArcadeBuilder.Box(g, "Slot", new Vector3(x, h * 0.18f, 0.017f), new Vector3(0.004f, 0.04f, 0.002f), black, false));
            ArcadeBuilder.S(ArcadeBuilder.Box(g, "CoinLight", new Vector3(x, h * 0.18f + 0.06f, 0.008f), new Vector3(0.05f, 0.022f, 0.006f), ArcadeBuilder.Neon("CoinLight", new Color(1f, 0.25f, 0.15f), 2.5f), false));
        }
        ArcadeBuilder.Text(g, "500 WON", new Vector3(0f, h * 0.18f + 0.06f, 0.013f), 0.0028f, Color.white, true);
        var lockCyl = ArcadeBuilder.Cyl(g, "Lock", new Vector3(0f, -h * 0.25f, 0.012f), new Vector3(0.022f, 0.006f, 0.022f), chrome);
        lockCyl.localRotation = Quaternion.Euler(90f, 0f, 0f); ArcadeBuilder.S(lockCyl);
        ArcadeBuilder.S(ArcadeBuilder.Box(g, "Return", new Vector3(0f, -h * 0.38f, 0.01f), new Vector3(0.07f, 0.035f, 0.02f), black, false));
    }

    // 크롬 모서리 몰딩 (세로 모서리에 얇은 둥근 막대)
    public static void CornerTrims(Transform parent, Vector3 center, Vector3 size, Material chrome)
    {
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                ArcadeBuilder.S(ArcadeBuilder.RBox(parent, "Trim", center + new Vector3(sx * size.x * 0.5f, 0f, sz * size.z * 0.5f), new Vector3(0.022f, size.y, 0.022f), chrome, false, 0.009f));
    }

    public static void SetStatic(Transform root)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            if (r.GetComponentInParent<MarqueeLights>() == null || !r.name.StartsWith("Bulb"))
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }
}

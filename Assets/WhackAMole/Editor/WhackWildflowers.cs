// 울타리 안 들꽃·풀 무더기 – 로우폴리 메시 + 반실사 텍스처
//  - 들꽃: 줄기(엇갈린 평면 2장) + 꽃송이(평면 1장) + 잎(평면 3~4장) = 삼각형 약 14~16개
//          텍스처는 Poly Haven 실사 사진 아틀라스(가자니아·민들레)에서 꽃송이·잎·줄기 부분만 잘라 씀
//  - 풀 무더기: 휘어진 풀잎 9~12장 (잎 하나 삼각형 3개), 사진 톤의 풀잎 텍스처를 직접 생성
// 사용: WhackEnvironment(Build Environment / Rebuild Fence And Flowers)가 Build()를 호출
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class WhackWildflowers
{
    const string MeshDir = "Assets/WhackAMole/Meshes/Wildflowers";
    const string FoliageMatDir = "Assets/WhackAMole/Realism/Foliage";
    const float AtlasPx = 1932f; // 아래 좌표는 2048 아틀라스를 1932로 본 화면 좌표 기준

    // 아틀라스 조각 (화면 좌표 px): 꽃송이 = 중심·반지름, 잎·줄기 = 밑동→끝, 반폭
    struct Head { public Vector2 c; public float r; }
    struct Strip { public Vector2 a, b; public float hw; }

    static readonly Head[] GazHeads = { new Head { c = new Vector2(340, 1562), r = 300 }, new Head { c = new Vector2(985, 1590), r = 235 } };
    static readonly Strip[] GazLeaves =
    {
        new Strip { a = new Vector2(1435, 1272), b = new Vector2(548, 1165), hw = 150 },
        new Strip { a = new Vector2(1800, 1108), b = new Vector2(1318, 1005), hw = 105 },
        new Strip { a = new Vector2(60, 1105), b = new Vector2(1052, 742), hw = 185 },
        new Strip { a = new Vector2(1770, 1420), b = new Vector2(1360, 1360), hw = 95 },
    };
    static readonly Strip GazStem = new Strip { a = new Vector2(1662, 945), b = new Vector2(615, 292), hw = 26 };

    static readonly Head[] DanHeads = { new Head { c = new Vector2(1315, 258), r = 205 }, new Head { c = new Vector2(1735, 258), r = 185 } };
    static readonly Strip[] DanLeaves =
    {
        new Strip { a = new Vector2(265, 1782), b = new Vector2(190, 435), hw = 140 },
        new Strip { a = new Vector2(555, 385), b = new Vector2(690, 1490), hw = 165 },
        new Strip { a = new Vector2(1860, 1855), b = new Vector2(615, 1690), hw = 165 },
        new Strip { a = new Vector2(975, 1495), b = new Vector2(1850, 1575), hw = 150 },
    };
    static readonly Strip DanStem = new Strip { a = new Vector2(1890, 665), b = new Vector2(690, 700), hw = 22 };

    static bool Blocked(float x, float z) =>
        (Mathf.Abs(x) < 1.25f && z > -1.3f && z < 2.0f) ||   // 플레이어·탁자·부스
        (Mathf.Abs(x) > 2.9f && Mathf.Abs(x) < 3.5f && Mathf.Abs(z - 0.6f) < 0.5f); // 깃발 장대

    public static string Build(Transform envRoot, System.Random rng, float fenceRadius, Vector3 fenceCenter, List<Vector2> avoid)
    {
        var old = envRoot.Find("Wildflowers");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Wildflowers").transform;
        root.SetParent(envRoot, false);

        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/WhackAMole/Meshes", "Wildflowers");
        var gazMat = AssetDatabase.LoadAssetAtPath<Material>($"{FoliageMatDir}/flower_gazania.mat");
        var danMat = AssetDatabase.LoadAssetAtPath<Material>($"{FoliageMatDir}/dandelion_01.mat");
        var grassMat = GrassMaterial();

        var gaz = new List<Mesh>(); var dan = new List<Mesh>(); var tuft = new List<Mesh>();
        for (int i = 0; i < 4; i++)
        {
            gaz.Add(Save(Flower(new System.Random(100 + i), GazHeads, GazLeaves, GazStem, 0.24f, 0.4f, 0.11f, 0.15f), $"Gazania_{i}"));
            dan.Add(Save(Flower(new System.Random(200 + i), DanHeads, DanLeaves, DanStem, 0.16f, 0.28f, 0.075f, 0.13f), $"Dandelion_{i}"));
            tuft.Add(Save(Tuft(new System.Random(300 + i)), $"GrassTuft_{i}"));
        }

        int flowers = 0, tufts = 0; long tris = 0;
        bool Ok(float x, float z)
        {
            if (Blocked(x, z)) return false;
            if (new Vector2(x - fenceCenter.x, z - fenceCenter.z).magnitude > fenceRadius - 0.35f) return false;
            foreach (var a in avoid) if ((new Vector2(x, z) - a).sqrMagnitude < 0.35f * 0.35f) return false;
            return true;
        }
        void Place(Mesh mesh, Material mat, Vector3 p, float scale, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(root, false);
            g.transform.localPosition = p;
            g.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            g.transform.localScale = Vector3.one * scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off; // 작은 풀꽃은 그림자 생략 (성능)
            g.isStatic = true;
            tris += mesh.triangles.Length / 3;
        }

        // 꽃밭 무리: 같은 종류끼리 모여 핌 (울타리 쪽에 더 많이)
        for (int c = 0, guard = 0; c < 34 && guard < 2000; guard++)
        {
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            float rad = Mathf.Lerp(1.6f, fenceRadius - 0.6f, Mathf.Sqrt((float)rng.NextDouble()));
            float cx = fenceCenter.x + Mathf.Sin(ang) * rad, cz = fenceCenter.z + Mathf.Cos(ang) * rad;
            if (!Ok(cx, cz)) continue;
            c++;
            bool isGaz = rng.NextDouble() < 0.5;
            int n = rng.Next(5, 11);
            for (int k = 0; k < n; k++)
            {
                float x = cx + ((float)rng.NextDouble() - 0.5f) * 0.9f, z = cz + ((float)rng.NextDouble() - 0.5f) * 0.9f;
                if (!Ok(x, z)) continue;
                var list = isGaz ? gaz : dan;
                Place(list[rng.Next(list.Count)], isGaz ? gazMat : danMat, new Vector3(x, 0f, z), 0.85f + (float)rng.NextDouble() * 0.4f, isGaz ? "Gazania" : "Dandelion");
                flowers++;
            }
            // 꽃 무리 둘레에 풀
            for (int k = 0; k < 3; k++)
            {
                float x = cx + ((float)rng.NextDouble() - 0.5f) * 1.3f, z = cz + ((float)rng.NextDouble() - 0.5f) * 1.3f;
                if (!Ok(x, z)) continue;
                Place(tuft[rng.Next(tuft.Count)], grassMat, new Vector3(x, 0f, z), 0.8f + (float)rng.NextDouble() * 0.6f, "GrassTuft");
                tufts++;
            }
        }
        // 흩어진 풀 무더기 · 외톨이 꽃
        for (int i = 0, guard = 0; i < 90 && guard < 3000; guard++)
        {
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            float rad = Mathf.Lerp(1.2f, fenceRadius - 0.4f, Mathf.Sqrt((float)rng.NextDouble()));
            float x = fenceCenter.x + Mathf.Sin(ang) * rad, z = fenceCenter.z + Mathf.Cos(ang) * rad;
            if (!Ok(x, z)) continue;
            i++;
            if (rng.NextDouble() < 0.75) { Place(tuft[rng.Next(tuft.Count)], grassMat, new Vector3(x, 0f, z), 0.7f + (float)rng.NextDouble() * 0.7f, "GrassTuft"); tufts++; }
            else { bool g = rng.NextDouble() < 0.5; Place((g ? gaz : dan)[rng.Next(4)], g ? gazMat : danMat, new Vector3(x, 0f, z), 0.8f + (float)rng.NextDouble() * 0.35f, g ? "Gazania" : "Dandelion"); flowers++; }
        }
        return $"wildflowers={flowers} tufts={tufts} tris={tris}";
    }

    // ───────────────────────── 들꽃 메시 ─────────────────────────
    static Mesh Flower(System.Random rng, Head[] heads, Strip[] leaves, Strip stem, float hMin, float hMax, float headSize, float leafLen)
    {
        var b = new MB();
        float h = Mathf.Lerp(hMin, hMax, (float)rng.NextDouble());
        float lean = ((float)rng.NextDouble() - 0.5f) * 0.12f;
        Vector3 top = new Vector3(lean * h, h, ((float)rng.NextDouble() - 0.5f) * 0.1f * h);
        // 줄기: 엇갈린 평면 2장
        for (int k = 0; k < 2; k++)
        {
            Vector3 side = Quaternion.Euler(0f, k * 90f + 20f, 0f) * Vector3.right * 0.008f;
            b.Quad(-side, side, top + side, top - side, StripUV(stem));
        }
        // 꽃송이: 하늘·사람 쪽으로 기울어진 원판 (사진 한 장)
        var hd = heads[rng.Next(heads.Length)];
        float s = headSize * (0.85f + (float)rng.NextDouble() * 0.3f);
        Quaternion tilt = Quaternion.Euler(-90f + 25f + (float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f, 0f);
        Vector3 ux = tilt * Vector3.right * s * 0.5f, uy = tilt * Vector3.up * s * 0.5f;
        Vector3 hc = top + tilt * Vector3.forward * 0.004f;
        b.Quad(hc - ux - uy, hc + ux - uy, hc + ux + uy, hc - ux + uy, HeadUV(hd));
        // 잎: 밑동에서 방사형으로 비스듬히
        int nl = rng.Next(3, 5);
        float baseYaw = (float)rng.NextDouble() * 360f;
        for (int i = 0; i < nl; i++)
        {
            var lf = leaves[rng.Next(leaves.Length)];
            float len = leafLen * (0.8f + (float)rng.NextDouble() * 0.45f);
            float aspect = lf.hw * 2f / Vector2.Distance(lf.a, lf.b);
            float w = len * aspect;
            Quaternion q = Quaternion.Euler(-(25f + (float)rng.NextDouble() * 35f), baseYaw + i * 360f / nl + ((float)rng.NextDouble() - 0.5f) * 30f, 0f);
            Vector3 dir = q * Vector3.forward, side = q * Vector3.right * w * 0.5f;
            Vector3 p0 = new Vector3(0f, 0.005f, 0f), p1 = p0 + dir * len;
            b.Quad(p0 - side, p0 + side, p1 + side, p1 - side, StripUV(lf));
        }
        return b.ToMesh("Flower");
    }

    static Vector2 UV(Vector2 px) => new Vector2(px.x / AtlasPx, 1f - px.y / AtlasPx);

    // 밑동(a)→끝(b) 띠 → 평면 네 귀퉁이 UV (아래 왼쪽, 아래 오른쪽, 위 오른쪽, 위 왼쪽)
    static Vector2[] StripUV(Strip s)
    {
        Vector2 d = (s.b - s.a).normalized, n = new Vector2(-d.y, d.x) * s.hw;
        return new[] { UV(s.a - n), UV(s.a + n), UV(s.b + n), UV(s.b - n) };
    }

    static Vector2[] HeadUV(Head h) => new[]
    {
        UV(h.c + new Vector2(-h.r, h.r)), UV(h.c + new Vector2(h.r, h.r)), UV(h.c + new Vector2(h.r, -h.r)), UV(h.c + new Vector2(-h.r, -h.r)),
    };

    // ───────────────────────── 풀 무더기 ─────────────────────────
    static Mesh Tuft(System.Random rng)
    {
        var b = new MB();
        int blades = rng.Next(9, 13);
        for (int i = 0; i < blades; i++)
        {
            float yaw = (float)rng.NextDouble() * 360f;
            float h = 0.16f + (float)rng.NextDouble() * 0.2f, w = 0.012f + (float)rng.NextDouble() * 0.008f;
            float bend = 0.25f + (float)rng.NextDouble() * 0.5f;           // 바깥으로 휘는 정도
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 off = q * new Vector3(0f, 0f, (float)rng.NextDouble() * 0.04f);
            Vector3 right = q * Vector3.right * w, fwd = q * Vector3.forward;
            Vector3 b0 = off, m = off + Vector3.up * h * 0.55f + fwd * h * bend * 0.25f, t = off + Vector3.up * h * 0.9f + fwd * h * bend;
            float u0 = (float)rng.NextDouble() * 0.8f; // 풀잎 텍스처에서 세로 띠를 무작위로
            // 밑 사각형(삼각형 2개) + 끝 삼각형
            b.Quad(b0 - right, b0 + right, m + right * 0.7f, m - right * 0.7f,
                new[] { new Vector2(u0, 0f), new Vector2(u0 + 0.2f, 0f), new Vector2(u0 + 0.17f, 0.55f), new Vector2(u0 + 0.03f, 0.55f) });
            b.Tri(m - right * 0.7f, m + right * 0.7f, t, new Vector2(u0 + 0.03f, 0.55f), new Vector2(u0 + 0.17f, 0.55f), new Vector2(u0 + 0.1f, 1f));
        }
        return b.ToMesh("GrassTuft");
    }

    // 사진 톤 풀잎 텍스처: 밑동은 짙고 끝은 밝은 녹색, 잎맥 줄무늬·얼룩 (잔디 사진 색상에서 뽑은 팔레트)
    static Material GrassMaterial()
    {
        const string texPath = "Assets/WhackAMole/Realism/Foliage/GrassBlade.png";
        const string matPath = "Assets/WhackAMole/Realism/Foliage/GrassBlade.mat";
        if (!File.Exists(texPath))
        {
            int W = 128, H = 256;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true);
            var rnd = new System.Random(9);
            Color baseC = new Color(0.10f, 0.20f, 0.05f), midC = new Color(0.26f, 0.42f, 0.12f), tipC = new Color(0.52f, 0.60f, 0.26f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = y / (float)(H - 1);
                    Color c = v < 0.5f ? Color.Lerp(baseC, midC, v * 2f) : Color.Lerp(midC, tipC, (v - 0.5f) * 2f);
                    float streak = Mathf.PerlinNoise(x * 0.45f, y * 0.02f) - 0.5f;          // 세로 잎맥
                    float blotch = Mathf.PerlinNoise(x * 0.05f + 3f, y * 0.04f) - 0.5f;     // 얼룩
                    c *= 1f + streak * 0.35f + blotch * 0.25f + ((float)rnd.NextDouble() - 0.5f) * 0.06f;
                    if (v > 0.9f && Mathf.PerlinNoise(x * 0.3f, 7f) > 0.6f) c = Color.Lerp(c, new Color(0.62f, 0.55f, 0.3f), 0.5f); // 마른 끝
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            File.WriteAllBytes(texPath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath);
        }
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, matPath); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.2f);
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.doubleSidedGI = true;
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ───────────────────────── 메시 조립 ─────────────────────────
    class MB
    {
        readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> t = new List<int>();
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2[] u)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d); uv.AddRange(u);
            t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); uv.Add(ua); uv.Add(ub); uv.Add(uc);
            t.AddRange(new[] { i, i + 2, i + 1 });
        }
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            // 양면 재질이라 법선은 위쪽을 향하게 정리 (빛을 고르게 받도록)
            var n = m.normals;
            for (int i = 0; i < n.Length; i++) if (n[i].y < 0f) n[i] = -n[i];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.Lerp(n[i], Vector3.up, 0.35f).normalized;
            m.normals = n;
            return m;
        }
    }

    static Mesh Save(Mesh src, string name)
    {
        string path = $"{MeshDir}/{name}.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { src.name = name; AssetDatabase.CreateAsset(src, path); return src; }
        mesh.Clear();
        mesh.SetVertices(src.vertices); mesh.SetUVs(0, src.uv); mesh.SetTriangles(src.triangles, 0);
        mesh.normals = src.normals; mesh.tangents = src.tangents; mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }
}

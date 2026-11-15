// 로우폴리 스타일 나무 메시를 절차적으로 만들어 에셋으로 저장하는 에디터 스크립트
//  - 활엽수 4종: 뿌리 쪽이 퍼진 줄기 + 가지 + 울퉁불퉁한 잎 덩어리 여러 개 (두 가지 초록)
//  - 침엽수 3종: 줄기 + 겹겹이 쌓인 원뿔 잎
//  - 면마다 법선을 따로 둔 플랫 셰이딩이라 기본 구 도형보다 입체감이 또렷합니다.
// 메시는 Assets/WhackAMole/Meshes 에 저장되며, WhackEnvironment(Build Environment)가 사용합니다.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class WhackTreeModels
{
    const string MeshDir = "Assets/WhackAMole/Meshes";
    public const int BroadleafCount = 4;
    public const int PineCount = 3;

    // 서브메시 순서: 0 = 줄기, 1 = 잎(밝은), 2 = 잎(진한)  (침엽수는 1 = 잎만 사용, 2는 비어 있음)
    public static Mesh[] BuildAll(out Mesh[] pines)
    {
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/WhackAMole", "Meshes");
        var broad = new Mesh[BroadleafCount];
        for (int i = 0; i < BroadleafCount; i++) broad[i] = Save(Broadleaf(1000 + i * 17), $"Tree_Broadleaf_{i}");
        pines = new Mesh[PineCount];
        for (int i = 0; i < PineCount; i++) pines[i] = Save(Pine(2000 + i * 31), $"Tree_Pine_{i}");
        return broad;
    }

    // ───────────────────────── 활엽수 ─────────────────────────
    static Builder Broadleaf(int seed)
    {
        var rng = new System.Random(seed);
        var b = new Builder(3);
        float h = R(rng, 2.3f, 3.1f);          // 줄기 높이
        float r0 = R(rng, 0.17f, 0.22f);        // 줄기 굵기
        Vector3 lean = new Vector3(R(rng, -0.25f, 0.25f), 0f, R(rng, -0.25f, 0.25f));

        // 줄기 (뿌리 쪽이 넓게 퍼지고 살짝 휘어짐)
        var pts = new List<Vector3>(); var radii = new List<float>();
        for (int i = 0; i <= 6; i++)
        {
            float t = i / 6f;
            pts.Add(new Vector3(0f, h * t, 0f) + lean * (t * t));
            radii.Add(Mathf.Lerp(r0, r0 * 0.55f, t) * (1f + 0.9f * Mathf.Pow(1f - t, 5f)));
        }
        b.Tube(0, pts, radii, 8);
        Vector3 top = pts[pts.Count - 1];

        // 잎 덩어리: 꼭대기 큰 덩어리 + 둘레 작은 덩어리들
        float R0 = R(rng, 1.25f, 1.6f);
        var blobs = new List<(Vector3 c, float r)> { (top + Vector3.up * R0 * 0.55f, R0) };
        int ring = rng.Next(4, 6);
        float phase = R(rng, 0f, Mathf.PI * 2f);
        for (int i = 0; i < ring; i++)
        {
            float a = phase + i * Mathf.PI * 2f / ring + R(rng, -0.3f, 0.3f);
            float rr = R0 * R(rng, 0.55f, 0.75f);
            Vector3 c = top + new Vector3(Mathf.Cos(a) * R0 * 0.85f, R(rng, -0.15f, 0.35f) * R0, Mathf.Sin(a) * R0 * 0.85f);
            blobs.Add((c, rr));
            // 줄기에서 잎 덩어리로 뻗는 가지
            Vector3 from = Vector3.Lerp(pts[3], top, R(rng, 0.2f, 0.8f));
            Vector3 to = Vector3.Lerp(from, c, 0.75f);
            b.Tube(0, new List<Vector3> { from, Vector3.Lerp(from, to, 0.5f) + Vector3.up * 0.1f, to }, new List<float> { r0 * 0.45f, r0 * 0.32f, r0 * 0.18f }, 6);
        }
        blobs.Add((top + new Vector3(R(rng, -0.3f, 0.3f), R0 * 1.15f, R(rng, -0.3f, 0.3f)), R0 * 0.6f));

        for (int i = 0; i < blobs.Count; i++)
            b.Blob(i % 3 == 1 ? 2 : 1, blobs[i].c, blobs[i].r, new Vector3(1f, 0.82f, 1f), 2, seed * 0.37f + i * 3.1f, 0.22f);
        return b;
    }

    // ───────────────────────── 침엽수 ─────────────────────────
    static Builder Pine(int seed)
    {
        var rng = new System.Random(seed);
        var b = new Builder(3);
        float trunkH = R(rng, 0.9f, 1.3f);
        float r0 = R(rng, 0.14f, 0.18f);
        b.Tube(0, new List<Vector3> { Vector3.zero, Vector3.up * trunkH * 0.5f, Vector3.up * (trunkH + 0.4f) },
            new List<float> { r0 * 1.5f, r0, r0 * 0.8f }, 7);

        int tiers = rng.Next(4, 6);
        float baseR = R(rng, 1.4f, 1.8f);
        float y = trunkH;
        for (int i = 0; i < tiers; i++)
        {
            float t = i / (float)(tiers - 1);
            float rad = Mathf.Lerp(baseR, baseR * 0.32f, t);
            float height = Mathf.Lerp(1.5f, 0.9f, t) * (baseR / 1.6f);
            b.Cone(1, new Vector3(0f, y, 0f), rad, height, 9, seed * 0.21f + i * 1.7f, 0.18f);
            y += height * 0.48f;
        }
        return b;
    }

    static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

    static Mesh Save(Builder b, string name)
    {
        string path = $"{MeshDir}/{name}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        b.Fill(mesh);
        mesh.name = name;
        if (isNew) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // ───────────────────────── 메시 조립기 (플랫 셰이딩) ─────────────────────────
    class Builder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<int>[] subs;

        public Builder(int subCount)
        {
            subs = new List<int>[subCount];
            for (int i = 0; i < subCount; i++) subs[i] = new List<int>();
        }

        // 바깥 방향(outDir)을 향하도록 감기 순서를 맞춰 삼각형 추가
        void Tri(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 outDir)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outDir) < 0f) { Vector3 t = b; b = c; c = t; }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            subs[s].Add(i); subs[s].Add(i + 1); subs[s].Add(i + 2);
        }

        // 점들을 따라 굵기가 변하는 관 (줄기·가지)
        public void Tube(int s, List<Vector3> pts, List<float> radii, int sides)
        {
            var rings = new List<Vector3[]>();
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 dir = i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1];
                dir.Normalize();
                Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
                Vector3 v = Vector3.Cross(dir, u);
                var ring = new Vector3[sides];
                for (int k = 0; k < sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    ring[k] = pts[i] + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radii[i];
                }
                rings.Add(ring);
            }
            for (int i = 0; i < rings.Count - 1; i++)
            {
                Vector3 axis = (pts[i] + pts[i + 1]) * 0.5f;
                for (int k = 0; k < sides; k++)
                {
                    int k1 = (k + 1) % sides;
                    Vector3 a = rings[i][k], b = rings[i][k1], c = rings[i + 1][k], d = rings[i + 1][k1];
                    Vector3 o = (a + b + c + d) * 0.25f - axis;
                    Tri(s, a, b, c, o); Tri(s, b, d, c, o);
                }
            }
            // 위쪽 마개
            Vector3 tip = pts[pts.Count - 1];
            Vector3[] last = rings[rings.Count - 1];
            Vector3 tipDir = (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;
            for (int k = 0; k < sides; k++) Tri(s, last[k], last[(k + 1) % sides], tip, tipDir);
        }

        // 울퉁불퉁한 잎 덩어리 (icosphere + 노이즈)
        public void Blob(int s, Vector3 center, float radius, Vector3 squash, int subdiv, float seed, float rough)
        {
            Icosphere(subdiv, out List<Vector3> dirs, out List<int> tris);
            var pos = new Vector3[dirs.Count];
            for (int i = 0; i < dirs.Count; i++)
            {
                Vector3 d = dirs[i];
                float n = Mathf.PerlinNoise(d.x * 1.9f + seed, d.y * 1.9f + d.z * 1.3f + seed * 0.5f) - 0.5f;
                pos[i] = center + Vector3.Scale(d * radius * (1f + n * rough * 2f), squash);
            }
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = pos[tris[i]], b = pos[tris[i + 1]], c = pos[tris[i + 2]];
                Tri(s, a, b, c, (a + b + c) / 3f - center);
            }
        }

        // 침엽수 한 층: 아래 원(약간 울퉁불퉁) → 꼭짓점, 아래 면 포함
        public void Cone(int s, Vector3 basePos, float radius, float height, int sides, float seed, float rough)
        {
            Vector3 apex = basePos + Vector3.up * height;
            var ring = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = k * Mathf.PI * 2f / sides + seed;
                float r = radius * (1f + (Mathf.PerlinNoise(k * 0.9f + seed, seed) - 0.5f) * rough * 2f);
                ring[k] = basePos + new Vector3(Mathf.Cos(a) * r, (Mathf.PerlinNoise(seed, k * 1.3f) - 0.5f) * height * 0.15f, Mathf.Sin(a) * r);
            }
            for (int k = 0; k < sides; k++)
            {
                Vector3 a = ring[k], b = ring[(k + 1) % sides];
                Vector3 mid = (a + b) * 0.5f;
                Tri(s, a, b, apex, new Vector3(mid.x - basePos.x, 0.3f, mid.z - basePos.z));
                Tri(s, a, b, basePos + Vector3.up * height * 0.1f, Vector3.down);
            }
        }

        static void Icosphere(int subdiv, out List<Vector3> v, out List<int> f)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            f = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int s = 0; s < subdiv; s++)
            {
                var cache = new Dictionary<long, int>();
                var nf = new List<int>();
                var vv = v;
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int idx)) return idx;
                    vv.Add(((vv[a] + vv[b]) * 0.5f).normalized);
                    cache[key] = vv.Count - 1;
                    return vv.Count - 1;
                }
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
        }

        public void Fill(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) mesh.SetTriangles(subs[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}

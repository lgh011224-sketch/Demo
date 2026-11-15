// 두더지 구멍(흙더미) 메시를 절차적으로 만드는 에디터 스크립트
// 사용법: 상단 메뉴 Tools > Whack-A-Mole > Rebuild Mole Holes
//  - 탁자 구멍 9개: 검은 원판 대신, 테두리가 불룩 솟고 안쪽이 어둡게 파인 흙구멍
//  - 배경 두더지 흙더미도 같은 메시를 크게 키워 사용 (WhackEnvironment)
// 형태: 단면(바깥 → 안쪽) 윤곽을 한 바퀴 돌린 회전체 + 노이즈로 울퉁불퉁하게
//   서브메시 0 = 흙더미 (Dirt.mat: park_dirt),  1 = 구멍 안쪽·바닥 (HolePit.mat: 아주 어두운 흙)
// 크기 기준(배율 1): 바깥 지름 약 18 cm, 안쪽 구멍 지름 약 11 cm (두더지 폭 10 cm), 테두리 높이 약 2.6 cm
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WhackHoleModels
{
    const string MeshDir = "Assets/WhackAMole/Meshes";
    const int Variants = 3;

    // 단면 윤곽 (반지름 m, 높이 m) – 바깥 바닥 → 테두리 꼭대기 → 안쪽 벽 → 구멍 바닥
    // 구멍 바닥은 탁자 윗면(높이 0)보다 살짝 위에 둬야 보임 (탁자 상판이 아래를 가리므로).
    // 두더지는 바닥 원판 아래(탁자 속)에서 올라와 원판을 뚫고 나오는 것처럼 보임.
    static readonly Vector2[] Profile =
    {
        new Vector2(0.088f, 0.000f), new Vector2(0.080f, 0.005f), new Vector2(0.073f, 0.012f),
        new Vector2(0.066f, 0.017f), new Vector2(0.061f, 0.016f), new Vector2(0.058f, 0.011f),   // ← 여기까지 흙더미
        new Vector2(0.0565f, 0.006f), new Vector2(0.056f, 0.0028f), new Vector2(0.0f, 0.0028f),  // ← 구멍 안쪽 벽·바닥 (어두움)
    };
    const int PitStart = 5; // 이 점부터 구멍 안쪽(서브메시 1)

    [MenuItem("Tools/Whack-A-Mole/Rebuild Mole Holes")]
    public static void RebuildMenu()
    {
        WhackGameManager gm = Object.FindFirstObjectByType<WhackGameManager>();
        if (gm != null) RestyleTableHoles(gm);
    }

    public static Mesh[] GetMeshes()
    {
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/WhackAMole", "Meshes");
        var meshes = new Mesh[Variants];
        for (int i = 0; i < Variants; i++) meshes[i] = Save(Build(500 + i * 37), $"MoleHole_{i}");
        return meshes;
    }

    public static Material[] GetMaterials()
    {
        var dirt = AssetDatabase.LoadAssetAtPath<Material>("Assets/WhackAMole/Materials/Env/Dirt.mat");
        const string pitPath = "Assets/WhackAMole/Materials/Env/HolePit.mat";
        var pit = AssetDatabase.LoadAssetAtPath<Material>(pitPath);
        if (pit == null)
        {
            pit = new Material(dirt != null ? dirt : new Material(Shader.Find("Universal Render Pipeline/Lit")));
            AssetDatabase.CreateAsset(pit, pitPath);
        }
        const string tex = "Assets/WhackAMole/External/PolyHaven/Textures/red_dirt_mud_01/red_dirt_mud_01";
        var diff = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_diff_2k.jpg");
        var nor = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_nor_gl_2k.jpg");
        if (diff != null) pit.SetTexture("_BaseMap", diff);
        if (nor != null) { pit.SetTexture("_BumpMap", nor); pit.EnableKeyword("_NORMALMAP"); }
        pit.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
        pit.SetColor("_BaseColor", new Color(0.07f, 0.05f, 0.035f)); // 빛이 안 드는 깊은 구멍
        pit.SetFloat("_Smoothness", 0.08f);
        EditorUtility.SetDirty(pit);
        return new[] { dirt, pit };
    }

    // 탁자 위 Hole_rc 원판 → 흙구멍
    public static void RestyleTableHoles(WhackGameManager gm)
    {
        Transform root = gm.transform.parent; // WhackAMole
        Mesh[] meshes = GetMeshes();
        Material[] mats = GetMaterials();
        var rng = new System.Random(77);
        int n = 0;
        foreach (Transform t in root)
        {
            if (!t.name.StartsWith("Hole_")) continue;
            Undo.RecordObject(t, "Hole");
            var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
            Undo.RecordObject(mf, "Hole"); Undo.RecordObject(mr, "Hole");
            Vector3 p = t.position; p.y = 0.75f; // 탁자 윗면
            t.position = p;
            t.localScale = Vector3.one;
            t.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            mf.sharedMesh = meshes[n % meshes.Length];
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            n++;
        }
        // 두더지가 흙 테두리 위로 올라오도록 높이 조정 (앞발이 테두리에 걸침)
        if (gm.tableMoles != null)
            foreach (Mole m in gm.tableMoles)
                if (m != null) { Undo.RecordObject(m, "Mole height"); m.upHeight = 0.058f; m.downHeight = -0.1f; EditorUtility.SetDirty(m); }
        EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
    }

    // ───────────────────────── 메시 생성 ─────────────────────────
    static Mesh Build(int seed)
    {
        const int seg = 48;
        float s = seed * 0.173f;
        var verts = new List<Vector3>(); var uvs = new List<Vector2>();
        var mound = new List<int>(); var pitTris = new List<int>();
        int rows = Profile.Length;

        // 단면 누적 길이 (v 좌표)
        var vlen = new float[rows];
        for (int i = 1; i < rows; i++) vlen[i] = vlen[i - 1] + Vector2.Distance(Profile[i], Profile[i - 1]);

        for (int k = 0; k <= seg; k++)
        {
            float a = k * Mathf.PI * 2f / seg;
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            // 둘레 방향 노이즈 (이음매에서 이어지도록 원 위 좌표로 샘플)
            float nOuter = Mathf.PerlinNoise(ca * 1.3f + s, sa * 1.3f + s) - 0.5f;
            float nFine = Mathf.PerlinNoise(ca * 4f + s * 2f, sa * 4f + s * 3f) - 0.5f;
            for (int i = 0; i < rows; i++)
            {
                Vector2 pr = Profile[i];
                bool inner = i >= PitStart + 1;            // 구멍 안쪽은 둥글게 유지 (두더지가 통과)
                float rNoise = inner ? 0f : (nOuter * 0.22f + nFine * 0.08f) * (i <= 3 ? 1f : 0.5f);
                float hNoise = inner || i == 0 ? 0f : (nOuter * 0.5f + nFine * 0.6f) * 0.012f;
                float r = pr.x * (1f + rNoise);
                float y = pr.y + hNoise;
                if (i == rows - 1) r = 0f;
                verts.Add(new Vector3(ca * r, y, sa * r));
                uvs.Add(new Vector2(a * 0.075f * 4f, vlen[i] * 4f));
            }
        }
        for (int k = 0; k < seg; k++)
        {
            for (int i = 0; i < rows - 1; i++)
            {
                int a = k * rows + i, b = a + 1, c = (k + 1) * rows + i, d = c + 1;
                // 단면 접선 → 바깥(윗면) 방향 법선: (tx, ty) → 반지름 성분 ty, 높이 성분 -tx
                Vector2 t2 = (Profile[i + 1] - Profile[i]).normalized;
                Vector3 mid = (verts[a] + verts[b] + verts[c] + verts[d]) * 0.25f;
                Vector3 radial = new Vector3(mid.x, 0f, mid.z).normalized;
                Vector3 outDir = radial * t2.y + Vector3.up * -t2.x;
                var list = i >= PitStart ? pitTris : mound;
                AddTri(list, verts, a, c, b, outDir);
                AddTri(list, verts, b, c, d, outDir);
            }
        }
        var mesh = new Mesh { name = "MoleHole" };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(mound, 0); mesh.SetTriangles(pitTris, 1);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    static void AddTri(List<int> list, List<Vector3> v, int a, int b, int c, Vector3 outDir)
    {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (n.sqrMagnitude < 1e-14f) return;
        if (Vector3.Dot(n, outDir) < 0f) { int t = b; b = c; c = t; }
        list.Add(a); list.Add(b); list.Add(c);
    }

    static Mesh Save(Mesh src, string name)
    {
        string path = $"{MeshDir}/{name}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { src.name = name; AssetDatabase.CreateAsset(src, path); return src; }
        mesh.Clear();
        mesh.SetVertices(src.vertices); mesh.SetUVs(0, src.uv);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(src.GetTriangles(0), 0); mesh.SetTriangles(src.GetTriangles(1), 1);
        mesh.normals = src.normals; mesh.tangents = src.tangents; mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }
}

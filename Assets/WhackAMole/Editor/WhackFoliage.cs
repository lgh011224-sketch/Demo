// 실사 식물(나무 · 덤불 · 풀 · 꽃) 가져오기 설정 + 배치
// 에셋: Assets/WhackAMole/External/PolyHaven/Models (Poly Haven, CC0)
//  - 잎 텍스처: 색(diff) + 흑백 알파(alpha)를 합쳐 RGBA로 만들고, URP Lit 알파 컷아웃 + 양면 렌더링
//  - 나무: 원본이 매우 무거워(수십만~수백만 삼각형) Unity 6 Mesh LOD를 자동 생성하고,
//          거리별로 정해 둔 삼각형 예산에 맞는 LOD 단계를 그루마다 고정(forceMeshLod)
//  - 덤불·꽃: FBX 안에 여러 변형이 줄지어 있으므로 하나씩 떼어 무작위 배치
// WhackEnvironment(Build Environment)가 PlaceTrees / PlacePlants를 호출합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class WhackFoliage
{
    const string Root = "Assets/WhackAMole/External/PolyHaven/Models";
    const string MatDir = "Assets/WhackAMole/Realism/Foliage";

    // 이 반지름·통로 안에는 아무것도 두지 않음 (플레이 영역)
    static bool Blocked(float x, float z) => (Mathf.Abs(x) < 2.6f && z > -2f && z < 5.2f) || (Mathf.Abs(x) < 1.1f && z > -1.5f && z < 5.5f);

    public struct Variant { public Mesh mesh; public Material[] mats; public Matrix4x4 local; public Bounds bounds; public long tris; }

    // ───────────────────────── 가져오기 · 재질 ─────────────────────────
    public static void PrepareAll()
    {
        if (!AssetDatabase.IsValidFolder("Assets/WhackAMole/Realism")) AssetDatabase.CreateFolder("Assets/WhackAMole", "Realism");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/WhackAMole/Realism", "Foliage");
        foreach (string id in new[] { "shrub_02", "shrub_04", "fern_02", "flower_gazania", "dandelion_01", "nettle_plant" })
            SetupModel(id, false, new Dictionary<string, Material> { { id, Foliage(id, $"{id}_diff_2k.jpg", $"{id}_alpha_2k.png", null, id == "shrub_02" ? 0.45f : 0.4f) } });

        SetupModel("island_tree_02", true, new Dictionary<string, Material>
        {
            { "island_tree_02", Opaque("island_tree_02", "island_tree_02_diff_2k.jpg", null) },
            { "island_tree_02_branches", Opaque("island_tree_02", "island_tree_02_branches_diff_2k.png", "island_tree_02_branches_nor_gl_2k.png") },
            { "island_tree_02_leaves", Foliage("island_tree_02", "island_tree_02_leaves_diff_2k.png", "island_tree_02_leaves_alpha_2k.png", "island_tree_02_leaves_nor_gl_2k.png", 0.45f) },
        });
        SetupModel("jacaranda_tree", true, new Dictionary<string, Material>
        {
            { "jacaranda_tree_trunk", Opaque("jacaranda_tree", "jacaranda_tree_trunk_diff_2k.png", "jacaranda_tree_trunk_nor_gl_2k.png") },
            { "jacaranda_tree_branches", Opaque("jacaranda_tree", "jacaranda_tree_branches_diff_2k.png", "jacaranda_tree_branches_nor_gl_2k.png") },
            { "jacaranda_tree_leaves", Foliage("jacaranda_tree", "jacaranda_tree_leaves_diff_2k.png", "jacaranda_tree_leaves_alpha_2k.png", "jacaranda_tree_leaves_nor_gl_2k.png", 0.45f) },
        });
    }

    static string ModelPath(string id) => $"{Root}/{id}/{id}_2k.fbx";
    static string Tex(string id, string file) => $"{Root}/{id}/textures/{file}";

    static void SetupModel(string id, bool meshLods, Dictionary<string, Material> remap)
    {
        var imp = AssetImporter.GetAtPath(ModelPath(id)) as ModelImporter;
        if (imp == null) { Debug.LogWarning("[식물] 모델 없음 " + id); return; }
        bool changed = false;
        foreach (var kv in remap)
        {
            var key = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
            if (imp.GetExternalObjectMap().TryGetValue(key, out Object cur) && cur == kv.Value) continue;
            imp.RemoveRemap(key); imp.AddRemap(key, kv.Value); changed = true;
        }
        if (imp.generateMeshLods != meshLods) { imp.generateMeshLods = meshLods; changed = true; }
        if (imp.importAnimation) { imp.importAnimation = false; changed = true; }
        if (imp.importCameras || imp.importLights) { imp.importCameras = false; imp.importLights = false; changed = true; }
        if (changed) imp.SaveAndReimport();
    }

    // 잎·꽃: 알파 컷아웃 + 양면
    static Material Foliage(string id, string diff, string alpha, string normal, float cutoff)
    {
        string matPath = $"{MatDir}/{Path.GetFileNameWithoutExtension(diff).Replace("_diff_2k", "")}.mat";
        var m = LoadOrNew(matPath);
        string rgba = Tex(id, Path.GetFileNameWithoutExtension(diff).Replace("_diff_2k", "") + "_rgba_2k.png");
        if (!File.Exists(rgba)) CombineAlpha(Tex(id, diff), Tex(id, alpha), rgba);
        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(rgba);
        m.SetTexture("_BaseMap", baseTex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", cutoff); m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", (float)CullMode.Off);       // 잎 뒷면도 보이게
        m.SetFloat("_Smoothness", 0.15f); m.SetFloat("_Metallic", 0f);
        m.renderQueue = (int)RenderQueue.AlphaTest;
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.doubleSidedGI = true;
        m.enableInstancing = true;
        SetNormal(m, normal != null ? Tex(id, normal) : null);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Opaque(string id, string diff, string normal)
    {
        string matPath = $"{MatDir}/{Path.GetFileNameWithoutExtension(diff).Replace("_diff_2k", "")}.mat";
        var m = LoadOrNew(matPath);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex(id, diff)));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.1f); m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        SetNormal(m, normal != null ? Tex(id, normal) : null);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetNormal(Material m, string path)
    {
        if (path == null || !File.Exists(path)) return;
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        m.EnableKeyword("_NORMALMAP");
    }

    static Material LoadOrNew(string path)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    // 색 텍스처 RGB + 알파 이미지(흑백) → RGBA PNG
    public static void CombineAlpha(string diffPath, string alphaPath, string outPath)
    {
        // 주의: LoadImage는 JPG를 읽으면 형식을 알파 없는 RGB24로 바꿔 버림 → 읽은 뒤 RGBA32 텍스처로 옮겨 담아야 알파가 저장됨
        var dSrc = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var a = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        dSrc.LoadImage(File.ReadAllBytes(diffPath));
        a.LoadImage(File.ReadAllBytes(alphaPath));
        var d = new Texture2D(dSrc.width, dSrc.height, TextureFormat.RGBA32, false);
        d.SetPixels32(dSrc.GetPixels32()); d.Apply();
        Object.DestroyImmediate(dSrc);
        if (a.width != d.width || a.height != d.height)
        {
            var rt = RenderTexture.GetTemporary(d.width, d.height, 0);
            Graphics.Blit(a, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            a = new Texture2D(d.width, d.height, TextureFormat.RGBA32, false);
            a.ReadPixels(new Rect(0, 0, d.width, d.height), 0, 0); a.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        }
        Color32[] dp = d.GetPixels32(), ap = a.GetPixels32();
        for (int i = 0; i < dp.Length; i++) dp[i].a = ap[i].r;
        d.SetPixels32(dp); d.Apply();
        File.WriteAllBytes(outPath, d.EncodeToPNG());
        Object.DestroyImmediate(d); Object.DestroyImmediate(a);
        AssetDatabase.ImportAsset(outPath);
        var imp = AssetImporter.GetAtPath(outPath) as TextureImporter;
        if (imp != null)
        {
            imp.alphaIsTransparency = true; imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.mipMapsPreserveCoverage = true; imp.alphaTestReferenceValue = 0.45f; // 멀리서도 잎이 사라지지 않게
            imp.maxTextureSize = 2048; imp.SaveAndReimport();
        }
    }

    // ───────────────────────── 변형 추출 ─────────────────────────
    public static List<Variant> Variants(string id)
    {
        var list = new List<Variant>();
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(id));
        if (go == null) return list;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            // 루트 오브젝트의 회전·스케일(FBX 단위 변환)까지 포함한 변환 (에셋 루트는 원점)
            Matrix4x4 local = mf.transform.localToWorldMatrix;
            Bounds mb = mf.sharedMesh.bounds;
            var pts = new List<Vector3>();
            for (int i = 0; i < 8; i++)
                pts.Add(local.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            var b = GeometryUtility.CalculateBounds(pts.ToArray(), Matrix4x4.identity);
            long tris = 0; for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tris += mf.sharedMesh.GetIndexCount(s) / 3;
            list.Add(new Variant { mesh = mf.sharedMesh, mats = mf.GetComponent<MeshRenderer>().sharedMaterials, local = local, bounds = b, tris = tris });
        }
        return list;
    }

    // 바닥 중심이 원점에 오도록 감싼 인스턴스 생성
    static GameObject Spawn(Transform parent, string name, Variant v, Vector3 pos, float yaw, float scale, bool shadows)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = pos;
        holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        holder.transform.localScale = Vector3.one * scale;
        holder.isStatic = true;
        var g = new GameObject("Mesh");
        g.transform.SetParent(holder.transform, false);
        g.transform.localPosition = (Vector3)v.local.GetColumn(3) - new Vector3(v.bounds.center.x, v.bounds.min.y, v.bounds.center.z);
        g.transform.localRotation = v.local.rotation;
        g.transform.localScale = v.local.lossyScale;
        g.AddComponent<MeshFilter>().sharedMesh = v.mesh;
        var mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterials = v.mats;
        mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        g.isStatic = true;
        return holder;
    }

    // ───────────────────────── 나무 (임포스터) ─────────────────────────
    // 실사 나무 원본은 그루당 100만~400만 삼각형이고, 자동 Mesh LOD는 잎 카드를 지워 버림.
    // 플레이어는 부스 앞(원점)에 서 있으므로: 각 나무를 실제 자리에 최고 품질로 놓고 플레이어 눈 위치에서
    // 한 번 렌더링 → 그 사진을 플레이어를 향한 평면(임포스터)으로 세움. 플레이 위치에서는 원본과 똑같이 보임.
    static readonly Vector3 Eye = new Vector3(0f, 1.6f, -0.1f);
    const string ImpDir = "Assets/WhackAMole/Realism/TreeImpostors";
    const int CaptureLayer = 31;

    public static string PlaceTrees(Transform parent, System.Random rng)
    {
        if (!AssetDatabase.IsValidFolder(ImpDir)) AssetDatabase.CreateFolder("Assets/WhackAMole/Realism", "TreeImpostors");
        var plan = new (string id, int count, float rMin, float rMax, float sMin, float sMax, float spacing, int res)[]
        {
            ("island_tree_02", 16, 10.0f, 15f, 1.7f, 2.4f, 3.4f, 1024),
            ("jacaranda_tree", 7, 14.5f, 24f, 0.40f, 0.55f, 7.5f, 1024),
            ("island_tree_02", 14, 22f, 32f, 2.2f, 3.0f, 5f, 512),
        };
        var used = new List<Vector2>();
        int n = 0;
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        var camGo = new GameObject("__ImpostorCam");
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << CaptureLayer;
        cam.nearClipPlane = 0.5f; cam.farClipPlane = 200f;
        cam.allowHDR = false; cam.allowMSAA = true;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        try
        {
            foreach (var p in plan)
            {
                var vs = Variants(p.id);
                if (vs.Count == 0) continue;
                var v = vs[0];
                for (int i = 0; i < p.count; i++)
                {
                    Vector3 pos = Vector3.zero; bool ok = false;
                    for (int t = 0; t < 80 && !ok; t++)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Lerp(p.rMin, p.rMax, (float)rng.NextDouble());
                        pos = new Vector3(Mathf.Sin(a) * r, 0f, 1.5f + Mathf.Cos(a) * r);
                        ok = used.All(u => Vector2.Distance(u, new Vector2(pos.x, pos.z)) > p.spacing);
                    }
                    if (!ok) continue;
                    used.Add(new Vector2(pos.x, pos.z));
                    float yaw = (float)rng.NextDouble() * 360f, scale = Mathf.Lerp(p.sMin, p.sMax, (float)rng.NextDouble());
                    Impostor(parent, cam, v, $"Tree_{n}", pos, yaw, scale, p.res);
                    n++;
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(camGo);
            RenderSettings.fog = fog;
        }
        AssetDatabase.SaveAssets();
        return $"tree impostors={n}";
    }

    static void Impostor(Transform parent, Camera cam, Variant v, string name, Vector3 pos, float yaw, float scale, int res)
    {
        // 1) 원본 나무를 실제 자리에 최고 품질로 임시 배치
        var temp = Spawn(parent, "__temp", v, pos, yaw, scale, true);
        foreach (var t in temp.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CaptureLayer;
        var mr = temp.GetComponentInChildren<MeshRenderer>();
        ForceLod(mr, 0);
        Bounds b = mr.bounds;

        // 2) 플레이어 눈에서 나무 전체가 들어오게 촬영
        Vector3 toC = b.center - Eye; float d = toC.magnitude;
        float radius = b.extents.magnitude * 1.02f;
        float fov = 2f * Mathf.Atan(radius / d) * Mathf.Rad2Deg;
        cam.transform.position = Eye;
        cam.transform.rotation = Quaternion.LookRotation(toC);
        cam.fieldOfView = fov; cam.aspect = 1f;
        var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(res, res, RenderTextureFormat.ARGB32, 24) { msaaSamples = 8, sRGB = true });
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, true, false);
        tex.ReadPixels(new Rect(0, 0, res, res), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(temp);

        // 반투명 가장자리 색이 검게 번지지 않도록 알파로 나눠 줌 (프리멀티 → 스트레이트)
        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            byte a = px[i].a;
            if (a > 0 && a < 255) { px[i].r = (byte)Mathf.Min(255, px[i].r * 255 / a); px[i].g = (byte)Mathf.Min(255, px[i].g * 255 / a); px[i].b = (byte)Mathf.Min(255, px[i].b * 255 / a); }
        }
        tex.SetPixels32(px); tex.Apply();
        string texPath = $"{ImpDir}/{name}.png";
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        ti.alphaIsTransparency = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.5f;
        ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = res; ti.SaveAndReimport();

        // 3) 같은 시야각·거리로 플레이어를 향한 평면에 붙임 → 플레이 위치에서 원본과 똑같이 보임
        var mat = LoadOrNewShader($"{ImpDir}/{name}.mat", "Universal Render Pipeline/Unlit");
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.5f); mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        EditorUtility.SetDirty(mat);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        Vector3 fwd = toC.normalized;
        float size = 2f * d * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        quad.transform.position = Eye + fwd * d;
        quad.transform.rotation = Quaternion.LookRotation(fwd, cam.transform.up);
        quad.transform.localScale = new Vector3(size, size, 1f);
        var qr = quad.GetComponent<MeshRenderer>();
        qr.sharedMaterial = mat;
        qr.shadowCastingMode = ShadowCastingMode.Off;
        qr.receiveShadows = false;
        quad.isStatic = true;
    }

    static Material LoadOrNewShader(string path, string shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    // (예전 방식: LOD 고정 배치 – 잎이 사라지는 문제로 사용 안 함)
    public static string PlaceTreesMeshLod(Transform parent, System.Random rng)
    {
        var plan = new (string id, int count, float rMin, float rMax, float sMin, float sMax, long budget)[]
        {
            ("island_tree_02", 11, 10.2f, 14f, 1.6f, 2.2f, 70000),
            ("jacaranda_tree", 4, 15f, 21f, 0.42f, 0.52f, 160000),
        };
        long total = 0; int placed = 0;
        var used = new List<Vector2>();
        foreach (var p in plan)
        {
            var vs = Variants(p.id);
            if (vs.Count == 0) continue;
            var v = vs[0];
            short lod = PickLod(v.mesh, p.budget, out long lodTris);
            for (int i = 0; i < p.count; i++)
            {
                Vector3 pos = Vector3.zero; bool ok = false;
                for (int t = 0; t < 60 && !ok; t++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Lerp(p.rMin, p.rMax, (float)rng.NextDouble());
                    pos = new Vector3(Mathf.Sin(a) * r, 0f, 1.5f + Mathf.Cos(a) * r);
                    ok = used.All(u => Vector2.Distance(u, new Vector2(pos.x, pos.z)) > (p.id == "jacaranda_tree" ? 7f : 3.6f));
                }
                used.Add(new Vector2(pos.x, pos.z));
                var go = Spawn(parent, $"{p.id}_{i}", v, pos, (float)rng.NextDouble() * 360f, Mathf.Lerp(p.sMin, p.sMax, (float)rng.NextDouble()), true);
                ForceLod(go.GetComponentInChildren<MeshRenderer>(), lod);
                total += lodTris; placed++;
            }
        }
        return $"trees={placed} tris≈{total:N0}";
    }

    // 예산 이하가 되는 가장 정밀한 LOD 단계
    static short PickLod(Mesh mesh, long budget, out long tris)
    {
        int count = 1;
        var lc = typeof(Mesh).GetProperty("lodCount");
        if (lc != null) count = Convert.ToInt32(lc.GetValue(mesh));
        var getLod = typeof(Mesh).GetMethod("GetLod", new[] { typeof(int), typeof(int) });
        for (int l = 0; l < count; l++)
        {
            long t = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (getLod == null) { t += mesh.GetIndexCount(s) / 3; continue; }
                object range = getLod.Invoke(mesh, new object[] { s, l });
                var f = range.GetType().GetField("indexCount") ?? null;
                var pp = range.GetType().GetProperty("indexCount");
                t += Convert.ToInt64(f != null ? f.GetValue(range) : pp.GetValue(range)) / 3;
            }
            if (t <= budget || l == count - 1) { tris = t; return (short)l; }
        }
        tris = 0; return 0;
    }

    static void ForceLod(MeshRenderer mr, short lod)
    {
        var p = typeof(MeshRenderer).GetProperty("forceMeshLod");
        if (p != null) p.SetValue(mr, Convert.ChangeType(lod, p.PropertyType));
    }

    // ───────────────────────── 덤불 · 풀 · 꽃 ─────────────────────────
    public static string PlantsPlace(Transform parent, System.Random rng)
    {
        var shrubs = Variants("shrub_02");
        var ground = Variants("fern_02").Concat(Variants("shrub_04")).Concat(Variants("nettle_plant")).ToList();
        var flowers = Variants("flower_gazania").Concat(Variants("dandelion_01")).ToList();
        int n = 0; long tris = 0;

        // 큰 덤불: 울타리 안쪽 가장자리
        for (int i = 0; i < 22 && shrubs.Count > 0; i++)
        {
            float a = (i + (float)rng.NextDouble() * 0.6f) * Mathf.PI * 2f / 22f, r = 7.0f + (float)rng.NextDouble() * 0.9f;
            float x = Mathf.Sin(a) * r, z = 1.5f + Mathf.Cos(a) * r;
            if (Blocked(x, z)) continue;
            var v = shrubs[rng.Next(shrubs.Count)];
            Spawn(parent, $"Shrub_{n++}", v, new Vector3(x, 0f, z), (float)rng.NextDouble() * 360f, 0.8f + (float)rng.NextDouble() * 0.5f, true);
            tris += v.tris;
        }
        // 풀·고사리 무리, 꽃 무리: 울타리 쪽 띠 + 부스 양옆
        void Clusters(List<Variant> pool, int clusters, int perMin, int perMax, float sMin, float sMax, string label, bool shadows)
        {
            for (int c = 0; c < clusters && pool.Count > 0; c++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = rng.NextDouble() < 0.7 ? 5.8f + (float)rng.NextDouble() * 2.4f : 1.8f + (float)rng.NextDouble() * 2.2f;
                float cx = Mathf.Sin(a) * r, cz = 1.5f + Mathf.Cos(a) * r;
                if (Blocked(cx, cz)) { c--; if (rng.NextDouble() < 0.02) break; continue; }
                int k = rng.Next(perMin, perMax + 1);
                for (int j = 0; j < k; j++)
                {
                    float x = cx + ((float)rng.NextDouble() - 0.5f) * 0.9f, z = cz + ((float)rng.NextDouble() - 0.5f) * 0.9f;
                    if (Blocked(x, z)) continue;
                    var v = pool[rng.Next(pool.Count)];
                    Spawn(parent, $"{label}_{n++}", v, new Vector3(x, 0f, z), (float)rng.NextDouble() * 360f, Mathf.Lerp(sMin, sMax, (float)rng.NextDouble()), shadows);
                    tris += v.tris;
                }
            }
        }
        Clusters(ground, 30, 3, 5, 1.2f, 1.8f, "Ground", false);
        Clusters(flowers, 30, 4, 7, 1.2f, 1.6f, "Flower", false);
        return $"plants={n} tris≈{tris:N0}";
    }
}

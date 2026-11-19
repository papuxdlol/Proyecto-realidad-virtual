using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

[ScriptedImporter(1, "glb")]
public class GlbScriptedImporter : ScriptedImporter
{
    public float targetHeightMeters = 1.75f;

    [Serializable]
    private class GlbAccessor
    {
        public int bufferView;
        public int byteOffset;
        public int componentType;
        public int count;
        public string type;
    }

    [Serializable]
    private class GlbBufferView
    {
        public int buffer;
        public int byteOffset;
        public int byteLength;
        public int byteStride;
    }

    public override void OnImportAsset(AssetImportContext ctx)
    {
        byte[] data = File.ReadAllBytes(ctx.assetPath);
        if (data.Length < 28) return;

        uint magic = BitConverter.ToUInt32(data, 0);
        if (magic != 0x46546C67) // "glTF"
            return;

        uint jsonChunkLen = BitConverter.ToUInt32(data, 12);
        string json = Encoding.UTF8.GetString(data, 20, (int)jsonChunkLen);

        int binHeaderOffset = 20 + (int)jsonChunkLen;
        if (binHeaderOffset + 8 > data.Length) return;
        uint binChunkLen = BitConverter.ToUInt32(data, binHeaderOffset);
        int binStart = binHeaderOffset + 8;

        List<GlbAccessor> accessors = ParseAccessors(json);
        List<GlbBufferView> bufferViews = ParseBufferViews(json);

        int posAccIdx = ExtractAttributeIndex(json, "POSITION");
        int uvAccIdx = ExtractAttributeIndex(json, "TEXCOORD_0");
        int normAccIdx = ExtractAttributeIndex(json, "NORMAL");
        int indAccIdx = ExtractIndicesIndex(json);

        if (posAccIdx < 0 || posAccIdx >= accessors.Count || indAccIdx < 0 || indAccIdx >= accessors.Count)
            return;

        Vector3[] positions = ReadVec3Accessor(data, binStart, accessors[posAccIdx], bufferViews, flipX: true);
        Vector2[] uvs = (uvAccIdx >= 0 && uvAccIdx < accessors.Count)
            ? ReadVec2Accessor(data, binStart, accessors[uvAccIdx], bufferViews, flipV: true)
            : null;
        Vector3[] normals = (normAccIdx >= 0 && normAccIdx < accessors.Count)
            ? ReadVec3Accessor(data, binStart, accessors[normAccIdx], bufferViews, flipX: true)
            : null;
        int[] indices = ReadScalarIndices(data, binStart, accessors[indAccIdx], bufferViews);

        // Normalizar altura y colocar pivote exactamente en la planta de los pies (Y = 0)
        Bounds rawBounds = CalcularBounds(positions);
        float rawHeight = Mathf.Max(0.001f, rawBounds.size.y);
        float scale = targetHeightMeters > 0.01f ? (targetHeightMeters / rawHeight) : 1f;
        Vector3 offsetCenter = new Vector3(-rawBounds.center.x, -rawBounds.min.y, -rawBounds.center.z);

        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = (positions[i] + offsetCenter) * scale;
        }

        // Invertir el orden de los vértices de cada triángulo por el cambio de mano (glTF RH -> Unity LH)
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int tmp = indices[i + 1];
            indices[i + 1] = indices[i + 2];
            indices[i + 2] = tmp;
        }

        string baseName = Path.GetFileNameWithoutExtension(ctx.assetPath);
        Mesh mesh = new Mesh
        {
            name = baseName + "_Mesh",
            indexFormat = positions.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };

        mesh.vertices = positions;
        if (uvs != null && uvs.Length == positions.Length)
            mesh.uv = uvs;
        if (normals != null && normals.Length == positions.Length)
            mesh.normals = normals;

        mesh.triangles = indices;
        if (normals == null || normals.Length != positions.Length)
            mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Material mat = CrearMaterialConTexturas(ctx.assetPath, baseName);

        GameObject root = new GameObject(baseName);
        MeshFilter mf = root.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = root.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        ctx.AddObjectToAsset("mesh", mesh);
        ctx.AddObjectToAsset("material", mat);
        ctx.AddObjectToAsset("main", root);
        ctx.SetMainObject(root);
    }

    private static Material CrearMaterialConTexturas(string assetPath, string baseName)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(urpLit)
        {
            name = baseName + "_Mat"
        };
        mat.SetFloat("_Smoothness", 0.25f);

        string sourceDir = Path.GetDirectoryName(assetPath);
        string parentDir = sourceDir != null ? Path.GetDirectoryName(sourceDir) : null;
        string texDir = parentDir != null ? Path.Combine(parentDir, "textures") : null;

        if (!string.IsNullOrEmpty(texDir) && Directory.Exists(texDir))
        {
            string[] files = Directory.GetFiles(texDir);
            Texture2D baseTex = null;
            Texture2D normalTex = null;

            foreach (string f in files)
            {
                if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string unityPath = f.Replace('\\', '/');
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(unityPath);
                if (tex == null) continue;

                string fn = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                if (fn.Contains("_0_1") || fn.Contains("embedded_0") || fn.Contains("base") || baseTex == null)
                {
                    if (!fn.Contains("channe") && !fn.Contains("_2_0"))
                        baseTex = tex;
                }
                if (fn.Contains("_2_0") || fn.Contains("normal"))
                {
                    normalTex = tex;
                }
            }

            if (baseTex != null)
            {
                mat.SetTexture("_BaseMap", baseTex);
                mat.mainTexture = baseTex;
                mat.SetColor("_BaseColor", Color.white);
            }
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
            }
        }

        return mat;
    }

    private static Bounds CalcularBounds(Vector3[] pts)
    {
        if (pts == null || pts.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);

        Vector3 min = pts[0];
        Vector3 max = pts[0];
        for (int i = 1; i < pts.Length; i++)
        {
            Vector3 p = pts[i];
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.z < min.z) min.z = p.z;
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
            if (p.z > max.z) max.z = p.z;
        }
        Bounds b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    private static int ExtractAttributeIndex(string json, string attrName)
    {
        Match m = Regex.Match(json, "\"" + attrName + "\"\\s*:\\s*(\\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : -1;
    }

    private static int ExtractIndicesIndex(string json)
    {
        Match m = Regex.Match(json, "\"indices\"\\s*:\\s*(\\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : -1;
    }

    private static List<GlbAccessor> ParseAccessors(string json)
    {
        List<GlbAccessor> list = new List<GlbAccessor>();
        Match arrMatch = Regex.Match(json, "\"accessors\"\\s*:\\s*\\[(.*?)\\](?=,\\s*\"[a-zA-Z]|\\})", RegexOptions.Singleline);
        if (!arrMatch.Success) return list;

        foreach (Match objMatch in Regex.Matches(arrMatch.Groups[1].Value, "\\{[^{}]*\\}"))
        {
            string s = objMatch.Value;
            list.Add(new GlbAccessor
            {
                bufferView = MatchInt(s, "bufferView", 0),
                byteOffset = MatchInt(s, "byteOffset", 0),
                componentType = MatchInt(s, "componentType", 5126),
                count = MatchInt(s, "count", 0),
                type = MatchString(s, "type", "SCALAR")
            });
        }
        return list;
    }

    private static List<GlbBufferView> ParseBufferViews(string json)
    {
        List<GlbBufferView> list = new List<GlbBufferView>();
        Match arrMatch = Regex.Match(json, "\"bufferViews\"\\s*:\\s*\\[(.*?)\\](?=,\\s*\"[a-zA-Z]|\\})", RegexOptions.Singleline);
        if (!arrMatch.Success) return list;

        foreach (Match objMatch in Regex.Matches(arrMatch.Groups[1].Value, "\\{[^{}]*\\}"))
        {
            string s = objMatch.Value;
            list.Add(new GlbBufferView
            {
                buffer = MatchInt(s, "buffer", 0),
                byteOffset = MatchInt(s, "byteOffset", 0),
                byteLength = MatchInt(s, "byteLength", 0),
                byteStride = MatchInt(s, "byteStride", 0)
            });
        }
        return list;
    }

    private static int MatchInt(string src, string key, int def)
    {
        Match m = Regex.Match(src, "\"" + key + "\"\\s*:\\s*(\\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : def;
    }

    private static string MatchString(string src, string key, string def)
    {
        Match m = Regex.Match(src, "\"" + key + "\"\\s*:\\s*\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : def;
    }

    private static Vector3[] ReadVec3Accessor(byte[] data, int binStart, GlbAccessor acc, List<GlbBufferView> views, bool flipX)
    {
        GlbBufferView bv = views[acc.bufferView];
        int stride = bv.byteStride > 0 ? bv.byteStride : 12;
        int start = binStart + bv.byteOffset + acc.byteOffset;
        Vector3[] res = new Vector3[acc.count];
        float signX = flipX ? -1f : 1f;

        for (int i = 0; i < acc.count; i++)
        {
            int off = start + i * stride;
            float x = BitConverter.ToSingle(data, off) * signX;
            float y = BitConverter.ToSingle(data, off + 4);
            float z = BitConverter.ToSingle(data, off + 8);
            res[i] = new Vector3(x, y, z);
        }
        return res;
    }

    private static Vector2[] ReadVec2Accessor(byte[] data, int binStart, GlbAccessor acc, List<GlbBufferView> views, bool flipV)
    {
        GlbBufferView bv = views[acc.bufferView];
        int stride = bv.byteStride > 0 ? bv.byteStride : 8;
        int start = binStart + bv.byteOffset + acc.byteOffset;
        Vector2[] res = new Vector2[acc.count];

        for (int i = 0; i < acc.count; i++)
        {
            int off = start + i * stride;
            float u = BitConverter.ToSingle(data, off);
            float v = BitConverter.ToSingle(data, off + 4);
            res[i] = new Vector2(u, flipV ? (1f - v) : v);
        }
        return res;
    }

    private static int[] ReadScalarIndices(byte[] data, int binStart, GlbAccessor acc, List<GlbBufferView> views)
    {
        GlbBufferView bv = views[acc.bufferView];
        int start = binStart + bv.byteOffset + acc.byteOffset;
        int[] res = new int[acc.count];

        if (acc.componentType == 5125) // UNSIGNED_INT
        {
            int stride = bv.byteStride > 0 ? bv.byteStride : 4;
            for (int i = 0; i < acc.count; i++)
                res[i] = (int)BitConverter.ToUInt32(data, start + i * stride);
        }
        else if (acc.componentType == 5123) // UNSIGNED_SHORT
        {
            int stride = bv.byteStride > 0 ? bv.byteStride : 2;
            for (int i = 0; i < acc.count; i++)
                res[i] = BitConverter.ToUInt16(data, start + i * stride);
        }
        else if (acc.componentType == 5121) // UNSIGNED_BYTE
        {
            for (int i = 0; i < acc.count; i++)
                res[i] = data[start + i];
        }
        return res;
    }
}

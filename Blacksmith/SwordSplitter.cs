using System.Collections.Generic;
using UnityEngine;

// 칼 모델이 메시 하나로 되어 있을 때, 길이 방향(로컬 Y)의 기준 높이로
// 손잡이(아래)와 칼날(위) 두 오브젝트로 나눕니다.
// 모델 Import 설정에서 Read/Write를 켜 두어야 합니다.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SwordSplitter : MonoBehaviour
{
    [Tooltip("이 높이(로컬 Y)보다 위의 삼각형은 칼날, 아래는 손잡이가 됩니다.")]
    public float splitHeight = 0f;
    public bool addBladeDamage = true;

    public GameObject Blade { get; private set; }
    public GameObject Handle { get; private set; }

    void Awake() => Split();

    public void Split()
    {
        Mesh src = GetComponent<MeshFilter>().sharedMesh;
        var renderer = GetComponent<MeshRenderer>();
        Vector3[] v = src.vertices;
        int[] tris = src.triangles;

        var bladeTris = new List<int>();
        var handleTris = new List<int>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            float centerY = (v[tris[i]].y + v[tris[i + 1]].y + v[tris[i + 2]].y) / 3f;
            var list = centerY >= splitHeight ? bladeTris : handleTris;
            list.Add(tris[i]);
            list.Add(tris[i + 1]);
            list.Add(tris[i + 2]);
        }

        Handle = CreatePart("Handle", src, handleTris, renderer.sharedMaterial);
        Blade = CreatePart("Blade", src, bladeTris, renderer.sharedMaterial);
        renderer.enabled = false; // 원본은 숨깁니다

        if (addBladeDamage) Blade.AddComponent<BladeDamage>();
    }

    GameObject CreatePart(string partName, Mesh src, List<int> tris, Material material)
    {
        Vector3[] srcVerts = src.vertices;
        Vector3[] srcNormals = src.normals;
        Vector2[] srcUv = src.uv;
        bool hasNormals = srcNormals.Length == srcVerts.Length;
        bool hasUv = srcUv.Length == srcVerts.Length;

        // 이 부분에서 쓰는 정점만 골라 새 번호를 매깁니다.
        var remap = new Dictionary<int, int>();
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var newTris = new List<int>(tris.Count);
        foreach (int old in tris)
        {
            if (!remap.TryGetValue(old, out int idx))
            {
                idx = verts.Count;
                remap[old] = idx;
                verts.Add(srcVerts[old]);
                if (hasNormals) normals.Add(srcNormals[old]);
                if (hasUv) uv.Add(srcUv[old]);
            }
            newTris.Add(idx);
        }

        var mesh = new Mesh { name = partName };
        mesh.SetVertices(verts);
        if (hasNormals) mesh.SetNormals(normals);
        if (hasUv) mesh.SetUVs(0, uv);
        mesh.SetTriangles(newTris, 0);
        if (!hasNormals) mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(partName);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        var col = go.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        col.convex = true;
        return go;
    }
}

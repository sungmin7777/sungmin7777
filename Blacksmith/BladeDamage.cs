using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 칼날 메시를 깨뜨리고(날에 삼각형 홈) 반대편 날을 거울처럼 복사해서 복구합니다.
// 칼날 메시의 로컬 축 기준: Y = 길이 방향, X = 날 폭 방향(양쪽 날), Z = 두께 방향
[RequireComponent(typeof(MeshFilter))]
public class BladeDamage : MonoBehaviour
{
    [Header("깨짐 설정")]
    public int minChips = 2;            // 한 번에 생기는 홈 개수 (최소)
    public int maxChips = 4;            // 한 번에 생기는 홈 개수 (최대)
    public float chipWidth = 0.08f;     // 홈의 길이 방향 폭
    public float chipDepth = 0.03f;     // 홈 깊이
    public float minHalfWidth = 0.005f; // 날이 중심선을 넘어가지 않게 하는 최소 폭

    [Header("연출")]
    public float breakDuration = 0.2f;
    public float repairDuration = 1.0f;
    public bool spawnShards = true;     // 깨질 때 삼각형 파편을 튀게 할지

    const float Quant = 10000f;

    Mesh mesh;
    Vector3[] verts;
    int[] mirrorOf;   // 각 정점의 반대편 짝. 중심선 위의 정점은 자기 자신
    Coroutine running;

    public bool IsBusy => running != null;

    // 양쪽 날의 모양이 다르면 깨진 상태로 봅니다.
    public bool IsBroken
    {
        get
        {
            for (int i = 0; i < verts.Length; i++)
            {
                int j = mirrorOf[i];
                if (j != i && Mathf.Abs(Mathf.Abs(verts[i].x) - Mathf.Abs(verts[j].x)) > 0.001f)
                    return true;
            }
            return false;
        }
    }

    void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh; // 인스턴스 복사본이라 원본 에셋은 안 바뀝니다
        mesh.MarkDynamic();
        verts = mesh.vertices;
        BuildMirrorPairs();
    }

    public void Break()
    {
        if (running == null) Run(DoChange(ComputeBrokenShape(spawnShards), breakDuration));
    }

    public void Repair()
    {
        if (running == null) Run(DoChange(ComputeRepairedShape(), repairDuration));
    }

    public void BreakThenRepair(float pause) => Run(BreakThenRepairRoutine(pause));

    // 연출 없이 바로 깨진 상태로 만듭니다. (처음부터 깨진 칼을 만들 때 사용)
    public void BreakImmediately()
    {
        verts = ComputeBrokenShape(false);
        ApplyMesh();
    }

    void Run(IEnumerator routine)
    {
        if (running != null) return;
        running = StartCoroutine(Wrap(routine));
    }

    IEnumerator Wrap(IEnumerator routine)
    {
        yield return StartCoroutine(routine);
        running = null;
    }

    IEnumerator BreakThenRepairRoutine(float pause)
    {
        yield return StartCoroutine(DoChange(ComputeBrokenShape(spawnShards), breakDuration));
        yield return new WaitForSeconds(pause);
        yield return StartCoroutine(DoChange(ComputeRepairedShape(), repairDuration));
    }

    // 같은 높이(Y)와 두께 위치(Z)에 있는 정점끼리 묶어서, +X 쪽과 -X 쪽을 바깥쪽부터 짝지어 줍니다.
    // 깨짐은 X만 바꾸기 때문에, 이미 깨진 칼이어도 짝을 제대로 찾을 수 있습니다.
    void BuildMirrorPairs()
    {
        mirrorOf = new int[verts.Length];
        var groups = new Dictionary<Vector2Int, List<int>>();
        for (int i = 0; i < verts.Length; i++)
        {
            mirrorOf[i] = i;
            var key = new Vector2Int(RowKey(verts[i].y), Mathf.RoundToInt(verts[i].z * Quant));
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<int>();
            list.Add(i);
        }

        foreach (var list in groups.Values)
        {
            var pos = new List<int>();
            var neg = new List<int>();
            foreach (int i in list)
            {
                if (verts[i].x > 1e-5f) pos.Add(i);
                else if (verts[i].x < -1e-5f) neg.Add(i);
            }
            pos.Sort((a, b) => verts[b].x.CompareTo(verts[a].x));
            neg.Sort((a, b) => verts[a].x.CompareTo(verts[b].x));
            int n = Mathf.Min(pos.Count, neg.Count);
            for (int k = 0; k < n; k++)
            {
                mirrorOf[pos[k]] = neg[k];
                mirrorOf[neg[k]] = pos[k];
            }
        }
    }

    // 한쪽 날에 V자(삼각형) 홈을 여러 개 냅니다.
    Vector3[] ComputeBrokenShape(bool withShards)
    {
        float side = Random.value < 0.5f ? 1f : -1f;

        float minY = float.MaxValue, maxY = float.MinValue;
        var rowEdge = new Dictionary<int, float>(); // 높이별 가장 바깥 날 위치
        foreach (var v in verts)
        {
            minY = Mathf.Min(minY, v.y);
            maxY = Mathf.Max(maxY, v.y);
            if (v.x * side <= 0f) continue;
            int key = RowKey(v.y);
            rowEdge.TryGetValue(key, out float edge);
            rowEdge[key] = Mathf.Max(edge, Mathf.Abs(v.x));
        }

        var target = (Vector3[])verts.Clone();
        float len = maxY - minY;
        int chips = Random.Range(minChips, maxChips + 1);

        for (int c = 0; c < chips; c++)
        {
            float center = Random.Range(minY + len * 0.15f, maxY - len * 0.15f);
            float depth = chipDepth * Random.Range(0.6f, 1.2f);
            float half = chipWidth * 0.5f * Random.Range(0.7f, 1.3f);
            float shardEdge = 0f;

            for (int i = 0; i < target.Length; i++)
            {
                Vector3 v = target[i];
                if (v.x * side <= 0f) continue;
                float profile = 1f - Mathf.Abs(v.y - center) / half; // 가운데가 가장 깊은 삼각형 모양
                if (profile <= 0f) continue;

                float edge = rowEdge[RowKey(v.y)];
                shardEdge = Mathf.Max(shardEdge, edge);
                float limit = Mathf.Max(edge - depth * profile, minHalfWidth);
                if (Mathf.Abs(v.x) > limit)
                {
                    v.x = side * limit;
                    target[i] = v;
                }
            }

            if (withShards && shardEdge > 0f)
            {
                SpawnShard(new Vector3(side * shardEdge, center - half, 0f),
                           new Vector3(side * shardEdge, center + half, 0f),
                           new Vector3(side * Mathf.Max(shardEdge - depth, minHalfWidth), center, 0f),
                           side);
            }
        }
        return target;
    }

    // 덜 손상된 쪽 날(더 바깥으로 나와 있는 쪽)을 기준으로, 반대편을 거울처럼 똑같이 맞춥니다.
    Vector3[] ComputeRepairedShape()
    {
        float posSum = 0f, negSum = 0f;
        for (int i = 0; i < verts.Length; i++)
        {
            if (mirrorOf[i] == i) continue;
            if (verts[i].x > 0f) posSum += verts[i].x;
            else negSum -= verts[i].x;
        }
        float refSide = posSum >= negSum ? 1f : -1f;

        var target = (Vector3[])verts.Clone();
        for (int i = 0; i < verts.Length; i++)
        {
            int j = mirrorOf[i];
            if (j == i || verts[i].x * refSide > 0f) continue;
            Vector3 v = target[i];
            v.x = -verts[j].x;
            target[i] = v;
        }
        return target;
    }

    IEnumerator DoChange(Vector3[] target, float duration)
    {
        var start = (Vector3[])verts.Clone();
        float t = 0f;
        while (t < 1f)
        {
            t = duration > 0f ? Mathf.Min(1f, t + Time.deltaTime / duration) : 1f;
            float e = Mathf.SmoothStep(0f, 1f, t);
            for (int i = 0; i < verts.Length; i++)
                verts[i] = Vector3.LerpUnclamped(start[i], target[i], e);
            ApplyMesh();
            yield return null;
        }
    }

    void ApplyMesh()
    {
        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    static int RowKey(float y) => Mathf.RoundToInt(y * Quant);

    // 떨어져 나간 조각: 삼각형 모양의 얇은 파편 (a, b, c는 칼날 로컬 좌표)
    void SpawnShard(Vector3 a, Vector3 b, Vector3 c, float side)
    {
        Vector3 z = new Vector3(0f, 0f, 0.004f);
        var shardVerts = new List<Vector3>();
        var shardTris = new List<int>();

        // 면 하나를 양면으로 추가합니다. (어느 방향에서 봐도 보이게)
        void Face(Vector3[] p)
        {
            for (int s = 0; s < 2; s++)
            {
                int start = shardVerts.Count;
                shardVerts.AddRange(p);
                for (int i = 1; i < p.Length - 1; i++)
                {
                    shardTris.Add(start);
                    shardTris.Add(start + (s == 0 ? i : i + 1));
                    shardTris.Add(start + (s == 0 ? i + 1 : i));
                }
            }
        }

        Face(new[] { a + z, b + z, c + z });
        Face(new[] { a - z, b - z, c - z });
        Face(new[] { a + z, b + z, b - z, a - z });
        Face(new[] { b + z, c + z, c - z, b - z });
        Face(new[] { c + z, a + z, a - z, c - z });

        var shardMesh = new Mesh { name = "Shard" };
        shardMesh.SetVertices(shardVerts);
        shardMesh.SetTriangles(shardTris, 0);
        shardMesh.RecalculateNormals();

        var go = new GameObject("Shard");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = shardMesh;
        var mr = go.AddComponent<MeshRenderer>();
        var myRenderer = GetComponent<MeshRenderer>();
        if (myRenderer != null) mr.sharedMaterial = myRenderer.sharedMaterial;

        // 콜라이더 없이 날아가다 사라집니다. (작업 영역 트리거에 걸리지 않게)
        var rb = go.AddComponent<Rigidbody>();
        rb.centerOfMass = (a + b + c) / 3f; // 파편 자체를 중심으로 회전하게
        Vector3 outward = transform.TransformDirection(new Vector3(side, 0f, 0f));
        rb.AddForce(outward * 1.2f + Vector3.up * 0.8f, ForceMode.VelocityChange);
        rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);
        Destroy(go, 2f);
    }
}

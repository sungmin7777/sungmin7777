using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 테스트용 데모
// 사용법: 빈 씬에 빈 오브젝트를 만들고 이 스크립트를 붙인 뒤 플레이하세요.
// 가운데에 육면체 작업 영역, 왼쪽에 멀쩡한 칼, 오른쪽에 이미 깨진 칼이 만들어집니다.
// 조작: Tab = 움직일 칼 바꾸기, 방향키 = 선택한 칼 이동
public class BlacksmithDemo : MonoBehaviour
{
    public float moveSpeed = 1.5f;

    Transform[] swords;
    BladeDamage[] blades;
    int selected;

    void Start()
    {
        CreateZone(new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 1.5f, 0.6f));

        swords = new Transform[2];
        blades = new BladeDamage[2];
        swords[0] = CreateSword("Sword_Normal", new Vector3(-1.2f, 0f, 0f), false, out blades[0]);
        swords[1] = CreateSword("Sword_Broken", new Vector3(1.2f, 0f, 0f), true, out blades[1]);

        SetupCameraAndLight();
    }

    void Update()
    {
        Vector2 move = ReadInput(out bool switchPressed);
        if (switchPressed) selected = (selected + 1) % swords.Length;
        swords[selected].position += new Vector3(move.x, move.y, 0f) * moveSpeed * Time.deltaTime;
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 600, 25), "Tab: 칼 바꾸기   방향키: 이동");
        for (int i = 0; i < swords.Length; i++)
        {
            string mark = i == selected ? "▶ " : "   ";
            string state = blades[i].IsBusy ? "작업 중" : blades[i].IsBroken ? "깨짐" : "정상";
            GUI.Label(new Rect(10, 35 + i * 22, 600, 25), $"{mark}{swords[i].name} : {state}");
        }
    }

    static Vector2 ReadInput(out bool switchPressed)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        switchPressed = kb != null && kb.tabKey.wasPressedThisFrame;
        if (kb == null) return Vector2.zero;
        float x = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
        float y = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
#else
        switchPressed = Input.GetKeyDown(KeyCode.Tab);
        float x = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float y = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        return new Vector2(x, y);
#endif
    }

    // 트리거 육면체 + 모서리만 보이는 테두리
    static void CreateZone(Vector3 center, Vector3 size)
    {
        var zone = new GameObject("ForgeZone");
        zone.transform.position = center;
        zone.AddComponent<BoxCollider>().size = size;
        zone.AddComponent<ForgeZone>();

        const float thickness = 0.01f;
        Vector3 half = size * 0.5f;
        for (int axis = 0; axis < 3; axis++)
        {
            int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
            for (int i = 0; i < 4; i++)
            {
                Vector3 pos = Vector3.zero;
                pos[a1] = (i & 1) == 0 ? -half[a1] : half[a1];
                pos[a2] = (i & 2) == 0 ? -half[a2] : half[a2];
                Vector3 scale = Vector3.one * thickness;
                scale[axis] = size[axis];

                var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(edge.GetComponent<Collider>());
                edge.transform.SetParent(zone.transform, false);
                edge.transform.localPosition = pos;
                edge.transform.localScale = scale;
                Tint(edge, new Color(1f, 0.5f, 0.1f));
            }
        }
    }

    // 손잡이, 가드, 칼날이 분리된 칼. 칼날은 날 부분에 정점이 촘촘한 메시로 만듭니다.
    static Transform CreateSword(string swordName, Vector3 position, bool preBroken, out BladeDamage damage)
    {
        var root = new GameObject(swordName).transform;
        root.position = position;
        var rb = root.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Handle";
        handle.transform.SetParent(root, false);
        handle.transform.localPosition = new Vector3(0f, -0.13f, 0f);
        handle.transform.localScale = new Vector3(0.035f, 0.12f, 0.035f);
        Tint(handle, new Color(0.35f, 0.2f, 0.1f));

        var guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        guard.name = "Guard";
        guard.transform.SetParent(root, false);
        guard.transform.localScale = new Vector3(0.22f, 0.03f, 0.05f);
        Tint(guard, new Color(0.6f, 0.5f, 0.2f));

        var blade = new GameObject("Blade");
        blade.transform.SetParent(root, false);
        blade.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        blade.AddComponent<MeshFilter>().sharedMesh = BuildBladeMesh(1.0f, 0.04f, 0.008f, 100);
        var mr = blade.AddComponent<MeshRenderer>();
        mr.sharedMaterial = guard.GetComponent<MeshRenderer>().sharedMaterial;
        Tint(blade, new Color(0.8f, 0.82f, 0.85f));
        blade.AddComponent<BoxCollider>();

        damage = blade.AddComponent<BladeDamage>();
        if (preBroken) damage.BreakImmediately();
        return root;
    }

    // 단면이 마름모인 칼날. 로컬 Y = 길이, X = 날 폭, Z = 두께
    static Mesh BuildBladeMesh(float length, float halfWidth, float halfThick, int segments)
    {
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        float bodyLength = length * 0.92f;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float taper = t < 0.8f ? 1f : Mathf.Lerp(1f, 0.25f, (t - 0.8f) / 0.2f); // 끝으로 갈수록 좁게
            float y = t * bodyLength;
            float w = halfWidth * taper;
            float th = halfThick * taper;
            verts.Add(new Vector3(w, y, 0f));   // 오른쪽 날
            verts.Add(new Vector3(0f, y, th));  // 앞면 중심
            verts.Add(new Vector3(-w, y, 0f));  // 왼쪽 날
            verts.Add(new Vector3(0f, y, -th)); // 뒷면 중심
        }
        int tip = verts.Count;
        verts.Add(new Vector3(0f, length, 0f));

        for (int i = 0; i < segments; i++)
        {
            for (int k = 0; k < 4; k++)
            {
                int a0 = i * 4 + k, b0 = i * 4 + (k + 1) % 4;
                int a1 = a0 + 4, b1 = b0 + 4;
                tris.Add(a1); tris.Add(b1); tris.Add(b0);
                tris.Add(a1); tris.Add(b0); tris.Add(a0);
            }
        }
        int last = segments * 4;
        for (int k = 0; k < 4; k++)
        {
            tris.Add(tip); tris.Add(last + (k + 1) % 4); tris.Add(last + k);
        }

        var mesh = new Mesh { name = "BladeMesh" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void SetupCameraAndLight()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
        }
        cam.transform.position = new Vector3(0f, 0.45f, -2.6f);
        cam.transform.LookAt(new Vector3(0f, 0.45f, 0f));

        if (FindFirstObjectByType<Light>() == null)
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        }
    }

    static void Tint(GameObject go, Color color)
    {
        go.GetComponent<Renderer>().material.color = color;
    }
}

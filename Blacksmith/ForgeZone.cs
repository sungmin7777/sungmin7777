using System.Collections.Generic;
using UnityEngine;

// 육면체 작업 영역. 칼이 들어오면
//  - 멀쩡한 칼: 깨졌다가 → 잠시 뒤 반대편 날을 기준으로 복구
//  - 이미 깨진 칼: 바로 복구
// 칼 최상위 오브젝트에 Rigidbody(Is Kinematic 켜기)가 있어야 감지됩니다.
[RequireComponent(typeof(BoxCollider))]
public class ForgeZone : MonoBehaviour
{
    public float pauseBeforeRepair = 1.0f;

    // 칼 하나에 콜라이더가 여러 개(손잡이, 가드, 칼날)라서, 칼마다 몇 개가 들어와 있는지 셉니다.
    readonly Dictionary<Rigidbody, int> inside = new Dictionary<Rigidbody, int>();

    void Reset() => GetComponent<BoxCollider>().isTrigger = true;
    void Awake() => GetComponent<BoxCollider>().isTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;

        inside.TryGetValue(rb, out int count);
        inside[rb] = count + 1;
        if (count > 0) return; // 같은 칼의 다른 부분이 이미 들어와 있음

        var blade = rb.GetComponentInChildren<BladeDamage>();
        if (blade == null || blade.IsBusy) return;

        if (blade.IsBroken) blade.Repair();
        else blade.BreakThenRepair(pauseBeforeRepair);
    }

    void OnTriggerExit(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null || !inside.TryGetValue(rb, out int count)) return;
        if (count <= 1) inside.Remove(rb);
        else inside[rb] = count - 1;
    }
}

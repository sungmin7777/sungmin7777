using UnityEngine;

// 테스트용 스크립트: 시작할 때 콘솔에 메시지를 출력하고, 오브젝트를 천천히 회전시킵니다.
public class HelloTest : MonoBehaviour
{
    public float rotateSpeed = 45f;

    void Start()
    {
        Debug.Log("HelloTest 스크립트가 실행됐어요!");
    }

    void Update()
    {
        transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f);
    }
}

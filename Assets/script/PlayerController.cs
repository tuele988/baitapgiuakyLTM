using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public float speed = 5f;
    public bool isLocalPlayer = false; // set true cho player local
    public float sendRate = 0.1f; // gửi vị trí mỗi 0.1s (10Hz)

    Rigidbody2D rb;
    Vector2 moveInput;
    Vector3 lastSentPos;
    float nextSendTime = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // Input đơn giản: WASD hoặc Arrow keys
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
    }

    void FixedUpdate()
    {
        if (isLocalPlayer)
        {
            Vector2 newPos = rb.position + moveInput * speed * Time.fixedDeltaTime;
            rb.MovePosition(newPos);
        }
    }

    void LateUpdate()
    {
        // Gửi vị trí theo rate
        if (!isLocalPlayer) return;
        if (Time.time >= nextSendTime)
        {
            nextSendTime = Time.time + sendRate;
            SendPositionIfMoved();
        }
    }

    void SendPositionIfMoved()
    {
        Vector3 cur = transform.position;
        // chỉ gửi nếu di chuyển một khoảng nhỏ (tránh spam khi đứng yên)
        if (Vector3.Distance(cur, lastSentPos) > 0.001f)
        {
            lastSentPos = cur;
            if (NetworkManagerTCP.Instance != null)
            {
                NetworkManagerTCP.Instance.SendPosition(cur);
            }
        }
    }
}

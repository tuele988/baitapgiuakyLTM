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
    
    private GameInitializer gameInitializer;
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
            // 1. Tính toán vector di chuyển (movement)
            // Lấy input, chuẩn hóa để di chuyển chéo không nhanh hơn,
            // và nhân với tốc độ (speed) cùng thời gian cố định (Time.fixedDeltaTime)
            // để đảm bảo chuyển động mượt mà và độc lập với tốc độ khung hình.
            Vector2 movement = moveInput.normalized * speed * Time.fixedDeltaTime; 
            
            // 2. Tính toán vị trí mới (newPos)
            Vector2 newPos = rb.position + movement;
            
            // 3. Di chuyển Rigidbody
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
   public void ResetToSpawnPosition()
    {
        if (gameInitializer == null)
        {
            // Kiểm tra lại nếu Awake() chạy trước khi GameInitializer sinh ra
            gameInitializer = FindObjectOfType<GameInitializer>();
            if (gameInitializer == null)
            {
                Debug.LogError("GameInitializer not found! Cannot reset player position.");
                return;
            }
        }

        Vector2 spawnPos = Vector2.zero;

        if (CompareTag("ServerPlayer"))
        {
            spawnPos = gameInitializer.serverSpawn;
        }
        else if (CompareTag("ClientPlayer"))
        {
            spawnPos = gameInitializer.clientSpawn;
        }
        
        // 1. Đặt lại vị trí Transform
        transform.position = spawnPos;
        
        // 2. Reset vị trí Rigidbody (Quan trọng cho vật lý)
        rb.position = spawnPos; 
        
        // 3. Reset lastSentPos để client gửi vị trí mới ngay lập tức
        lastSentPos = spawnPos;
        
        // 4. Reset vận tốc (Tránh nhân vật bị trôi)
        rb.linearVelocity = Vector2.zero;
        
        // Xóa lệnh không chính xác: NetworkManagerTCP.Instance.Update(); 
    }
}

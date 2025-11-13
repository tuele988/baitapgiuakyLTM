using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public Rigidbody2D body;
    public float speed = 5f;
    public bool isLocalPlayer = false; // chỉ true nếu nhân vật này thuộc về mình

    void Update()
    {
        if (!isLocalPlayer) return;

        float moveX = Input.GetAxis("Horizontal");
        float moveY = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(moveX, moveY, 0) * speed * Time.deltaTime;
        transform.position += move;

        // Gửi vị trí mới cho bên kia qua TCP
        if (NetworkManagerTCP.Instance != null)
        {
            NetworkManagerTCP.Instance.SendPosition(transform.position);
        }
    }
}
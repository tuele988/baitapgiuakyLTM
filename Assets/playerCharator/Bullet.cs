using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 3f;

    void Start()
    {
        // Sau 3 giây tự hủy
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        // Di chuyển thẳng mỗi frame
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // Khi chạm vật thể, xóa đạn (bạn có thể thêm logic khác)
        Destroy(gameObject);
    }
}

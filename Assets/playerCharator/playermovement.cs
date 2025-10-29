using UnityEngine;

public class playermovement : MonoBehaviour
{
    public Rigidbody2D body;
    public float speed;

    void Start()
    {
    }

    void Update()
    {
        float xInput = Input.GetAxis("Horizontal");
        float yInput = Input.GetAxis("Vertical");
        Vector2 move = new Vector2(xInput, yInput);
        body.linearVelocity = move*speed;
    }
}

using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 50f;
    public float height = 30f;
    public float orbitSpeed = 50f;

    private float currentAngle = 0f;

    void Update()
    {
        if (target == null) return;

        float horizontalInput = Input.GetAxis("Horizontal");
        currentAngle += horizontalInput * orbitSpeed * Time.deltaTime;

        float rad = currentAngle * Mathf.Deg2Rad;
        float x = Mathf.Sin(rad) * distance;
        float z = Mathf.Cos(rad) * distance;

        Vector3 pos = new Vector3(x, height, z) + target.position;
        transform.position = pos;
        transform.LookAt(target.position);
    }
}

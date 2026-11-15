using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform cam;
    public float speed = 2.2f;
    public float lookSensitivity = 0.12f;
    public bool controlsEnabled = true;

    CharacterController cc;
    float pitch, shakeT, shakeM;
    Vector3 lastShake;

    void Awake() { cc = GetComponent<CharacterController>(); }

    public void ResetLook() { pitch = 0f; cam.localRotation = Quaternion.identity; }
    public void Shake(float duration, float magnitude) { shakeT = duration; shakeM = magnitude; }

    void Update()
    {
        if (!controlsEnabled) return;
        Vector2 look = MobileInput.ConsumeLook() * lookSensitivity;
        transform.Rotate(0, look.x, 0);
        pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
        cam.localEulerAngles = new Vector3(pitch, 0, 0);
        Vector2 mv = MobileInput.Move;
        cc.SimpleMove((transform.forward * mv.y + transform.right * mv.x) * speed);
    }

    void LateUpdate()
    {
        cam.localPosition -= lastShake;
        lastShake = Vector3.zero;
        if (shakeT > 0f)
        {
            shakeT -= Time.deltaTime;
            lastShake = Random.insideUnitSphere * shakeM;
            cam.localPosition += lastShake;
        }
    }
}

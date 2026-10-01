using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    public Transform CameraHolder;
    public float MoveSpeed = 4f;
    public float LookSensitivity = 0.1f;
    public float InteractRange = 3f;

    CharacterController controller;
    float pitch;
    float verticalVelocity;
    Interactable current;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        Vector2 look = mouse.delta.ReadValue() * LookSensitivity;
        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
        CameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        Vector3 move = (transform.right * x + transform.forward * z).normalized * MoveSpeed;

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -1f;
        verticalVelocity += Physics.gravity.y * Time.deltaTime;
        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);

        UpdateInteraction(kb);

        if (kb.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void UpdateInteraction(Keyboard kb)
    {
        current = null;
        Transform cam = CameraHolder;
        RaycastHit hit;
        if (Physics.Raycast(cam.position, cam.forward, out hit, InteractRange))
        {
            current = hit.collider.GetComponentInParent<Interactable>();
        }

        if (current != null && kb.eKey.wasPressedThisFrame)
        {
            current.Interact();
        }
    }

    void OnGUI()
    {
        float cx = Screen.width / 2f;
        float cy = Screen.height / 2f;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), Texture2D.whiteTexture);

        if (current != null)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.fontSize = 24;
            s.alignment = TextAnchor.MiddleCenter;
            s.normal.textColor = Color.white;
            GUI.Label(new Rect(cx - 250f, cy + 30f, 500f, 40f), "[E] " + current.Prompt, s);
        }
    }
}
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
    Interactable holdTarget;
    float holdProgress;

    // Arayüzün (GameUI) okuduğu bilgiler
    public string CurrentPrompt
    {
        get { return current != null ? current.Prompt : null; }
    }

    // Basılı tutma yoksa -1, varsa 0 ile 1 arası
    public float HoldRatio
    {
        get
        {
            if (holdTarget != null && holdTarget.HoldTime > 0f)
                return Mathf.Clamp01(holdProgress / holdTarget.HoldTime);
            return -1f;
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    bool IsPlaying()
    {
        return WrapManager.Instance == null || WrapManager.Instance.State == GameState.Playing;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        bool playing = IsPlaying();

        if (playing)
        {
            Vector2 look = mouse.delta.ReadValue() * LookSensitivity;
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
            CameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            float speed = MoveSpeed;
            if (WrapManager.Instance != null) speed *= WrapManager.Instance.SpeedMultiplier;

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            Vector3 move = (transform.right * x + transform.forward * z).normalized * speed;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -1f;
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            move.y = verticalVelocity;
            controller.Move(move * Time.deltaTime);
        }

        UpdateInteraction(kb, playing);

        // Unity editöründe ESC imleci kendiliğinden serbest bırakır, oyun penceresine tıklayınca tekrar kilitlenir
        if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void ResetHold()
    {
        holdTarget = null;
        holdProgress = 0f;
    }

    void UpdateInteraction(Keyboard kb, bool playing)
    {
        current = null;

        if (!playing)
        {
            ResetHold();
            return;
        }

        Transform cam = CameraHolder;
        RaycastHit hit;
        if (Physics.Raycast(cam.position, cam.forward, out hit, InteractRange))
        {
            current = hit.collider.GetComponentInParent<Interactable>();
        }

        if (current != null && current.HoldTime > 0f)
        {
            if (kb.eKey.wasPressedThisFrame && current.CanInteract())
            {
                holdTarget = current;
                holdProgress = 0f;
            }

            if (holdTarget == current && kb.eKey.isPressed)
            {
                holdProgress += Time.deltaTime;
                if (holdProgress >= current.HoldTime)
                {
                    current.Interact();
                    ResetHold();
                }
            }
            else
            {
                ResetHold();
            }
        }
        else
        {
            ResetHold();
            if (current != null && kb.eKey.wasPressedThisFrame)
            {
                current.Interact();
            }
        }
    }
}
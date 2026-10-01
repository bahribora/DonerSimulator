using UnityEngine;

public class ActionStation : Interactable
{
    public enum Mode { Serve, Trash }
    public Mode Kind;

    void Awake()
    {
        Prompt = Kind == Mode.Serve ? "Dürümü sar ve teslim et" : "Çöpe at";

        Renderer r = GetComponentInChildren<Renderer>();
        if (r != null)
            r.material.color = Kind == Mode.Serve ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 0.4f, 0.4f);
    }

    public override void Interact()
    {
        if (Kind == Mode.Serve) WrapManager.Instance.Serve();
        else WrapManager.Instance.Trash();
    }
}
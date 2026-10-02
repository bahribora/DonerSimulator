using UnityEngine;

public class IngredientStation : Interactable
{
    public Ingredient Type;

    [Tooltip("E'ye kaç saniye basılı tutulacak")]
    public float CutTime = 2f;

    [Tooltip("Gerçek model (küpün altındaki çocuk nesne). Boşsa küp renkli kalır.")]
    public GameObject ModelVisual;

    bool visible = true;
    MeshRenderer ownRenderer;

    void Awake()
    {
        HoldTime = CutTime;

        switch (Type)
        {
            case Ingredient.Lavas: Prompt = "Lavaş al (basılı tut)"; break;
            case Ingredient.Et: Prompt = "Et kes (basılı tut)"; break;
            default: Prompt = WrapManager.Name(Type) + " ekle (basılı tut)"; break;
        }

        ownRenderer = GetComponent<MeshRenderer>();

        if (ModelVisual != null)
        {
            // Model varsa küpü gizle (collider kalır), renklendirme yapma
            if (ownRenderer != null) ownRenderer.enabled = false;
        }
        else
        {
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null) r.material.color = ColorFor(Type);
        }
    }

    // Soğan ve turşu istasyonları satın alınana kadar kapalı kalır
    bool IsOpen()
    {
        WrapManager wm = WrapManager.Instance;
        if (wm == null) return true;

        if (Type == Ingredient.Sogan) return wm.OnionUnlocked;
        if (Type == Ingredient.Tursu) return wm.PickleUnlocked;
        return true;
    }

    void Update()
    {
        if (Type != Ingredient.Sogan && Type != Ingredient.Tursu) return;
        if (WrapManager.Instance == null) return;

        bool open = IsOpen();
        if (open == visible) return;

        visible = open;

        if (ModelVisual != null)
        {
            ModelVisual.SetActive(open);
        }
        else
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = open;
        }

        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = open;
    }

    public override bool CanInteract()
    {
        return WrapManager.Instance != null && WrapManager.Instance.CanAddIngredient(Type);
    }

    public override void Interact()
    {
        WrapManager.Instance.AddIngredient(Type);
    }

    Color ColorFor(Ingredient i)
    {
        switch (i)
        {
            case Ingredient.Lavas: return new Color(0.95f, 0.88f, 0.7f);
            case Ingredient.Et: return new Color(0.55f, 0.3f, 0.15f);
            case Ingredient.Marul: return new Color(0.3f, 0.75f, 0.3f);
            case Ingredient.Domates: return new Color(0.9f, 0.2f, 0.15f);
            case Ingredient.Sogan: return new Color(0.7f, 0.4f, 0.7f);
            case Ingredient.Tursu: return new Color(0.45f, 0.6f, 0.2f);
            default: return new Color(0.95f, 0.95f, 0.85f);
        }
    }
}
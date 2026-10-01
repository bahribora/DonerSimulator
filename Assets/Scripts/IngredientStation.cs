using UnityEngine;

public class IngredientStation : Interactable
{
    public Ingredient Type;

    void Awake()
    {
        Prompt = Type == Ingredient.Lavas ? "Lavaş al" : WrapManager.Name(Type) + " ekle";

        Renderer r = GetComponentInChildren<Renderer>();
        if (r != null) r.material.color = ColorFor(Type);
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
            default: return new Color(0.95f, 0.95f, 0.85f);
        }
    }
}
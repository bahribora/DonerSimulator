using UnityEngine;

public class Interactable : MonoBehaviour
{
    public string Prompt = "Etkileşim";

    public virtual void Interact()
    {
        Debug.Log(Prompt + " kullanıldı");
    }
}
using UnityEngine;

public class Interactable : MonoBehaviour
{
    public string Prompt = "Etkileşim";

    // 0 ise tek basışla çalışır. 0'dan büyükse E'ye bu kadar saniye basılı tutmak gerekir.
    public float HoldTime = 0f;

    public virtual bool CanInteract()
    {
        return true;
    }

    public virtual void Interact()
    {
        Debug.Log(Prompt + " kullanıldı");
    }
}
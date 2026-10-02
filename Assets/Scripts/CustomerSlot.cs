using UnityEngine;

public class CustomerSlot : MonoBehaviour
{
    [Tooltip("Bu müşteri noktasının altındaki karakter modelleri. Her müşteri için biri rastgele seçilir. Boşsa kapsül renklenir.")]
    public GameObject[] Models;

    public void Show()
    {
        gameObject.SetActive(true);

        if (Models != null && Models.Length > 0)
        {
            // Karakter modeli varsa kapsülü gizle, rastgele bir model göster
            MeshRenderer capsule = GetComponent<MeshRenderer>();
            if (capsule != null) capsule.enabled = false;

            int pick = Random.Range(0, Models.Length);
            for (int i = 0; i < Models.Length; i++)
            {
                if (Models[i] != null) Models[i].SetActive(i == pick);
            }
        }
        else
        {
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null) r.material.color = Color.HSVToRGB(Random.value, 0.5f, 0.9f);
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
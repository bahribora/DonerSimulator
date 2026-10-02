using UnityEngine;

public class DonerMachine : MonoBehaviour
{
    [Header("Build'de pembe çıkmaması için URP malzemesi")]
    public Material BaseMaterial;

    [Header("Et boyutları")]
    public float MeatHeight = 1.0f;
    public float BottomRadius = 0.14f;
    public float TopRadius = 0.26f;
    public int Layers = 10;

    [Header("Dönme hızı (derece/saniye)")]
    public float SpinSpeed = 25f;

    Transform meat;

    void Awake()
    {
        if (BaseMaterial == null)
            Debug.LogWarning("DonerMachine: BaseMaterial boş. Build'de parçalar pembe çıkabilir.");

        Build();
    }

    void Update()
    {
        if (meat != null) meat.Rotate(0f, SpinSpeed * Time.deltaTime, 0f, Space.Self);
    }

    void Build()
    {
        float baseHeight = 0.06f;

        // Taban
        Part("Taban", PrimitiveType.Cylinder,
            new Vector3(0f, baseHeight / 2f, 0f),
            new Vector3(0.8f, baseHeight / 2f, 0.8f),
            new Color(0.25f, 0.25f, 0.28f), transform);

        // Dönen kısım (şiş + et)
        GameObject meatObj = new GameObject("Et");
        meat = meatObj.transform;
        meat.SetParent(transform, false);
        meat.localPosition = new Vector3(0f, baseHeight, 0f);

        // Şiş
        float skewerHeight = MeatHeight + 0.3f;
        Part("Sis", PrimitiveType.Cylinder,
            new Vector3(0f, skewerHeight / 2f, 0f),
            new Vector3(0.04f, skewerHeight / 2f, 0.04f),
            new Color(0.75f, 0.75f, 0.78f), meat);

        // Et katları: aşağıdan yukarı doğru genişler
        float segHeight = MeatHeight / Layers;
        for (int i = 0; i < Layers; i++)
        {
            float t = (i + 0.5f) / Layers;
            float r = Mathf.Lerp(BottomRadius, TopRadius, t);
            r *= 1f + 0.06f * Mathf.Sin(i * 2.1f);

            Color c = (i % 2 == 0)
                ? new Color(0.55f, 0.30f, 0.15f)
                : new Color(0.45f, 0.24f, 0.12f);

            Part("Kat" + i, PrimitiveType.Cylinder,
                new Vector3(0f, (i + 0.5f) * segHeight, 0f),
                new Vector3(r * 2f, segHeight / 2f, r * 2f),
                c, meat);
        }

        // Şişin üstündeki başlık
        Part("Baslik", PrimitiveType.Cylinder,
            new Vector3(0f, MeatHeight + 0.15f, 0f),
            new Vector3(0.12f, 0.025f, 0.12f),
            new Color(0.75f, 0.75f, 0.78f), meat);

        // İki yanda ısıtıcı çubuklar
        float heaterHeight = MeatHeight * 0.9f;
        float heaterY = baseHeight + MeatHeight / 2f;
        float heaterX = TopRadius + 0.2f;
        Color heaterColor = new Color(1f, 0.45f, 0.1f);

        Part("IsiticiSag", PrimitiveType.Cube,
            new Vector3(heaterX, heaterY, 0f),
            new Vector3(0.06f, heaterHeight, 0.35f),
            heaterColor, transform);

        Part("IsiticiSol", PrimitiveType.Cube,
            new Vector3(-heaterX, heaterY, 0f),
            new Vector3(0.06f, heaterHeight, 0.35f),
            heaterColor, transform);
    }

    Transform Part(string partName, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, Transform parent)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = partName;

        // Parçaların collider'ı olmasın, istasyon küpünün collider'ı yeter
        Collider col = g.GetComponent<Collider>();
        if (col != null) Destroy(col);

        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;

        Renderer r = g.GetComponent<Renderer>();
        if (BaseMaterial != null) r.material = new Material(BaseMaterial);
        r.material.color = color;

        return g.transform;
    }
}
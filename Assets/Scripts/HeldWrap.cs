using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(FirstPersonController))]
public class HeldWrap : MonoBehaviour
{
    [Header("Dürümün kamera önündeki yeri")]
    public Vector3 HoldPosition = new Vector3(0.32f, -0.3f, 0.7f);
    public Vector3 HoldEuler = new Vector3(-55f, -15f, 0f);

    class Layer
    {
        public Transform T;
        public Vector3 Target;
        public float Age;
    }

    class Dying
    {
        public Transform T;
        public float Age;
        public bool Served;
    }

    Transform camHolder;
    Transform root;
    float stackHeight;
    int shownCount;
    List<Layer> layers = new List<Layer>();
    List<Dying> dying = new List<Dying>();

    void Start()
    {
        FirstPersonController fpc = GetComponent<FirstPersonController>();
        if (fpc != null) camHolder = fpc.CameraHolder;

        if (camHolder == null)
        {
            Debug.LogError("HeldWrap: CameraHolder bulunamadı. Bu script Player nesnesinde olmalı.");
            enabled = false;
            return;
        }

        if (WrapManager.Instance != null) WrapManager.Instance.WrapEnded += OnWrapEnded;
    }

    void OnDestroy()
    {
        if (WrapManager.Instance != null) WrapManager.Instance.WrapEnded -= OnWrapEnded;
    }

    void Update()
    {
        WrapManager wm = WrapManager.Instance;
        if (wm != null) Sync(wm.WrapContents);

        AnimateLayers();
        AnimateDying();
    }

    void Sync(IReadOnlyList<Ingredient> contents)
    {
        // Dürüm sessizce boşaldıysa (gün bitti vb.) elindeki görüntüyü sil
        if (contents.Count < shownCount) ClearRoot();

        while (shownCount < contents.Count)
        {
            AddLayer(contents[shownCount]);
        }
    }

    void ClearRoot()
    {
        if (root != null) Destroy(root.gameObject);
        root = null;
        layers.Clear();
        shownCount = 0;
        stackHeight = 0f;
    }

    void AddLayer(Ingredient ing)
    {
        if (root == null)
        {
            GameObject g = new GameObject("HeldWrapRoot");
            root = g.transform;
            root.SetParent(camHolder, false);
            root.localPosition = HoldPosition;
            root.localRotation = Quaternion.Euler(HoldEuler);
            stackHeight = 0f;
        }

        Vector3 size = SizeFor(ing);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider col = cube.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Transform t = cube.transform;
        t.SetParent(root, false);
        t.localPosition = new Vector3(0f, stackHeight + size.y / 2f, ZFor(ing));
        t.localScale = new Vector3(size.x, 0.0001f, size.z);

        Renderer r = cube.GetComponent<Renderer>();
        r.material.color = ColorFor(ing);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        Layer l = new Layer();
        l.T = t;
        l.Target = size;
        l.Age = 0f;
        layers.Add(l);

        stackHeight += size.y;
        shownCount++;
    }

    void OnWrapEnded(bool served)
    {
        if (root == null) return;

        // Açılırken yarım kalan katları tamamla
        foreach (Layer l in layers) l.T.localScale = l.Target;

        Dying d = new Dying();
        d.T = root;
        d.Age = 0f;
        d.Served = served;
        dying.Add(d);

        root = null;
        layers.Clear();
        shownCount = 0;
        stackHeight = 0f;
    }

    void AnimateLayers()
    {
        foreach (Layer l in layers)
        {
            if (l.T == null) continue;
            l.Age += Time.deltaTime;
            float k = Mathf.Clamp01(l.Age / 0.12f);
            l.T.localScale = new Vector3(l.Target.x, Mathf.Max(l.Target.y * k, 0.0001f), l.Target.z);
        }
    }

    void AnimateDying()
    {
        for (int i = dying.Count - 1; i >= 0; i--)
        {
            Dying d = dying[i];
            if (d.T == null)
            {
                dying.RemoveAt(i);
                continue;
            }

            d.Age += Time.deltaTime;

            if (d.Served)
            {
                float rollTime = 0.55f;
                float tossTime = 0.35f;

                if (d.Age < rollTime)
                {
                    // Sarma: yana daralır, tüp gibi yuvarlanır
                    float k = d.Age / rollTime;
                    d.T.localScale = new Vector3(Mathf.Lerp(1f, 0.35f, k), Mathf.Lerp(1f, 3f, k), 1f);
                    d.T.localRotation = Quaternion.Euler(HoldEuler) * Quaternion.Euler(0f, 0f, -360f * k);
                }
                else if (d.Age < rollTime + tossTime)
                {
                    // Müşteriye doğru fırlar
                    float k = (d.Age - rollTime) / tossTime;
                    d.T.localPosition = HoldPosition + new Vector3(-0.1f, 0.15f, 0.8f) * k;
                    d.T.localScale = new Vector3(0.35f, 3f, 1f) * (1f - k);
                }
                else
                {
                    Destroy(d.T.gameObject);
                    dying.RemoveAt(i);
                }
            }
            else
            {
                // Çöpe gider: aşağı düşüp küçülür
                float dropTime = 0.35f;
                if (d.Age < dropTime)
                {
                    float k = d.Age / dropTime;
                    d.T.localPosition = HoldPosition + new Vector3(0.1f, -0.5f, 0f) * k;
                    d.T.localScale = Vector3.one * (1f - k);
                }
                else
                {
                    Destroy(d.T.gameObject);
                    dying.RemoveAt(i);
                }
            }
        }
    }

    static Vector3 SizeFor(Ingredient i)
    {
        switch (i)
        {
            case Ingredient.Lavas: return new Vector3(0.36f, 0.015f, 0.30f);
            case Ingredient.Et: return new Vector3(0.30f, 0.03f, 0.10f);
            case Ingredient.Marul: return new Vector3(0.32f, 0.012f, 0.22f);
            case Ingredient.Domates: return new Vector3(0.26f, 0.02f, 0.08f);
            case Ingredient.Sogan: return new Vector3(0.28f, 0.012f, 0.14f);
            default: return new Vector3(0.30f, 0.01f, 0.05f);
        }
    }

    static float ZFor(Ingredient i)
    {
        switch (i)
        {
            case Ingredient.Marul: return 0.02f;
            case Ingredient.Domates: return -0.05f;
            case Ingredient.Sogan: return 0.04f;
            case Ingredient.Sos: return 0.01f;
            default: return 0f;
        }
    }

    static Color ColorFor(Ingredient i)
    {
        switch (i)
        {
            case Ingredient.Lavas: return new Color(0.95f, 0.88f, 0.7f);
            case Ingredient.Et: return new Color(0.55f, 0.3f, 0.15f);
            case Ingredient.Marul: return new Color(0.3f, 0.75f, 0.3f);
            case Ingredient.Domates: return new Color(0.9f, 0.2f, 0.15f);
            case Ingredient.Sogan: return new Color(0.7f, 0.4f, 0.7f);
            default: return new Color(0.95f, 0.95f, 0.85f);
        }
    }
}
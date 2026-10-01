using System.Collections.Generic;
using UnityEngine;

public enum Ingredient { Lavas, Et, Marul, Domates, Sos }

public class WrapManager : MonoBehaviour
{
    public static WrapManager Instance { get; private set; }

    public GameObject CustomerObject;
    public float NextCustomerDelay = 2f;
    public float OrderTime = 45f;
    public int AngryPenalty = 20;

    public int Money;
    public int Served;
    public int Missed;

    static readonly string[] Names = { "Lavaş", "Et", "Marul", "Domates", "Sos" };

    List<Ingredient> wrap = new List<Ingredient>();
    List<Ingredient> order = new List<Ingredient>();
    string message = "";
    Color messageColor = Color.white;
    float messageTimer;

    bool waiting;
    float waitTimer;
    float patienceLeft;

    public static string Name(Ingredient i)
    {
        return Names[(int)i];
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        CallCustomer();
    }

    void Update()
    {
        if (messageTimer > 0f) messageTimer -= Time.deltaTime;

        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f) CallCustomer();
            return;
        }

        patienceLeft -= Time.deltaTime;
        if (patienceLeft <= 0f) CustomerAngry();
    }

    void ShowMessage(string text, Color color)
    {
        message = text;
        messageColor = color;
        messageTimer = 2.5f;
    }

    void CallCustomer()
    {
        waiting = false;
        NewOrder();
        patienceLeft = OrderTime;

        if (CustomerObject != null)
        {
            CustomerObject.SetActive(true);
            Renderer r = CustomerObject.GetComponentInChildren<Renderer>();
            if (r != null) r.material.color = Color.HSVToRGB(Random.value, 0.5f, 0.9f);
        }
    }

    void CustomerLeaves()
    {
        order.Clear();
        waiting = true;
        waitTimer = NextCustomerDelay;

        if (CustomerObject != null) CustomerObject.SetActive(false);
    }

    void CustomerAngry()
    {
        int loss = Mathf.Min(Money, AngryPenalty);
        Money -= loss;
        Missed++;
        ShowMessage("Müşteri kızıp gitti! -" + loss + " TL", Color.red);
        wrap.Clear();
        CustomerLeaves();
    }

    void NewOrder()
    {
        order.Clear();
        order.Add(Ingredient.Lavas);
        order.Add(Ingredient.Et);

        List<Ingredient> extras = new List<Ingredient> { Ingredient.Marul, Ingredient.Domates, Ingredient.Sos };
        for (int i = 0; i < extras.Count; i++)
        {
            int j = Random.Range(i, extras.Count);
            Ingredient tmp = extras[i];
            extras[i] = extras[j];
            extras[j] = tmp;
        }

        int count = Random.Range(1, 4);
        for (int i = 0; i < count; i++) order.Add(extras[i]);
    }

    public void AddIngredient(Ingredient ing)
    {
        if (waiting)
        {
            ShowMessage("Şu an müşteri yok.", Color.white);
            return;
        }
        if (wrap.Count == 0 && ing != Ingredient.Lavas)
        {
            ShowMessage("Önce lavaş al!", Color.red);
            return;
        }
        if (ing == Ingredient.Lavas && wrap.Count > 0)
        {
            ShowMessage("Zaten lavaşın var.", Color.red);
            return;
        }
        if (wrap.Contains(ing))
        {
            ShowMessage(Name(ing) + " zaten ekli.", Color.red);
            return;
        }

        wrap.Add(ing);
        ShowMessage(Name(ing) + " eklendi.", new Color(0.4f, 1f, 0.4f));
    }

    public void Serve()
    {
        if (waiting)
        {
            ShowMessage("Şu an müşteri yok.", Color.white);
            return;
        }

        if (wrap.Count == 0)
        {
            ShowMessage("Elinde dürüm yok!", Color.red);
            return;
        }

        bool ok = wrap.Count == order.Count;
        if (ok)
        {
            foreach (Ingredient o in order)
            {
                if (!wrap.Contains(o)) { ok = false; break; }
            }
        }

        if (ok)
        {
            int reward = 30 + order.Count * 10;
            int bonus = Mathf.RoundToInt(20f * Mathf.Clamp01(patienceLeft / OrderTime));
            Money += reward + bonus;
            Served++;
            ShowMessage("Afiyet olsun! +" + reward + " TL (hız bonusu +" + bonus + ")", new Color(1f, 0.85f, 0.2f));
            wrap.Clear();
            CustomerLeaves();
        }
        else
        {
            ShowMessage("Yanlış dürüm! Çöpe gitti.", Color.red);
            wrap.Clear();
        }
    }

    public void Trash()
    {
        wrap.Clear();
        ShowMessage("Dürüm çöpe atıldı.", Color.white);
    }

    void DrawOrderBubble()
    {
        if (waiting || CustomerObject == null || Camera.main == null) return;

        Vector3 sp = Camera.main.WorldToScreenPoint(CustomerObject.transform.position + Vector3.up * 1.9f);
        if (sp.z < 0f) return;

        float w = 220f;
        float h = 60f + order.Count * 30f;
        float x = sp.x - w / 2f;
        float y = Screen.height - sp.y - h;

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.fontSize = 22;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y + 5f, w, 30f), "SİPARİŞ", title);

        float ratio = Mathf.Clamp01(patienceLeft / OrderTime);
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(x + 15f, y + 38f, w - 30f, 10f), Texture2D.whiteTexture);
        GUI.color = Color.Lerp(Color.red, Color.green, ratio);
        GUI.DrawTexture(new Rect(x + 15f, y + 38f, (w - 30f) * ratio, 10f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle line = new GUIStyle(GUI.skin.label);
        line.fontSize = 20;

        for (int i = 0; i < order.Count; i++)
        {
            bool have = wrap.Contains(order[i]);
            line.normal.textColor = have ? new Color(0.4f, 1f, 0.4f) : Color.white;
            string mark = have ? "[X] " : "[  ] ";
            GUI.Label(new Rect(x + 15f, y + 55f + i * 30f, w - 20f, 30f), mark + Name(order[i]), line);
        }
    }

    void OnGUI()
    {
        DrawOrderBubble();

        GUIStyle label = new GUIStyle(GUI.skin.label);
        label.fontSize = 26;
        label.normal.textColor = Color.white;

        GUI.Label(new Rect(20, 20, 800, 40), "Para: " + Money + " TL   |   Servis: " + Served + "   |   Kaçan: " + Missed, label);

        GUIStyle line = new GUIStyle(label);
        line.fontSize = 22;

        if (waiting)
        {
            GUI.Label(new Rect(20, 65, 500, 40), "Müşteri bekleniyor...", line);
        }

        string inHand = "boş";
        if (wrap.Count > 0)
        {
            List<string> names = wrap.ConvertAll(x => Name(x));
            inHand = string.Join(", ", names);
        }
        GUI.Label(new Rect(20, waiting ? 100 : 65, 700, 30), "Elindeki: " + inHand, line);

        if (messageTimer > 0f)
        {
            GUIStyle msg = new GUIStyle(label);
            msg.fontSize = 30;
            msg.fontStyle = FontStyle.Bold;
            msg.alignment = TextAnchor.MiddleCenter;
            msg.normal.textColor = messageColor;
            GUI.Label(new Rect(Screen.width / 2f - 450f, Screen.height - 110f, 900f, 50f), message, msg);
        }
    }
}
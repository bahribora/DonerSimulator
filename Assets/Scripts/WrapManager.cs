using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum Ingredient { Lavas, Et, Marul, Domates, Sos, Sogan }

public enum GameState { Playing, DayReport, GameOver }

public class WrapManager : MonoBehaviour
{
    public static WrapManager Instance { get; private set; }

    public GameObject CustomerObject;

    [Tooltip("Musteri nesnesinin altındaki karakter modelleri. Her müşteri için biri rastgele seçilir. Boşsa kapsül renklenir.")]
    public GameObject[] CustomerModels;

    public float NextCustomerDelay = 2f;
    public float OrderTime = 45f;
    public int AngryPenalty = 20;

    [Header("Gün Sistemi")]
    public float DayLength = 120f;
    public int BaseRent = 50;
    public int RentIncrease = 10;

    [Header("Yükseltmeler")]
    public int SpeedCost = 80;
    public int PriceCost = 100;
    public int OnionCost = 150;
    public float SpeedBonusPerLevel = 0.15f;
    public float PriceBonusPerLevel = 0.2f;

    public int Money;
    public int Served;
    public int Missed;

    public GameState State { get; private set; }

    public int SpeedLevel { get; private set; }
    public int PriceLevel { get; private set; }
    public bool OnionUnlocked { get; private set; }

    // Elindeki dürümün içindekiler (sadece okunur)
    public IReadOnlyList<Ingredient> WrapContents
    {
        get { return wrap; }
    }

    // Dürüm bitince çağrılır: true = başarıyla servis edildi, false = çöpe gitti
    public event System.Action<bool> WrapEnded;

    public float SpeedMultiplier
    {
        get { return 1f + SpeedLevel * SpeedBonusPerLevel; }
    }

    float PriceMultiplier
    {
        get { return 1f + PriceLevel * PriceBonusPerLevel; }
    }

    const int MaxLevel = 3;

    static readonly string[] Names = { "Lavaş", "Et", "Marul", "Domates", "Sos", "Soğan" };

    List<Ingredient> wrap = new List<Ingredient>();
    List<Ingredient> order = new List<Ingredient>();
    string message = "";
    Color messageColor = Color.white;
    float messageTimer;

    bool waiting;
    float waitTimer;
    float patienceLeft;

    int startMoney;
    int day = 1;
    float dayTimeLeft;
    int dayEarned;
    int dayPenalty;
    int dayServed;
    int dayMissed;
    int rentDue;

    public static string Name(Ingredient i)
    {
        return Names[(int)i];
    }

    void Awake()
    {
        Instance = this;
        startMoney = Money;
    }

    void Start()
    {
        day = 1;
        StartDay();
    }

    void EndWrap(bool served)
    {
        if (WrapEnded != null) WrapEnded(served);
    }

    int CurrentRent()
    {
        return BaseRent + (day - 1) * RentIncrease;
    }

    int NextSpeedCost()
    {
        return SpeedCost * (SpeedLevel + 1);
    }

    int NextPriceCost()
    {
        return PriceCost * (SpeedLevel * 0 + PriceLevel + 1);
    }

    void StartDay()
    {
        State = GameState.Playing;
        dayTimeLeft = DayLength;
        dayEarned = 0;
        dayPenalty = 0;
        dayServed = 0;
        dayMissed = 0;
        wrap.Clear();
        messageTimer = 0f;
        CallCustomer();
    }

    void EndDay()
    {
        wrap.Clear();
        order.Clear();
        waiting = true;
        messageTimer = 0f;
        if (CustomerObject != null) CustomerObject.SetActive(false);

        rentDue = CurrentRent();
        if (Money >= rentDue)
        {
            Money -= rentDue;
            State = GameState.DayReport;
        }
        else
        {
            State = GameState.GameOver;
        }
    }

    void RestartGame()
    {
        day = 1;
        Money = startMoney;
        Served = 0;
        Missed = 0;
        SpeedLevel = 0;
        PriceLevel = 0;
        OnionUnlocked = false;
        StartDay();
    }

    void BuySpeed()
    {
        if (SpeedLevel >= MaxLevel)
        {
            ShowMessage("Hız zaten son seviyede.", Color.white);
            return;
        }

        int cost = NextSpeedCost();
        if (Money < cost)
        {
            ShowMessage("Yeterli paran yok!", Color.red);
            return;
        }

        Money -= cost;
        SpeedLevel++;
        ShowMessage("Hız yükseltildi! (Seviye " + SpeedLevel + "/" + MaxLevel + ")", new Color(0.4f, 1f, 0.4f));
    }

    void BuyPrice()
    {
        if (PriceLevel >= MaxLevel)
        {
            ShowMessage("Fiyat zaten son seviyede.", Color.white);
            return;
        }

        int cost = NextPriceCost();
        if (Money < cost)
        {
            ShowMessage("Yeterli paran yok!", Color.red);
            return;
        }

        Money -= cost;
        PriceLevel++;
        ShowMessage("Fiyat yükseltildi! (Seviye " + PriceLevel + "/" + MaxLevel + ")", new Color(0.4f, 1f, 0.4f));
    }

    void BuyOnion()
    {
        if (OnionUnlocked)
        {
            ShowMessage("Soğan zaten açık.", Color.white);
            return;
        }

        if (Money < OnionCost)
        {
            ShowMessage("Yeterli paran yok!", Color.red);
            return;
        }

        Money -= OnionCost;
        OnionUnlocked = true;
        ShowMessage("Soğan istasyonu açıldı!", new Color(0.4f, 1f, 0.4f));
    }

    void Update()
    {
        if (messageTimer > 0f) messageTimer -= Time.deltaTime;

        if (State == GameState.DayReport)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) BuySpeed();
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) BuyPrice();
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) BuyOnion();

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                day++;
                StartDay();
            }
            return;
        }

        if (State == GameState.GameOver)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) RestartGame();
            return;
        }

        dayTimeLeft -= Time.deltaTime;
        if (dayTimeLeft <= 0f)
        {
            dayTimeLeft = 0f;
            EndDay();
            return;
        }

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

        if (CustomerObject == null) return;

        CustomerObject.SetActive(true);

        if (CustomerModels != null && CustomerModels.Length > 0)
        {
            // Karakter modeli varsa kapsülü gizle, rastgele bir model göster
            MeshRenderer capsule = CustomerObject.GetComponent<MeshRenderer>();
            if (capsule != null) capsule.enabled = false;

            int pick = Random.Range(0, CustomerModels.Length);
            for (int i = 0; i < CustomerModels.Length; i++)
            {
                if (CustomerModels[i] != null) CustomerModels[i].SetActive(i == pick);
            }
        }
        else
        {
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
        dayMissed++;
        dayPenalty += loss;
        ShowMessage("Müşteri kızıp gitti! -" + loss + " TL", Color.red);
        EndWrap(false);
        wrap.Clear();
        CustomerLeaves();
    }

    void NewOrder()
    {
        order.Clear();
        order.Add(Ingredient.Lavas);
        order.Add(Ingredient.Et);

        List<Ingredient> extras = new List<Ingredient> { Ingredient.Marul, Ingredient.Domates, Ingredient.Sos };
        if (OnionUnlocked) extras.Add(Ingredient.Sogan);

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

    // Malzeme eklenebilir mi? Eklenemiyorsa nedenini ekranda gösterir.
    public bool CanAddIngredient(Ingredient ing)
    {
        if (State != GameState.Playing) return false;

        if (waiting)
        {
            ShowMessage("Şu an müşteri yok.", Color.white);
            return false;
        }
        if (wrap.Count == 0 && ing != Ingredient.Lavas)
        {
            ShowMessage("Önce lavaş al!", Color.red);
            return false;
        }
        if (ing == Ingredient.Lavas && wrap.Count > 0)
        {
            ShowMessage("Zaten lavaşın var.", Color.red);
            return false;
        }
        if (wrap.Contains(ing))
        {
            ShowMessage(Name(ing) + " zaten ekli.", Color.red);
            return false;
        }

        return true;
    }

    public void AddIngredient(Ingredient ing)
    {
        if (!CanAddIngredient(ing)) return;

        wrap.Add(ing);
        ShowMessage(Name(ing) + " eklendi.", new Color(0.4f, 1f, 0.4f));
    }

    public void Serve()
    {
        if (State != GameState.Playing) return;

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
            int total = Mathf.RoundToInt((reward + bonus) * PriceMultiplier);
            Money += total;
            Served++;
            dayServed++;
            dayEarned += total;
            ShowMessage("Afiyet olsun! +" + total + " TL", new Color(1f, 0.85f, 0.2f));
            EndWrap(true);
            wrap.Clear();
            CustomerLeaves();
        }
        else
        {
            ShowMessage("Yanlış dürüm! Çöpe gitti.", Color.red);
            EndWrap(false);
            wrap.Clear();
        }
    }

    public void Trash()
    {
        if (State != GameState.Playing) return;

        EndWrap(false);
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

    void DrawUpgradeLine(float x, float y, float w, string text, bool maxed, int cost)
    {
        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.fontSize = 24;
        s.alignment = TextAnchor.MiddleCenter;

        if (maxed) s.normal.textColor = new Color(1f, 0.85f, 0.2f);
        else if (Money >= cost) s.normal.textColor = new Color(0.4f, 1f, 0.4f);
        else s.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

        GUI.Label(new Rect(x, y, w, 36f), text, s);
    }

    void DrawReport()
    {
        bool gameOver = State == GameState.GameOver;

        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        float w = 700f;
        float h = gameOver ? 520f : 680f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.fontSize = 40;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.normal.textColor = gameOver ? Color.red : new Color(1f, 0.85f, 0.2f);
        GUI.Label(new Rect(x, y + 20f, w, 60f), gameOver ? "OYUN BİTTİ" : "GÜN " + day + " BİTTİ", title);

        GUIStyle line = new GUIStyle(GUI.skin.label);
        line.fontSize = 28;
        line.alignment = TextAnchor.MiddleCenter;
        line.normal.textColor = Color.white;

        float ly = y + 100f;
        float step = 48f;

        GUI.Label(new Rect(x, ly, w, 40f), "Servis edilen müşteri: " + dayServed, line);
        GUI.Label(new Rect(x, ly + step, w, 40f), "Kaçan müşteri: " + dayMissed, line);

        line.normal.textColor = new Color(0.4f, 1f, 0.4f);
        GUI.Label(new Rect(x, ly + step * 2f, w, 40f), "Kazanç: +" + dayEarned + " TL", line);

        line.normal.textColor = new Color(1f, 0.5f, 0.5f);
        GUI.Label(new Rect(x, ly + step * 3f, w, 40f), "Ceza: -" + dayPenalty + " TL", line);

        if (gameOver)
            GUI.Label(new Rect(x, ly + step * 4f, w, 40f), "Kira: " + rentDue + " TL (ödenemedi!)", line);
        else
            GUI.Label(new Rect(x, ly + step * 4f, w, 40f), "Kira: -" + rentDue + " TL", line);

        line.normal.textColor = Color.white;
        GUI.Label(new Rect(x, ly + step * 5f, w, 40f), "Kasadaki para: " + Money + " TL", line);

        if (!gameOver)
        {
            GUIStyle up = new GUIStyle(line);
            up.fontSize = 24;
            up.fontStyle = FontStyle.Bold;
            up.normal.textColor = new Color(1f, 0.85f, 0.2f);
            GUI.Label(new Rect(x, y + 395f, w, 36f), "YÜKSELTMELER", up);

            bool speedMax = SpeedLevel >= MaxLevel;
            string speedText = speedMax
                ? "[1] Hız  (MAKS)"
                : "[1] Hız  Sv." + SpeedLevel + "/" + MaxLevel + "  -  " + NextSpeedCost() + " TL";
            DrawUpgradeLine(x, y + 435f, w, speedText, speedMax, NextSpeedCost());

            bool priceMax = PriceLevel >= MaxLevel;
            string priceText = priceMax
                ? "[2] Fiyat  (MAKS)"
                : "[2] Fiyat  Sv." + PriceLevel + "/" + MaxLevel + "  -  " + NextPriceCost() + " TL";
            DrawUpgradeLine(x, y + 475f, w, priceText, priceMax, NextPriceCost());

            string onionText = OnionUnlocked
                ? "[3] Soğan istasyonu  (SATIN ALINDI)"
                : "[3] Soğan istasyonu  -  " + OnionCost + " TL";
            DrawUpgradeLine(x, y + 515f, w, onionText, OnionUnlocked, OnionCost);

            if (messageTimer > 0f)
            {
                GUIStyle msg = new GUIStyle(line);
                msg.fontSize = 24;
                msg.normal.textColor = messageColor;
                GUI.Label(new Rect(x, y + h - 95f, w, 36f), message, msg);
            }
        }

        GUIStyle hint = new GUIStyle(line);
        hint.fontSize = 24;
        hint.normal.textColor = new Color(1f, 0.85f, 0.2f);
        GUI.Label(new Rect(x, y + h - 50f, w, 40f),
            gameOver ? "Yeniden başlamak için R'ye bas" : "Yeni güne başlamak için ENTER'a bas", hint);
    }

    void OnGUI()
    {
        if (State != GameState.Playing)
        {
            DrawReport();
            return;
        }

        DrawOrderBubble();

        GUIStyle label = new GUIStyle(GUI.skin.label);
        label.fontSize = 26;
        label.normal.textColor = Color.white;

        int secs = Mathf.CeilToInt(dayTimeLeft);
        string timeText = (secs / 60) + ":" + (secs % 60).ToString("00");

        GUI.Label(new Rect(20, 20, 1100, 40),
            "Gün " + day + "   |   Süre: " + timeText + "   |   Para: " + Money + " TL   |   Servis: " + Served + "   |   Kaçan: " + Missed, label);

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
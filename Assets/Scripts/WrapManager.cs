using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum Ingredient { Lavas, Et, Marul, Domates, Sos, Sogan }

public enum GameState { Menu, Playing, Paused, DayReport, GameOver }

public enum GameSound
{
    IngredientAdded,
    CustomerArrived,
    Served,
    Wrong,
    Trashed,
    CustomerAngry,
    DayEnd,
    GameOver,
    Upgrade,
    Denied
}

// Kayıt dosyasının içeriği
[System.Serializable]
public class SaveData
{
    public int Day;
    public int Money;
    public int SpeedLevel;
    public int PriceLevel;
    public bool OnionUnlocked;
    public int Served;
    public int Missed;
}

public class WrapManager : MonoBehaviour
{
    public static WrapManager Instance { get; private set; }

    [Header("Müşteriler")]
    [Tooltip("Müşteri noktaları. Sıra önemli: ilk müşteri 0. noktaya gelir.")]
    public CustomerSlot[] Slots;
    [Tooltip("Aynı anda en fazla kaç müşteri olabilir (nokta sayısından fazla olamaz)")]
    public int MaxCustomers = 3;
    [Tooltip("Yeni müşteri kaç saniyede bir gelir")]
    public float SpawnInterval = 20f;
    [Tooltip("Hiç müşteri kalmayınca yenisi en geç kaç saniyede gelir")]
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

    // Ses çalınması gereken olaylarda çağrılır (AudioManager dinler)
    public event System.Action<GameSound> SoundRequested;

    public float SpeedMultiplier
    {
        get { return 1f + SpeedLevel * SpeedBonusPerLevel; }
    }

    float PriceMultiplier
    {
        get { return 1f + PriceLevel * PriceBonusPerLevel; }
    }

    // ---------- Arayüzün (GameUI) okuduğu bilgiler ----------

    public int Day { get { return day; } }
    public float DayTimeLeft { get { return dayTimeLeft; } }
    public int ActiveCustomerCount { get { return ActiveCount(); } }
    public int SlotCount { get { return active.Length; } }
    public string MessageText { get { return messageTimer > 0f ? message : ""; } }
    public Color MessageColor { get { return messageColor; } }

    public bool WrapHas(Ingredient i)
    {
        return wrap.Contains(i);
    }

    public bool TryGetCustomer(int index, out Transform anchor, out List<Ingredient> order, out float patienceRatio)
    {
        anchor = null;
        order = null;
        patienceRatio = 0f;

        if (index < 0 || index >= active.Length || active[index] == null) return false;

        Customer c = active[index];
        if (c.Slot == null) return false;

        anchor = c.Slot.transform;
        order = c.Order;
        patienceRatio = Mathf.Clamp01(c.PatienceLeft / OrderTime);
        return true;
    }

    const int MaxLevel = 3;
    const string SaveKey = "DonerSimulatorSave";

    static readonly string[] Names = { "Lavaş", "Et", "Marul", "Domates", "Sos", "Soğan" };

    class Customer
    {
        public CustomerSlot Slot;
        public List<Ingredient> Order = new List<Ingredient>();
        public float PatienceLeft;
    }

    List<Ingredient> wrap = new List<Ingredient>();
    Customer[] active = new Customer[0];
    float spawnTimer;

    string message = "";
    Color messageColor = Color.white;
    float messageTimer;

    int startMoney;
    int day = 1;
    float dayTimeLeft;
    int dayEarned;
    int dayPenalty;
    int dayServed;
    int dayMissed;
    int rentDue;

    SaveData menuSave;

    public static string Name(Ingredient i)
    {
        return Names[(int)i];
    }

    void Awake()
    {
        Instance = this;
        startMoney = Money;

        int n = Slots != null ? Slots.Length : 0;
        active = new Customer[n];
    }

    void Start()
    {
        if (Slots == null || Slots.Length == 0)
        {
            Debug.LogError("WrapManager: Slots listesi boş. Müşteri noktalarını Inspector'dan ekle.");
        }

        day = 1;
        GoToMenu();
    }

    // ---------- Kayıt sistemi ----------

    void SaveGame(int nextDay)
    {
        SaveData d = new SaveData();
        d.Day = nextDay;
        d.Money = Money;
        d.SpeedLevel = SpeedLevel;
        d.PriceLevel = PriceLevel;
        d.OnionUnlocked = OnionUnlocked;
        d.Served = Served;
        d.Missed = Missed;

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
    }

    SaveData LoadSave()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return null;

        try
        {
            return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    // ---------- Menü ve oyun akışı ----------

    void GoToMenu()
    {
        State = GameState.Menu;
        wrap.Clear();
        messageTimer = 0f;
        ClearCustomers();
        menuSave = LoadSave();
    }

    void NewGame()
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

    void ContinueGame()
    {
        if (menuSave == null) return;

        day = Mathf.Max(1, menuSave.Day);
        Money = menuSave.Money;
        SpeedLevel = Mathf.Clamp(menuSave.SpeedLevel, 0, MaxLevel);
        PriceLevel = Mathf.Clamp(menuSave.PriceLevel, 0, MaxLevel);
        OnionUnlocked = menuSave.OnionUnlocked;
        Served = menuSave.Served;
        Missed = menuSave.Missed;
        StartDay();
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void EndWrap(bool served)
    {
        if (WrapEnded != null) WrapEnded(served);
    }

    void Sound(GameSound sound)
    {
        if (SoundRequested != null) SoundRequested(sound);
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
        return PriceCost * (PriceLevel + 1);
    }

    int ActiveCount()
    {
        int count = 0;
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i] != null) count++;
        }
        return count;
    }

    int CustomerLimit()
    {
        return Mathf.Min(MaxCustomers, active.Length);
    }

    void ClearCustomers()
    {
        for (int i = 0; i < active.Length; i++)
        {
            active[i] = null;
            if (Slots[i] != null) Slots[i].Hide();
        }
    }

    bool SpawnCustomer()
    {
        int limit = CustomerLimit();
        for (int i = 0; i < limit; i++)
        {
            if (active[i] != null) continue;
            if (Slots[i] == null) continue;

            Customer c = new Customer();
            c.Slot = Slots[i];
            c.Order = MakeOrder();
            c.PatienceLeft = OrderTime;

            Slots[i].Show();
            active[i] = c;
            Sound(GameSound.CustomerArrived);
            return true;
        }
        return false;
    }

    void RemoveCustomer(int index)
    {
        if (active[index] != null && active[index].Slot != null) active[index].Slot.Hide();
        active[index] = null;

        // Hiç müşteri kalmadıysa bir sonraki çabuk gelsin
        if (ActiveCount() == 0) spawnTimer = Mathf.Min(spawnTimer, NextCustomerDelay);
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

        ClearCustomers();
        SpawnCustomer();
        spawnTimer = SpawnInterval;
    }

    void EndDay()
    {
        wrap.Clear();
        messageTimer = 0f;
        ClearCustomers();

        rentDue = CurrentRent();
        if (Money >= rentDue)
        {
            Money -= rentDue;
            State = GameState.DayReport;
            SaveGame(day + 1);
            Sound(GameSound.DayEnd);
        }
        else
        {
            State = GameState.GameOver;
            DeleteSave();
            Sound(GameSound.GameOver);
        }
    }

    void BuySpeed()
    {
        if (SpeedLevel >= MaxLevel)
        {
            ShowMessage("Hız zaten son seviyede.", Color.white);
            Sound(GameSound.Denied);
            return;
        }

        int cost = NextSpeedCost();
        if (Money < cost)
        {
            ShowMessage("Yeterli paran yok!", Color.red);
            Sound(GameSound.Denied);
            return;
        }

        Money -= cost;
        SpeedLevel++;
        SaveGame(day + 1);
        ShowMessage("Hız yükseltildi! (Seviye " + SpeedLevel + "/" + MaxLevel + ")", new Color(0.4f, 1f, 0.4f));
        Sound(GameSound.Upgrade);
    }

    void BuyPrice()
    {
        if (PriceLevel >= MaxLevel)
        {
            ShowMessage("Fiyat zaten son seviyede.", Color.white);
            Sound(GameSound.Denied);
            return;
        }

        int cost = NextPriceCost();
        if (Money < cost)
        {
            ShowMessage("Yeterli paran yok!", Color.red);
            Sound(GameSound.Denied);
            return;
        }

        Money -= cost;
        PriceLevel++;
        SaveGame(day + 1);
        ShowMessage("Fiyat yükseltildi! (Seviye " + PriceLevel + "/" + MaxLevel + ")", new Color(0.4f, 1f, 0.4f));
        Sound(GameSound.Upgrade);
    }

    void BuyOnion()
    {
        if (OnionUnlocked)
        {
            ShowMessage("Soğan zaten açık.", Color.white);
            Sound(GameSound.Denied);
            return;
        }

        if (Money < OnionCost)
        {
            ShowMessage("Yeterli paran yok!", Color.red);
            Sound(GameSound.Denied);
            return;
        }

        Money -= OnionCost;
        OnionUnlocked = true;
        SaveGame(day + 1);
        ShowMessage("Soğan istasyonu açıldı!", new Color(0.4f, 1f, 0.4f));
        Sound(GameSound.Upgrade);
    }

    void Update()
    {
        if (messageTimer > 0f) messageTimer -= Time.deltaTime;

        Keyboard kb = Keyboard.current;

        if (State == GameState.Menu)
        {
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            {
                if (menuSave != null) ContinueGame();
                else Sound(GameSound.Denied);
            }
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) NewGame();
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) QuitGame();
            return;
        }

        if (State == GameState.Paused)
        {
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame || kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            {
                State = GameState.Playing;
                return;
            }
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            {
                GoToMenu();
                return;
            }
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) QuitGame();
            return;
        }

        if (State == GameState.DayReport)
        {
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) BuySpeed();
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) BuyPrice();
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) BuyOnion();

            if (kb.mKey.wasPressedThisFrame)
            {
                GoToMenu();
                return;
            }

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                day++;
                StartDay();
            }
            return;
        }

        if (State == GameState.GameOver)
        {
            if (kb == null) return;

            if (kb.rKey.wasPressedThisFrame) NewGame();
            else if (kb.mKey.wasPressedThisFrame) GoToMenu();
            return;
        }

        // ---------- Oyun sırasında ----------

        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            State = GameState.Paused;
            return;
        }

        dayTimeLeft -= Time.deltaTime;
        if (dayTimeLeft <= 0f)
        {
            dayTimeLeft = 0f;
            EndDay();
            return;
        }

        // Yeni müşteri zamanı
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = SpawnCustomer() ? SpawnInterval : 1f;
        }

        // Her müşterinin sabrı ayrı azalır
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i] == null) continue;

            active[i].PatienceLeft -= Time.deltaTime;
            if (active[i].PatienceLeft <= 0f) CustomerAngry(i);
        }
    }

    void ShowMessage(string text, Color color)
    {
        message = text;
        messageColor = color;
        messageTimer = 2.5f;
    }

    void CustomerAngry(int index)
    {
        int loss = Mathf.Min(Money, AngryPenalty);
        Money -= loss;
        Missed++;
        dayMissed++;
        dayPenalty += loss;
        ShowMessage("Müşteri kızıp gitti! -" + loss + " TL", Color.red);
        Sound(GameSound.CustomerAngry);
        RemoveCustomer(index);
    }

    List<Ingredient> MakeOrder()
    {
        List<Ingredient> result = new List<Ingredient>();
        result.Add(Ingredient.Lavas);
        result.Add(Ingredient.Et);

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
        for (int i = 0; i < count; i++) result.Add(extras[i]);

        return result;
    }

    // Elindeki dürüm bu siparişle birebir aynı mı?
    bool WrapMatches(List<Ingredient> order)
    {
        if (wrap.Count != order.Count) return false;

        foreach (Ingredient o in order)
        {
            if (!wrap.Contains(o)) return false;
        }
        return true;
    }

    // Malzeme eklenebilir mi? Eklenemiyorsa nedenini ekranda gösterir.
    public bool CanAddIngredient(Ingredient ing)
    {
        if (State != GameState.Playing) return false;

        if (ActiveCount() == 0)
        {
            ShowMessage("Şu an müşteri yok.", Color.white);
            Sound(GameSound.Denied);
            return false;
        }
        if (wrap.Count == 0 && ing != Ingredient.Lavas)
        {
            ShowMessage("Önce lavaş al!", Color.red);
            Sound(GameSound.Denied);
            return false;
        }
        if (ing == Ingredient.Lavas && wrap.Count > 0)
        {
            ShowMessage("Zaten lavaşın var.", Color.red);
            Sound(GameSound.Denied);
            return false;
        }
        if (wrap.Contains(ing))
        {
            ShowMessage(Name(ing) + " zaten ekli.", Color.red);
            Sound(GameSound.Denied);
            return false;
        }

        return true;
    }

    public void AddIngredient(Ingredient ing)
    {
        if (!CanAddIngredient(ing)) return;

        wrap.Add(ing);
        ShowMessage(Name(ing) + " eklendi.", new Color(0.4f, 1f, 0.4f));
        Sound(GameSound.IngredientAdded);
    }

    public void Serve()
    {
        if (State != GameState.Playing) return;

        if (ActiveCount() == 0)
        {
            ShowMessage("Şu an müşteri yok.", Color.white);
            Sound(GameSound.Denied);
            return;
        }

        if (wrap.Count == 0)
        {
            ShowMessage("Elinde dürüm yok!", Color.red);
            Sound(GameSound.Denied);
            return;
        }

        // Siparişi dürümle uyan ilk müşteriyi bul
        int match = -1;
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i] != null && WrapMatches(active[i].Order))
            {
                match = i;
                break;
            }
        }

        if (match >= 0)
        {
            Customer c = active[match];
            int reward = 30 + c.Order.Count * 10;
            int bonus = Mathf.RoundToInt(20f * Mathf.Clamp01(c.PatienceLeft / OrderTime));
            int total = Mathf.RoundToInt((reward + bonus) * PriceMultiplier);
            Money += total;
            Served++;
            dayServed++;
            dayEarned += total;
            ShowMessage("Afiyet olsun! +" + total + " TL", new Color(1f, 0.85f, 0.2f));
            Sound(GameSound.Served);
            EndWrap(true);
            wrap.Clear();
            RemoveCustomer(match);
        }
        else
        {
            ShowMessage("Hiçbir müşterinin siparişi bu değil! Çöpe gitti.", Color.red);
            Sound(GameSound.Wrong);
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
        Sound(GameSound.Trashed);
    }

    void DrawMenu()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        float w = 700f;
        float h = 460f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.fontSize = 48;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.normal.textColor = new Color(1f, 0.85f, 0.2f);
        GUI.Label(new Rect(x, y + 30f, w, 70f), "DÖNER SİMÜLATÖRÜ", title);

        GUIStyle line = new GUIStyle(GUI.skin.label);
        line.fontSize = 30;
        line.alignment = TextAnchor.MiddleCenter;

        if (menuSave != null)
        {
            line.normal.textColor = new Color(0.4f, 1f, 0.4f);
            GUI.Label(new Rect(x, y + 140f, w, 44f),
                "[1] Devam Et  (Gün " + menuSave.Day + "  -  " + menuSave.Money + " TL)", line);
        }
        else
        {
            line.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            GUI.Label(new Rect(x, y + 140f, w, 44f), "[1] Devam Et  (kayıt yok)", line);
        }

        line.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y + 200f, w, 44f), "[2] Yeni Oyun", line);
        GUI.Label(new Rect(x, y + 260f, w, 44f), "[3] Çıkış", line);

        GUIStyle hint = new GUIStyle(GUI.skin.label);
        hint.fontSize = 20;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(x, y + h - 70f, w, 36f), "Oyun her gün sonunda otomatik kaydedilir.", hint);
    }

    void DrawPause()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        float w = 700f;
        float h = 420f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.fontSize = 44;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.normal.textColor = new Color(1f, 0.85f, 0.2f);
        GUI.Label(new Rect(x, y + 30f, w, 60f), "DURAKLATILDI", title);

        GUIStyle line = new GUIStyle(GUI.skin.label);
        line.fontSize = 30;
        line.alignment = TextAnchor.MiddleCenter;
        line.normal.textColor = Color.white;

        GUI.Label(new Rect(x, y + 120f, w, 44f), "[1] Devam Et  (ESC)", line);
        GUI.Label(new Rect(x, y + 180f, w, 44f), "[2] Ana Menü", line);
        GUI.Label(new Rect(x, y + 240f, w, 44f), "[3] Çıkış", line);

        GUIStyle hint = new GUIStyle(GUI.skin.label);
        hint.fontSize = 20;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(x, y + h - 70f, w, 36f), "Ana menüye dönersen bugünkü ilerleme kaybolur.", hint);
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
            gameOver ? "R: yeniden başla   |   M: ana menü" : "ENTER: yeni gün   |   M: ana menü", hint);
    }

    void OnGUI()
    {
        // Oyun sırasındaki arayüz artık GameUI (Canvas) tarafından çiziliyor
        if (State == GameState.Menu)
        {
            DrawMenu();
        }
        else if (State == GameState.Paused)
        {
            DrawPause();
        }
        else if (State == GameState.DayReport || State == GameState.GameOver)
        {
            DrawReport();
        }
    }
}
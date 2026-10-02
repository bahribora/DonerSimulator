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

    public bool HasSave { get { return menuSave != null; } }
    public int SaveDay { get { return menuSave != null ? menuSave.Day : 0; } }
    public int SaveMoney { get { return menuSave != null ? menuSave.Money : 0; } }

    public int DayServed { get { return dayServed; } }
    public int DayMissed { get { return dayMissed; } }
    public int DayEarned { get { return dayEarned; } }
    public int DayPenalty { get { return dayPenalty; } }
    public int RentDue { get { return rentDue; } }
    public int MaxUpgradeLevel { get { return MaxLevel; } }

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

    public int NextSpeedCost()
    {
        return SpeedCost * (SpeedLevel + 1);
    }

    public int NextPriceCost()
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
}
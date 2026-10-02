using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    class Bubble
    {
        public RectTransform Rect;
        public RectTransform Fill;
        public Text[] Lines = new Text[6];
    }

    static readonly Color Gold = new Color(1f, 0.85f, 0.2f);
    static readonly Color Green = new Color(0.4f, 1f, 0.4f);
    static readonly Color Grey = new Color(0.5f, 0.5f, 0.5f);
    static readonly Color Pink = new Color(1f, 0.5f, 0.5f);

    Canvas canvas;
    RectTransform canvasRect;
    Font font;
    FirstPersonController player;

    GameObject hudRoot;
    Text statsText;
    Text handText;
    GameObject waitingPanel;
    Text messageText;

    GameObject crossRoot;
    Text promptText;
    GameObject holdRoot;
    RectTransform holdFill;

    GameObject screenRoot;
    RectTransform panelRect;
    Text screenTitle;
    Text[] screenLines = new Text[12];
    Text screenHint;

    Bubble[] bubbles = new Bubble[0];

    void Awake()
    {
        font = GetFont();
        BuildCanvas();
        BuildHud();
        BuildCrosshair();
        BuildScreen();
    }

    void Start()
    {
        player = FindFirstObjectByType<FirstPersonController>();

        WrapManager wm = WrapManager.Instance;
        int count = wm != null ? wm.SlotCount : 0;
        bubbles = new Bubble[count];
        for (int i = 0; i < count; i++)
        {
            bubbles[i] = MakeBubble();
            bubbles[i].Rect.gameObject.SetActive(false);
        }
    }

    // ---------- Kurulum ----------

    Font GetFont()
    {
        Font f = null;
        try
        {
            f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch (System.Exception)
        {
            f = null;
        }

        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 24);
        return f;
    }

    void BuildCanvas()
    {
        GameObject g = new GameObject("GameCanvas", typeof(RectTransform));
        g.transform.SetParent(transform, false);

        canvas = g.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = g.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = g.GetComponent<RectTransform>();
    }

    RectTransform NewRect(string objName, Transform parent)
    {
        GameObject g = new GameObject(objName, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        return g.GetComponent<RectTransform>();
    }

    Image NewImage(string objName, Transform parent, Color color)
    {
        RectTransform rt = NewRect(objName, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text NewText(string objName, Transform parent, int size, Color color, TextAnchor anchor)
    {
        RectTransform rt = NewRect(objName, parent);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    void AddOutline(Text t)
    {
        UnityEngine.UI.Outline o = t.gameObject.AddComponent<UnityEngine.UI.Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.9f);
        o.effectDistance = new Vector2(2f, -2f);
    }

    // Sol üst köşeye yaslı panel
    RectTransform TopLeftPanel(string objName, float x, float y, float w, float h)
    {
        Image bg = NewImage(objName, hudRoot.transform, new Color(0f, 0f, 0f, 0.55f));
        RectTransform rt = bg.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    // Parent'ın içini dolduran, kenarlardan boşluk bırakan yazı
    Text PaddedText(RectTransform parent, int size, Color color, TextAnchor anchor)
    {
        Text t = NewText("Text", parent, size, color, anchor);
        RectTransform rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(18f, 0f);
        rt.offsetMax = new Vector2(-18f, 0f);
        return t;
    }

    // Üste yaslı, yatayda genişleyen eleman
    void SetTopStretch(RectTransform rt, float left, float right, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }

    void BuildHud()
    {
        hudRoot = NewRect("Hud", canvas.transform).gameObject;
        RectTransform hudRect = hudRoot.GetComponent<RectTransform>();
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.offsetMin = Vector2.zero;
        hudRect.offsetMax = Vector2.zero;

        RectTransform statsPanel = TopLeftPanel("StatsPanel", 20f, 20f, 1150f, 64f);
        statsText = PaddedText(statsPanel, 34, Color.white, TextAnchor.MiddleLeft);

        RectTransform handPanel = TopLeftPanel("HandPanel", 20f, 94f, 900f, 52f);
        handText = PaddedText(handPanel, 28, Color.white, TextAnchor.MiddleLeft);

        RectTransform waitPanel = TopLeftPanel("WaitingPanel", 20f, 156f, 520f, 48f);
        Text waitText = PaddedText(waitPanel, 26, Gold, TextAnchor.MiddleLeft);
        waitText.text = "Müşteri bekleniyor...";
        waitingPanel = waitPanel.gameObject;

        messageText = NewText("Message", hudRoot.transform, 40, Color.white, TextAnchor.MiddleCenter);
        messageText.fontStyle = FontStyle.Bold;
        AddOutline(messageText);
        RectTransform mr = messageText.rectTransform;
        mr.anchorMin = new Vector2(0.5f, 0f);
        mr.anchorMax = new Vector2(0.5f, 0f);
        mr.pivot = new Vector2(0.5f, 0f);
        mr.anchoredPosition = new Vector2(0f, 70f);
        mr.sizeDelta = new Vector2(1500f, 60f);
    }

    void BuildCrosshair()
    {
        crossRoot = NewRect("Crosshair", canvas.transform).gameObject;
        RectTransform cr = crossRoot.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.5f, 0.5f);
        cr.anchorMax = new Vector2(0.5f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f);
        cr.anchoredPosition = Vector2.zero;
        cr.sizeDelta = Vector2.zero;

        Image dot = NewImage("Dot", crossRoot.transform, new Color(1f, 1f, 1f, 0.9f));
        dot.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        dot.rectTransform.anchoredPosition = Vector2.zero;
        dot.rectTransform.sizeDelta = new Vector2(8f, 8f);

        promptText = NewText("Prompt", crossRoot.transform, 32, Color.white, TextAnchor.MiddleCenter);
        AddOutline(promptText);
        RectTransform pr = promptText.rectTransform;
        pr.anchorMin = new Vector2(0.5f, 0.5f);
        pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = new Vector2(0f, -60f);
        pr.sizeDelta = new Vector2(900f, 44f);

        holdRoot = NewRect("HoldBar", crossRoot.transform).gameObject;
        RectTransform hr = holdRoot.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0.5f, 0.5f);
        hr.anchorMax = new Vector2(0.5f, 0.5f);
        hr.pivot = new Vector2(0.5f, 0.5f);
        hr.anchoredPosition = new Vector2(0f, -120f);
        hr.sizeDelta = new Vector2(360f, 26f);

        Image holdBg = holdRoot.AddComponent<Image>();
        holdBg.color = new Color(0f, 0f, 0f, 0.75f);
        holdBg.raycastTarget = false;

        Image fill = NewImage("Fill", holdRoot.transform, new Color(0.9f, 0.5f, 0.2f, 1f));
        holdFill = fill.rectTransform;
        holdFill.anchorMin = new Vector2(0f, 0f);
        holdFill.anchorMax = new Vector2(0f, 1f);
        holdFill.offsetMin = new Vector2(4f, 4f);
        holdFill.offsetMax = new Vector2(-4f, -4f);

        Text label = NewText("Label", holdRoot.transform, 26, Color.white, TextAnchor.MiddleCenter);
        AddOutline(label);
        label.text = "Hazırlanıyor...";
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0.5f, 0f);
        lr.anchorMax = new Vector2(0.5f, 0f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.anchoredPosition = new Vector2(0f, -6f);
        lr.sizeDelta = new Vector2(400f, 34f);

        holdRoot.SetActive(false);
    }

    // Menü, duraklatma ve gün raporu için ortak ekran
    void BuildScreen()
    {
        Image dim = NewImage("Screen", canvas.transform, new Color(0f, 0f, 0f, 0.8f));
        screenRoot = dim.gameObject;
        RectTransform dr = dim.rectTransform;
        dr.anchorMin = Vector2.zero;
        dr.anchorMax = Vector2.one;
        dr.offsetMin = Vector2.zero;
        dr.offsetMax = Vector2.zero;

        Image panel = NewImage("Panel", screenRoot.transform, new Color(0.12f, 0.12f, 0.12f, 1f));
        panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(780f, 600f);

        screenTitle = NewText("Title", panelRect, 54, Gold, TextAnchor.MiddleCenter);
        screenTitle.fontStyle = FontStyle.Bold;
        SetTopStretch(screenTitle.rectTransform, 0f, 0f, 25f, 70f);

        for (int i = 0; i < screenLines.Length; i++)
        {
            screenLines[i] = NewText("Line" + i, panelRect, 32, Color.white, TextAnchor.MiddleCenter);
            SetTopStretch(screenLines[i].rectTransform, 20f, 20f, 110f + i * 44f, 40f);
        }

        screenHint = NewText("Hint", panelRect, 26, Gold, TextAnchor.MiddleCenter);
        RectTransform hr = screenHint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.offsetMin = new Vector2(20f, 18f);
        hr.offsetMax = new Vector2(-20f, 58f);

        screenRoot.SetActive(false);
    }

    Bubble MakeBubble()
    {
        Bubble b = new Bubble();

        RectTransform root = NewRect("Bubble", canvas.transform);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(280f, 200f);
        b.Rect = root;

        Image bg = root.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);
        bg.raycastTarget = false;

        Text title = NewText("Title", root, 28, Color.white, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        title.text = "SİPARİŞ";
        SetTopStretch(title.rectTransform, 0f, 0f, 8f, 36f);

        Image barBg = NewImage("BarBg", root, new Color(0.2f, 0.2f, 0.2f, 1f));
        SetTopStretch(barBg.rectTransform, 18f, 18f, 50f, 12f);

        Image barFill = NewImage("BarFill", barBg.transform, Color.green);
        b.Fill = barFill.rectTransform;
        b.Fill.anchorMin = Vector2.zero;
        b.Fill.anchorMax = Vector2.one;
        b.Fill.offsetMin = Vector2.zero;
        b.Fill.offsetMax = Vector2.zero;

        for (int i = 0; i < b.Lines.Length; i++)
        {
            Text line = NewText("Line" + i, root, 24, Color.white, TextAnchor.MiddleLeft);
            SetTopStretch(line.rectTransform, 20f, 20f, 72f + i * 32f, 30f);
            b.Lines[i] = line;
        }

        return b;
    }

    // ---------- Her kare ----------

    void Update()
    {
        WrapManager wm = WrapManager.Instance;
        if (wm == null) return;

        bool playing = wm.State == GameState.Playing;

        if (hudRoot.activeSelf != playing) hudRoot.SetActive(playing);
        if (crossRoot.activeSelf != playing) crossRoot.SetActive(playing);
        if (screenRoot.activeSelf == playing) screenRoot.SetActive(!playing);

        if (!playing)
        {
            for (int i = 0; i < bubbles.Length; i++)
            {
                if (bubbles[i].Rect.gameObject.activeSelf) bubbles[i].Rect.gameObject.SetActive(false);
            }
            UpdateScreen(wm);
            return;
        }

        UpdateHud(wm);
        UpdateBubbles(wm);
        UpdateCrosshair();
    }

    void SetLine(int index, string text, Color color)
    {
        screenLines[index].text = text;
        screenLines[index].color = color;
    }

    void ShowScreen(string title, Color titleColor, int lineCount, string hint)
    {
        screenTitle.text = title;
        screenTitle.color = titleColor;
        screenHint.text = hint;
        panelRect.sizeDelta = new Vector2(780f, 110f + lineCount * 44f + 90f);
    }

    Color UpgradeColor(bool maxed, int cost, int money)
    {
        if (maxed) return Gold;
        return money >= cost ? Green : Grey;
    }

    void UpdateScreen(WrapManager wm)
    {
        for (int i = 0; i < screenLines.Length; i++)
        {
            screenLines[i].text = "";
            screenLines[i].color = Color.white;
        }

        switch (wm.State)
        {
            case GameState.Menu:
                if (wm.HasSave)
                    SetLine(0, "[1] Devam Et  (Gün " + wm.SaveDay + "  -  " + wm.SaveMoney + " TL)", Green);
                else
                    SetLine(0, "[1] Devam Et  (kayıt yok)", Grey);
                SetLine(1, "[2] Yeni Oyun", Color.white);
                SetLine(2, "[3] Çıkış", Color.white);
                ShowScreen("DÖNER SİMÜLATÖRÜ", Gold, 3, "Oyun her gün sonunda otomatik kaydedilir.");
                break;

            case GameState.Paused:
                SetLine(0, "[1] Devam Et  (ESC)", Color.white);
                SetLine(1, "[2] Ana Menü", Color.white);
                SetLine(2, "[3] Çıkış", Color.white);
                ShowScreen("DURAKLATILDI", Gold, 3, "Ana menüye dönersen bugünkü ilerleme kaybolur.");
                break;

            case GameState.DayReport:
            case GameState.GameOver:
                DrawReportScreen(wm);
                break;
        }
    }

    void DrawReportScreen(WrapManager wm)
    {
        bool gameOver = wm.State == GameState.GameOver;
        int max = wm.MaxUpgradeLevel;

        SetLine(0, "Servis edilen müşteri: " + wm.DayServed, Color.white);
        SetLine(1, "Kaçan müşteri: " + wm.DayMissed, Color.white);
        SetLine(2, "Kazanç: +" + wm.DayEarned + " TL", Green);
        SetLine(3, "Ceza: -" + wm.DayPenalty + " TL", Pink);

        if (gameOver)
            SetLine(4, "Kira: " + wm.RentDue + " TL (ödenemedi!)", Pink);
        else
            SetLine(4, "Kira: -" + wm.RentDue + " TL", Pink);

        SetLine(5, "Kasadaki para: " + wm.Money + " TL", Color.white);

        if (gameOver)
        {
            ShowScreen("OYUN BİTTİ", Color.red, 6, "R: yeniden başla   |   M: ana menü");
            return;
        }

        SetLine(6, "YÜKSELTMELER", Gold);

        bool speedMax = wm.SpeedLevel >= max;
        string speedText = speedMax
            ? "[1] Hız  (MAKS)"
            : "[1] Hız  Sv." + wm.SpeedLevel + "/" + max + "  -  " + wm.NextSpeedCost() + " TL";
        SetLine(7, speedText, UpgradeColor(speedMax, wm.NextSpeedCost(), wm.Money));

        bool priceMax = wm.PriceLevel >= max;
        string priceText = priceMax
            ? "[2] Fiyat  (MAKS)"
            : "[2] Fiyat  Sv." + wm.PriceLevel + "/" + max + "  -  " + wm.NextPriceCost() + " TL";
        SetLine(8, priceText, UpgradeColor(priceMax, wm.NextPriceCost(), wm.Money));

        string onionText = wm.OnionUnlocked
            ? "[3] Soğan istasyonu  (SATIN ALINDI)"
            : "[3] Soğan istasyonu  -  " + wm.OnionCost + " TL";
        SetLine(9, onionText, UpgradeColor(wm.OnionUnlocked, wm.OnionCost, wm.Money));

        SetLine(10, wm.MessageText, wm.MessageColor);

        ShowScreen("GÜN " + wm.Day + " BİTTİ", Gold, 11, "ENTER: yeni gün   |   M: ana menü");
    }

    void UpdateHud(WrapManager wm)
    {
        int secs = Mathf.CeilToInt(wm.DayTimeLeft);
        string time = (secs / 60) + ":" + (secs % 60).ToString("00");
        if (secs <= 15) time = "<color=#FF5555>" + time + "</color>";

        statsText.text =
            "<color=#FFD633>Gün " + wm.Day + "</color>   |   Süre: " + time +
            "   |   Para: <color=#7CFF7C>" + wm.Money + " TL</color>" +
            "   |   Servis: " + wm.Served +
            "   |   Kaçan: <color=#FF8888>" + wm.Missed + "</color>";

        string hand = "boş";
        if (wm.WrapContents.Count > 0)
        {
            hand = "";
            for (int i = 0; i < wm.WrapContents.Count; i++)
            {
                if (i > 0) hand += ", ";
                hand += WrapManager.Name(wm.WrapContents[i]);
            }
        }
        handText.text = "Elindeki: " + hand;

        bool noCustomer = wm.ActiveCustomerCount == 0;
        if (waitingPanel.activeSelf != noCustomer) waitingPanel.SetActive(noCustomer);

        messageText.text = wm.MessageText;
        messageText.color = wm.MessageColor;
    }

    void UpdateBubbles(WrapManager wm)
    {
        Camera cam = Camera.main;

        for (int i = 0; i < bubbles.Length; i++)
        {
            Bubble b = bubbles[i];

            Transform anchor;
            System.Collections.Generic.List<Ingredient> order;
            float ratio;

            bool has = cam != null && wm.TryGetCustomer(i, out anchor, out order, out ratio);

            if (!has)
            {
                if (b.Rect.gameObject.activeSelf) b.Rect.gameObject.SetActive(false);
                continue;
            }

            wm.TryGetCustomer(i, out anchor, out order, out ratio);

            Vector3 sp = cam.WorldToScreenPoint(anchor.position + Vector3.up * 1.9f);
            if (sp.z < 0f)
            {
                if (b.Rect.gameObject.activeSelf) b.Rect.gameObject.SetActive(false);
                continue;
            }

            if (!b.Rect.gameObject.activeSelf) b.Rect.gameObject.SetActive(true);

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(sp.x, sp.y), null, out local);
            b.Rect.anchoredPosition = local;

            int count = Mathf.Min(order.Count, b.Lines.Length);
            b.Rect.sizeDelta = new Vector2(280f, 84f + count * 32f);

            b.Fill.anchorMax = new Vector2(ratio, 1f);
            b.Fill.GetComponent<Image>().color = Color.Lerp(Color.red, Color.green, ratio);

            for (int j = 0; j < b.Lines.Length; j++)
            {
                Text line = b.Lines[j];
                if (j >= count)
                {
                    if (line.gameObject.activeSelf) line.gameObject.SetActive(false);
                    continue;
                }

                if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);

                bool have = wm.WrapHas(order[j]);
                line.text = (have ? "[X] " : "[  ] ") + WrapManager.Name(order[j]);
                line.color = have ? new Color(0.4f, 1f, 0.4f) : Color.white;
            }
        }
    }

    void UpdateCrosshair()
    {
        string prompt = player != null ? player.CurrentPrompt : null;
        promptText.text = string.IsNullOrEmpty(prompt) ? "" : "[E] " + prompt;

        float hold = player != null ? player.HoldRatio : -1f;
        bool showHold = hold >= 0f;
        if (holdRoot.activeSelf != showHold) holdRoot.SetActive(showHold);
        if (showHold) holdFill.anchorMax = new Vector2(hold, 1f);
    }
}
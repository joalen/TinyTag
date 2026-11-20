using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds and updates all UI from code: main menu, HUD (timer, per-player status
/// and stamina), countdown, and round-over screen.
/// </summary>
public class GameUI : MonoBehaviour
{
    static readonly Color Dim = new Color(0f, 0f, 0f, 0.75f);
    static readonly Color ButtonColor = new Color(1f, 0.78f, 0.25f);
    static readonly Color It = new Color(1f, 0.3f, 0.25f);

    GameManager gm;
    Font font;

    GameObject menuPanel, hudRoot, endPanel;
    Text timerText, centerText, winnerText;
    readonly Text[] statusTexts = new Text[2];
    readonly RectTransform[] staminaFills = new RectTransform[2];

    void Start()
    {
        gm = GameManager.Instance;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        Build();
    }

    void Update()
    {
        if (gm == null) return;

        HandleShortcuts(gm.CurrentState); // may change state, so read it afterwards
        var state = gm.CurrentState;

        menuPanel.SetActive(state == GameManager.State.Menu);
        endPanel.SetActive(state == GameManager.State.RoundOver);
        hudRoot.SetActive(state == GameManager.State.Countdown || state == GameManager.State.Playing);

        if (hudRoot.activeSelf) UpdateHud(state);
        if (endPanel.activeSelf && gm.WinnerIndex >= 0)
        {
            winnerText.text = $"PLAYER {gm.WinnerIndex + 1} WINS!";
            winnerText.color = gm.players[gm.WinnerIndex].normalColor;
        }
    }

    void HandleShortcuts(GameManager.State state)
    {
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                       || (pad != null && pad.startButton.wasPressedThisFrame);
        bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);

        if (state == GameManager.State.Menu)
        {
            if (confirm) gm.StartRound();
            else if (back) gm.QuitGame();
        }
        else if (state == GameManager.State.RoundOver)
        {
            if (confirm) gm.StartRound();
            else if (back) gm.GoToMenu();
        }
    }

    void UpdateHud(GameManager.State state)
    {
        int secs = Mathf.CeilToInt(gm.TimeLeft);
        timerText.text = $"{secs / 60}:{secs % 60:00}";
        timerText.color = gm.TimeLeft <= 10f ? It : Color.white;

        if (state == GameManager.State.Countdown)
            centerText.text = Mathf.CeilToInt(gm.CountdownLeft).ToString();
        else
            centerText.text = gm.TimeLeft > gm.roundLength - 1f ? "GO!" : "";

        for (int i = 0; i < gm.players.Length && i < 2; i++)
        {
            var p = gm.players[i];
            if (p.IsFrozen) statusTexts[i].text = "TAGGED!  You're it now...";
            else if (p.IsIt) statusTexts[i].text = gm.TagCooldownLeft > 0f ? "YOU'RE IT!  (wait...)" : "YOU'RE IT!  Tag them!";
            else statusTexts[i].text = "RUN!  Don't get tagged";
            statusTexts[i].color = p.IsIt ? It : p.normalColor;

            float frac = Mathf.Clamp01(p.Movement.Stamina / p.Movement.maxStamina);
            staminaFills[i].anchorMax = new Vector2(frac, 1f);
        }
    }

    // ---------------------------------------------------------------- building

    void Build()
    {
        var canvasGo = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGo.transform;

        // --- HUD
        hudRoot = Panel(root, "HUD", Color.clear);
        Panel(hudRoot.transform, "Divider", Color.black, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(6, 0));

        var timerBg = Panel(hudRoot.transform, "TimerBg", Dim, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(220, 90));
        ((RectTransform)timerBg.transform).anchoredPosition = new Vector2(0, -55);
        timerText = Label(timerBg.transform, "1:30", 64, Color.white);

        centerText = Label(hudRoot.transform, "", 220, Color.white);
        centerText.gameObject.AddComponent<Outline>().effectDistance = new Vector2(4, -4);

        for (int i = 0; i < 2; i++)
        {
            float x0 = i * 0.5f, x1 = x0 + 0.5f;
            // P1 label on the far left, P2 on the far right, so neither hits the centre timer
            var name = Label(hudRoot.transform, $"PLAYER {i + 1}", 36, Color.white, i == 0 ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
            Place(name.rectTransform, new Vector2(x0, 1f), new Vector2(x1, 1f), new Vector2(30, -80), new Vector2(-30, -20));

            statusTexts[i] = Label(hudRoot.transform, "", 44, Color.white, TextAnchor.LowerCenter);
            statusTexts[i].gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
            Place(statusTexts[i].rectTransform, new Vector2(x0, 0f), new Vector2(x1, 0f), new Vector2(0, 70), new Vector2(0, 140));

            var barBg = Panel(hudRoot.transform, $"StaminaBg{i}", Dim);
            Place((RectTransform)barBg.transform, new Vector2(x0 + 0.15f, 0f), new Vector2(x1 - 0.15f, 0f), new Vector2(0, 30), new Vector2(0, 52));
            var fill = Panel(barBg.transform, "Fill", new Color(0.4f, 0.9f, 0.4f));
            Place((RectTransform)fill.transform, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
            staminaFills[i] = (RectTransform)fill.transform;
        }

        // --- Main menu
        menuPanel = Panel(root, "Menu", new Color(0.08f, 0.06f, 0.12f, 0.85f));
        var title = Label(menuPanel.transform, "TINY TAG", 160, ButtonColor);
        title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(6, -6);
        Place(title.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 0.92f), Vector2.zero, Vector2.zero);
        var sub = Label(menuPanel.transform, "You're tiny. The bedroom is huge. Don't be \"it\" when time runs out!", 34, Color.white);
        Place(sub.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.69f), Vector2.zero, Vector2.zero);

        var controls = Label(menuPanel.transform,
            "<b>PLAYER 1</b>   WASD move · Space jump · Left Shift sprint · hold E climb · Mouse look\n" +
            "<b>PLAYER 2</b>   Arrows move · Right Ctrl (or Numpad 0) jump · Right Shift sprint · hold / climb\n" +
            "<b>GAMEPADS</b>   Left stick move · A jump · LT sprint · hold RT climb · Right stick look\n\n" +
            "Whoever is \"it\" has a red diamond over their head. Climb furniture: walk into it and hold climb.",
            28, new Color(0.85f, 0.85f, 0.9f));
        Place(controls.rectTransform, new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.58f), Vector2.zero, Vector2.zero);

        Button(menuPanel.transform, "PLAY  (Enter)", new Vector2(-170, -350), gm.StartRound);
        Button(menuPanel.transform, "QUIT  (Esc)", new Vector2(170, -350), gm.QuitGame);

        // --- Round over
        endPanel = Panel(root, "RoundOver", Dim);
        winnerText = Label(endPanel.transform, "", 130, Color.white);
        winnerText.gameObject.AddComponent<Outline>().effectDistance = new Vector2(5, -5);
        Place(winnerText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.8f), Vector2.zero, Vector2.zero);
        var hint = Label(endPanel.transform, "Time's up! Whoever wasn't \"it\" wins.", 40, Color.white);
        Place(hint.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        Button(endPanel.transform, "REMATCH  (Enter)", new Vector2(-190, -200), gm.StartRound);
        Button(endPanel.transform, "MENU  (Esc)", new Vector2(190, -200), gm.GoToMenu);
    }

    GameObject Panel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = color.a > 0.5f;
        Place((RectTransform)go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return go;
    }

    GameObject Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        var go = Panel(parent, name, color);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyle.Bold;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Place(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return t;
    }

    void Button(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = Panel(parent, label, ButtonColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(320, 80));
        ((RectTransform)go.transform).anchoredPosition = pos;
        var b = go.AddComponent<Button>();
        b.onClick.AddListener(onClick);
        var t = Label(go.transform, label, 30, new Color(0.15f, 0.1f, 0.05f));
        t.fontStyle = FontStyle.Bold;
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}

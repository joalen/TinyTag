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
    readonly Image[] trackers = new Image[2]; // arrow on player i's screen pointing at the other player
    Camera[] cams;

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
        UpdateTrackers(state);
        if (endPanel.activeSelf && gm.WinnerIndex >= 0)
        {
            winnerText.text = $"THE {gm.players[gm.WinnerIndex].displayName} WINS!";
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

    // Only the player who is "it" gets an arrow toward the other player: floating above
    // their head when visible, pinned to the edge of the screen half when not.
    void UpdateTrackers(GameManager.State state)
    {
        const float marginX = 0.07f, marginY = 0.1f;

        for (int i = 0; i < trackers.Length; i++)
        {
            var arrow = trackers[i];
            bool show = state == GameManager.State.Playing && i < gm.players.Length && gm.players[i].IsIt
                        && cams != null && i < cams.Length && cams[i] != null;
            arrow.gameObject.SetActive(show);
            if (!show) continue;

            var cam = cams[i];
            var target = gm.players[(i + 1) % gm.players.Length];
            arrow.color = target.normalColor;

            Vector3 vp = cam.WorldToViewportPoint(target.transform.position + Vector3.up * 1.4f);
            bool behind = vp.z < 0f;
            if (behind) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; } // mirror so the arrow points the right way

            bool onScreen = !behind && vp.x > marginX && vp.x < 1f - marginX && vp.y > marginY && vp.y < 1f - marginY;
            Vector2 pos;
            float angle;
            if (onScreen)
            {
                pos = new Vector2(vp.x, vp.y + Mathf.Sin(Time.time * 6f) * 0.01f);
                angle = 180f; // pointing down at their head
            }
            else
            {
                Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                if (behind && d.sqrMagnitude < 0.0001f) d = Vector2.down;
                // push out to the edge of the (margined) viewport
                float k = Mathf.Max(Mathf.Abs(d.x) / (0.5f - marginX), Mathf.Abs(d.y) / (0.5f - marginY));
                pos = new Vector2(0.5f, 0.5f) + d / Mathf.Max(k, 0.0001f);
                // angle in screen pixels (viewport is wider/taller than it is square)
                angle = Mathf.Atan2(d.y * cam.pixelHeight, d.x * cam.pixelWidth) * Mathf.Rad2Deg - 90f;
            }

            // viewport (0..1 inside this player's half) -> whole-screen normalized position
            Rect r = cam.rect;
            Vector2 screen = new Vector2(r.x + pos.x * r.width, r.y + pos.y * r.height);
            var rt = arrow.rectTransform;
            rt.anchorMin = rt.anchorMax = screen;
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
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
            string who = i < gm.players.Length ? gm.players[i].displayName : "";
            var name = Label(hudRoot.transform, $"P{i + 1}  {who}", 36, Color.white, i == 0 ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
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

        // --- "Where are they?" arrows for whoever is it
        cams = new Camera[gm.playerCameras.Length];
        for (int i = 0; i < cams.Length; i++)
            cams[i] = gm.playerCameras[i] != null ? gm.playerCameras[i].GetComponent<Camera>() : null;
        var arrowSprite = MakeArrowSprite();
        for (int i = 0; i < trackers.Length; i++)
        {
            var go = new GameObject($"Tracker P{i + 1}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var img = go.GetComponent<Image>();
            img.sprite = arrowSprite;
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(56, 56);
            go.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
            go.SetActive(false);
            trackers[i] = img;
        }

        // --- Main menu
        menuPanel = Panel(root, "Menu", new Color(0.08f, 0.06f, 0.12f, 0.85f));
        var title = Label(menuPanel.transform, "TINY TAG", 160, ButtonColor);
        title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(6, -6);
        Place(title.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 0.92f), Vector2.zero, Vector2.zero);
        var sub = Label(menuPanel.transform, "A rubber duck and a rubber frog. One huge bedroom. Don't be \"it\" when time runs out!", 34, Color.white);
        Place(sub.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.69f), Vector2.zero, Vector2.zero);

        var controls = Label(menuPanel.transform,
            "<b>P1 DUCK</b>   WASD move · Space jump · Left Shift sprint · hold F climb · Q / E turn camera (or mouse)\n" +
            "<b>P2 FROG</b>   Arrows move · Right Ctrl (or Numpad 0) jump · Right Shift sprint · hold / climb · , / . turn camera\n" +
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

    // white upward-pointing triangle, tinted per player at runtime
    static Sprite MakeArrowSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float halfWidth = (size - 1 - y) * 0.5f; // wide at the bottom, point at the top
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - (size - 1) * 0.5f);
                // 1px soft edge for anti-aliasing
                byte a = (byte)(Mathf.Clamp01(halfWidth - dx + 0.5f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
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

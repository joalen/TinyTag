using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs the game: Menu -> Countdown -> Playing -> RoundOver.
/// One player starts as "it" (random). Touching the other player passes "it" to them.
/// When the timer runs out, whoever is NOT "it" wins.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum State { Menu, Countdown, Playing, RoundOver }

    public static GameManager Instance { get; private set; }

    [Header("Scene references")]
    public PlayerTagger[] players;
    public Transform[] spawnPoints;
    public ThirdPersonCamera[] playerCameras;

    [Header("Rules")]
    public float roundLength = 90f;
    public float countdownLength = 3f;
    [Tooltip("Seconds after a tag before another tag can happen (no instant tag-backs)")]
    public float tagCooldown = 3f;
    [Tooltip("How long the newly tagged player can't move")]
    public float tagFreeze = 1.5f;
    [Tooltip("Speed bonus for whoever is it, so they can catch up")]
    public float itSpeedMultiplier = 1.1f;
    [Tooltip("Players below this height respawn (fell out of the room)")]
    public float killHeight = -20f;

    public State CurrentState { get; private set; }
    public float TimeLeft { get; private set; }
    public float CountdownLeft { get; private set; }
    public int ItIndex { get; private set; }
    public int WinnerIndex { get; private set; } = -1;

    float lastTagTime = -999f;
    int lastCountdownBeep;

    void Awake() => Instance = this;

    void Start()
    {
        // each player moves relative to their own camera
        for (int i = 0; i < players.Length && i < playerCameras.Length; i++)
            if (playerCameras[i] != null) players[i].Movement.mainCamera = playerCameras[i].transform;
        GoToMenu();
    }

    void Update()
    {
        switch (CurrentState)
        {
            case State.Countdown:
                CountdownLeft -= Time.deltaTime;
                int whole = Mathf.CeilToInt(CountdownLeft);
                if (whole != lastCountdownBeep && whole > 0)
                {
                    lastCountdownBeep = whole;
                    Sfx.Instance?.PlayBeep();
                }
                if (CountdownLeft <= 0f)
                {
                    CurrentState = State.Playing;
                    Sfx.Instance?.PlayGo();
                }
                break;

            case State.Playing:
                TimeLeft -= Time.deltaTime;
                if (TimeLeft <= 0f) EndRound();
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) GoToMenu();
                break;
        }

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            p.Movement.controlsEnabled = CurrentState == State.Playing && !p.IsFrozen;
            p.Movement.speedMultiplier = p.IsIt ? itSpeedMultiplier : 1f;
            if (p.transform.position.y < killHeight) Respawn(i);
        }
    }

    public void GoToMenu()
    {
        CurrentState = State.Menu;
        ResetPlayers();
        SetCursorLocked(false);
    }

    public void StartRound()
    {
        ResetPlayers();
        ItIndex = Random.Range(0, players.Length);
        ApplyIt();
        TimeLeft = roundLength;
        CountdownLeft = countdownLength;
        lastCountdownBeep = 0;
        lastTagTime = -999f;
        WinnerIndex = -1;
        CurrentState = State.Countdown;
        SetCursorLocked(true);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Called by the "it" player's PlayerTagger when it touches someone.</summary>
    public void TryTag(PlayerTagger tagger, PlayerTagger target)
    {
        if (CurrentState != State.Playing) return;
        if (tagger.playerIndex != ItIndex || target.playerIndex == ItIndex) return;
        if (Time.time - lastTagTime < tagCooldown) return;

        lastTagTime = Time.time;
        ItIndex = target.playerIndex;
        ApplyIt();
        target.Freeze(tagFreeze);
        Sfx.Instance?.PlayTag();
    }

    /// <summary>Seconds until the current "it" is allowed to tag again (0 = can tag).</summary>
    public float TagCooldownLeft => Mathf.Max(0f, tagCooldown - (Time.time - lastTagTime));

    void EndRound()
    {
        TimeLeft = 0f;
        CurrentState = State.RoundOver;
        // with two players the winner is simply whoever isn't it
        WinnerIndex = (ItIndex + 1) % players.Length;
        Sfx.Instance?.PlayWin();
        SetCursorLocked(false);
    }

    void ApplyIt()
    {
        foreach (var p in players) p.SetIt(p.playerIndex == ItIndex);
    }

    void ResetPlayers()
    {
        for (int i = 0; i < players.Length; i++)
        {
            Respawn(i);
            players[i].Unfreeze();
            players[i].SetIt(false);
        }
        foreach (var cam in playerCameras) if (cam != null) cam.SnapBehind();
    }

    void Respawn(int i)
    {
        if (spawnPoints == null || i >= spawnPoints.Length) return;
        players[i].Movement.ResetTo(spawnPoints[i].position, spawnPoints[i].rotation);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}

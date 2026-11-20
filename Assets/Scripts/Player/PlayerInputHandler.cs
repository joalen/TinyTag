using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads input for one local player. Each player gets its own copy of the
/// action asset, filtered to its own half of the keyboard and its own gamepad,
/// so two players can share one keyboard without controlling each other.
///
/// Player 0: WASD, Space jump, Left Shift sprint, E climb, mouse look, gamepad #1
/// Player 1: Arrows, Right Ctrl / Numpad 0 jump, Right Shift sprint, / climb, gamepad #2
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    const string DefaultAssetName = "TinyTagControls"; // Assets/Resources/TinyTagControls.inputactions

    [Tooltip("0 = player one, 1 = player two")]
    [Range(0, 1)] public int playerIndex;

    [Tooltip("Optional; falls back to Resources/TinyTagControls")]
    public InputActionAsset actionsAsset;

    InputActionAsset actions;
    InputAction move, look, jump, sprint, climb;

    public Vector2 Move => move.ReadValue<Vector2>();
    public Vector2 Look => look.ReadValue<Vector2>();
    public bool JumpPressed => jump.WasPressedThisFrame();
    public bool SprintHeld => sprint.IsPressed();
    public bool ClimbHeld => climb.IsPressed();

    /// <summary>True when the latest look input came from the mouse (pixels per frame, not a stick value).</summary>
    public bool LookIsMouse => look.activeControl != null && look.activeControl.device is Mouse;

    void Awake()
    {
        if (actionsAsset == null) actionsAsset = Resources.Load<InputActionAsset>(DefaultAssetName);
        if (actionsAsset == null)
        {
            Debug.LogError($"{name}: no input actions asset found (expected Resources/{DefaultAssetName}).", this);
            enabled = false;
            return;
        }

        // private copy so each player can filter devices/bindings independently
        actions = Instantiate(actionsAsset);
        string keyboardGroup = playerIndex == 0 ? "KeyboardP1" : "KeyboardP2";
        actions.bindingMask = InputBinding.MaskByGroups(keyboardGroup, "Gamepad");

        var map = actions.FindActionMap("Player", true);
        move = map.FindAction("Move", true);
        look = map.FindAction("Look", true);
        jump = map.FindAction("Jump", true);
        sprint = map.FindAction("Sprint", true);
        climb = map.FindAction("Climb", true);

        AssignDevices();
    }

    void OnEnable()
    {
        if (actions == null) return;
        InputSystem.onDeviceChange += OnDeviceChange;
        actions.Enable();
    }

    void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        actions?.Disable();
    }

    void OnDestroy()
    {
        if (actions != null) Destroy(actions);
    }

    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed ||
            change == InputDeviceChange.Reconnected || change == InputDeviceChange.Disconnected)
            AssignDevices();
    }

    // Keyboard is shared (bindings masks split it); mouse belongs to player one;
    // gamepads are handed out in connection order.
    void AssignDevices()
    {
        var devices = new List<InputDevice>();
        if (Keyboard.current != null) devices.Add(Keyboard.current);
        if (playerIndex == 0 && Mouse.current != null) devices.Add(Mouse.current);
        if (Gamepad.all.Count > playerIndex) devices.Add(Gamepad.all[playerIndex]);
        actions.devices = devices.ToArray();
    }
}

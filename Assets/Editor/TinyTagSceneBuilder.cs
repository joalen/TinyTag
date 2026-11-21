using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the whole Bedroom scene (room, furniture, lighting, players, cameras, game manager).
/// Menu: TinyTag > Build Bedroom Scene. Safe to re-run; it overwrites Assets/Scenes/Bedroom.unity.
///
/// Scale: ~15 units = 1 real metre. The player capsule is 2 units tall (~13 cm toy figure).
/// Player can jump ~2.5 units and climb ~15 units, so every step below is &lt;= 2 units
/// and every climbable face is &lt;= 13 units tall.
/// </summary>
public static class TinyTagSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Bedroom.unity";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string GenDir = "Assets/Generated";

    const int DefaultLayer = 0, IgnoreRaycastLayer = 2, GroundLayer = 8, ClimbLayer = 9, PlayerLayer = 10;

    // Player size and speed. Shrinking the player makes the room feel bigger without
    // changing jump/climb reach (those are in world units), so the furniture stays reachable.
    const float PlayerScale = 0.7f;
    const float PlayerMoveSpeed = 8f;

    static Transform envRoot;

    [MenuItem("TinyTag/Build Bedroom Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureLayer(GroundLayer, "Ground");
        EnsureLayer(ClimbLayer, "Climbable");
        EnsureLayer(PlayerLayer, "Player");
        Directory.CreateDirectory(GenDir + "/Materials");
        Directory.CreateDirectory(GenDir + "/Textures");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        envRoot = new GameObject("Environment").transform;
        BuildRoom();
        BuildBed();
        BuildDesk();
        BuildDresserAndToys();
        BuildLighting();

        float spawnY = PlayerScale + 0.05f; // capsule half-height (1 * scale) above the floor
        var spawns = new[]
        {
            Spawn("Spawn P1", new Vector3(-8f, spawnY, -16f), 30f),
            Spawn("Spawn P2", new Vector3(12f, spawnY, 22f), 210f),
        };

        var p1 = BuildPlayer(0, spawns[0], new Color(1f, 0.85f, 0.15f));  // rubber duck
        var p2 = BuildPlayer(1, spawns[1], new Color(0.3f, 0.75f, 0.3f));  // rubber frog
        var c1 = BuildCamera(0, p1, new Rect(0f, 0f, 0.5f, 1f));
        var c2 = BuildCamera(1, p2, new Rect(0.5f, 0f, 0.5f, 1f));

        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        gm.players = new[] { p1.GetComponent<PlayerTagger>(), p2.GetComponent<PlayerTagger>() };
        gm.spawnPoints = spawns;
        gm.playerCameras = new[] { c1, c2 };
        gmGo.AddComponent<Sfx>();
        gmGo.AddComponent<GameUI>();

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("TinyTag: Bedroom scene built at " + ScenePath);
    }

    // ------------------------------------------------------------------ room

    static void BuildRoom()
    {
        var floor = Mat("Floor", Color.white, PlankTexture(), new Vector2(8, 8), 0.35f);
        var wall = Mat("Wall", new Color(0.93f, 0.89f, 0.8f), StripeTexture(), new Vector2(10, 1), 0.1f);
        var rug = Mat("Rug", Color.white, RugTexture(), Vector2.one, 0.05f);
        var door = Mat("Door", new Color(0.55f, 0.36f, 0.2f));
        var glass = Mat("WindowGlass", new Color(0.7f, 0.85f, 1f), emission: new Color(0.5f, 0.65f, 0.8f));
        var curtain = Mat("Curtain", new Color(0.75f, 0.25f, 0.3f));
        var trim = Mat("Trim", new Color(0.97f, 0.97f, 0.95f));

        Box("Floor", new Vector3(-32, -1, -32), new Vector3(32, 0, 32), floor, GroundLayer);
        Box("Wall N", new Vector3(-32, 0, 30), new Vector3(32, 30, 32), wall, DefaultLayer);
        Box("Wall S", new Vector3(-32, 0, -32), new Vector3(32, 30, -30), wall, DefaultLayer);
        Box("Wall E", new Vector3(30, 0, -30), new Vector3(32, 30, 30), wall, DefaultLayer);
        Box("Wall W", new Vector3(-32, 0, -30), new Vector3(-30, 30, 30), wall, DefaultLayer);

        // decorative only (no colliders)
        Box("Baseboard N", new Vector3(-30, 0, 29.6f), new Vector3(30, 1.2f, 30), trim, collider: false);
        Box("Baseboard S", new Vector3(-30, 0, -30), new Vector3(30, 1.2f, -29.6f), trim, collider: false);
        Box("Baseboard E", new Vector3(29.6f, 0, -30), new Vector3(30, 1.2f, 30), trim, collider: false);
        Box("Baseboard W", new Vector3(-30, 0, -30), new Vector3(-29.6f, 1.2f, 30), trim, collider: false);
        Box("Window", new Vector3(0, 10, 29.8f), new Vector3(16, 24, 30), glass, collider: false);
        Box("Window Frame H", new Vector3(0, 16.8f, 29.7f), new Vector3(16, 17.2f, 29.8f), trim, collider: false);
        Box("Window Frame V", new Vector3(7.8f, 10, 29.7f), new Vector3(8.2f, 24, 29.8f), trim, collider: false);
        Box("Curtain L", new Vector3(-3.5f, 7, 29.2f), new Vector3(-0.5f, 26, 29.7f), curtain, collider: false);
        Box("Curtain R", new Vector3(16.5f, 7, 29.2f), new Vector3(19.5f, 26, 29.7f), curtain, collider: false);
        Box("Door", new Vector3(-29.9f, 0, -12), new Vector3(-29.6f, 27, 2), door, collider: false);
        Sphere("Doorknob", new Vector3(-29.3f, 13, 0), 0.8f, Mat("Brass", new Color(0.85f, 0.7f, 0.3f), smoothness: 0.8f), collider: false);

        Box("Rug", new Vector3(-14, 0, -14), new Vector3(14, 0.15f, 10), rug, GroundLayer);
    }

    static void BuildBed()
    {
        var wood = Mat("WoodDark", new Color(0.45f, 0.28f, 0.16f));
        var sheet = Mat("Sheet", new Color(0.96f, 0.96f, 0.98f));
        var blanket = Mat("Blanket", new Color(0.3f, 0.45f, 0.8f));

        // legs leave a 2.6-unit gap: players (2 tall) can hide under the bed
        foreach (var (x, z) in new[] { (-29.6f, 4.4f), (-13.2f, 4.4f), (-29.6f, 26.8f), (-13.2f, 26.8f) })
            Box("Bed Leg", new Vector3(x, 0, z), new Vector3(x + 1.2f, 2.6f, z + 1.2f), wood, ClimbLayer);

        Box("Bed Frame", new Vector3(-30, 2.6f, 4), new Vector3(-12, 4.6f, 28), wood, ClimbLayer);
        Box("Mattress", new Vector3(-29.5f, 4.6f, 4.5f), new Vector3(-12.5f, 6.4f, 28), sheet, ClimbLayer);
        Box("Blanket", new Vector3(-29.7f, 6.4f, 4.3f), new Vector3(-12.3f, 6.7f, 20), blanket, ClimbLayer);
        Box("Pillow", new Vector3(-27, 6.4f, 22), new Vector3(-15, 7.6f, 27), sheet, ClimbLayer);
        Box("Headboard", new Vector3(-30, 0, 28), new Vector3(-12, 13, 30), wood, ClimbLayer);

        // toy-block staircase up to the bed (steps of 1.7)
        Box("Block Step 1", new Vector3(-6, 0, 8), new Vector3(-2, 1.7f, 12), Mat("BlockRed", new Color(0.9f, 0.25f, 0.2f)), ClimbLayer);
        Box("Block Step 2", new Vector3(-9, 0, 8), new Vector3(-6, 3.4f, 12), Mat("BlockYellow", new Color(1f, 0.8f, 0.2f)), ClimbLayer);
        Box("Block Step 3", new Vector3(-12, 0, 8), new Vector3(-9, 5.1f, 12), Mat("BlockBlue", new Color(0.25f, 0.5f, 0.95f)), ClimbLayer);
    }

    static void BuildDesk()
    {
        var wood = Mat("WoodLight", new Color(0.78f, 0.6f, 0.4f));
        var metal = Mat("ChairMetal", new Color(0.3f, 0.3f, 0.35f), smoothness: 0.6f);
        var seat = Mat("ChairSeat", new Color(0.2f, 0.55f, 0.55f));

        // desk against the east wall; solid side panels make a tunnel underneath
        Box("Desk Top", new Vector3(20, 10, 4), new Vector3(30, 11, 22), wood, ClimbLayer);
        Box("Desk Side A", new Vector3(20.5f, 0, 4), new Vector3(30, 10, 5), wood, ClimbLayer);
        Box("Desk Side B", new Vector3(20.5f, 0, 21), new Vector3(30, 10, 22), wood, ClimbLayer);

        // desk lamp (light added in BuildLighting)
        var lampMat = Mat("Lamp", new Color(0.95f, 0.85f, 0.5f), emission: new Color(0.6f, 0.5f, 0.2f));
        Box("Lamp Base", new Vector3(25, 11, 16), new Vector3(27, 11.4f, 18), metal, ClimbLayer);
        Box("Lamp Pole", new Vector3(25.8f, 11.4f, 16.8f), new Vector3(26.2f, 16, 17.2f), metal, collider: false);
        Box("Lamp Shade", new Vector3(24.5f, 16, 15.5f), new Vector3(27.5f, 18.5f, 18.5f), lampMat, collider: false);
        Box("Desk Books", new Vector3(22, 11, 6), new Vector3(26, 12.4f, 11), Mat("BookGreen", new Color(0.2f, 0.55f, 0.3f)), ClimbLayer);

        // chair facing the desk, with a pile of books on it as steps up to the desk top
        Box("Chair Seat", new Vector3(14, 5, 10), new Vector3(19, 5.8f, 16), seat, ClimbLayer);
        foreach (var (x, z) in new[] { (14.2f, 10.2f), (18.2f, 10.2f), (14.2f, 15.2f), (18.2f, 15.2f) })
            Box("Chair Leg", new Vector3(x, 0, z), new Vector3(x + 0.6f, 5, z + 0.6f), metal, ClimbLayer);
        Box("Chair Back", new Vector3(13.2f, 5, 10), new Vector3(14, 13, 16), seat, ClimbLayer);

        var bookRed = Mat("BookRed", new Color(0.75f, 0.2f, 0.2f));
        var bookBlue = Mat("BookBlue", new Color(0.2f, 0.3f, 0.7f));
        // floor -> chair seat
        Box("Floor Book 1", new Vector3(9, 0, 11), new Vector3(13.2f, 1.9f, 15), bookRed, ClimbLayer);
        Box("Floor Book 2", new Vector3(11, 1.9f, 11.5f), new Vector3(13.2f, 3.8f, 14.5f), bookBlue, ClimbLayer);
        // chair seat -> desk top
        Box("Chair Book 1", new Vector3(15.5f, 5.8f, 10.5f), new Vector3(19, 7.5f, 15.5f), bookBlue, ClimbLayer);
        Box("Chair Book 2", new Vector3(17, 7.5f, 11), new Vector3(19, 9.2f, 15), bookRed, ClimbLayer);
    }

    static void BuildDresserAndToys()
    {
        var dresser = Mat("Dresser", new Color(0.85f, 0.75f, 0.6f));
        var drawer = Mat("Drawer", new Color(0.78f, 0.66f, 0.5f));
        var knob = Mat("Knob", new Color(0.3f, 0.2f, 0.1f));

        Box("Dresser", new Vector3(-26, 0, -30), new Vector3(-10, 12, -22), dresser, ClimbLayer);
        for (int i = 0; i < 3; i++)
        {
            float y = 0.8f + i * 3.7f;
            Box("Drawer", new Vector3(-25, y, -22.1f), new Vector3(-11, y + 3.2f, -21.9f), drawer, collider: false);
            Sphere("Knob", new Vector3(-18, y + 1.6f, -21.7f), 0.6f, knob, collider: false);
        }
        // potted plant on the dresser
        Box("Plant Pot", new Vector3(-14, 12, -28), new Vector3(-11, 15, -25), Mat("Pot", new Color(0.75f, 0.4f, 0.25f)), ClimbLayer);
        Sphere("Plant Leaves", new Vector3(-12.5f, 17.5f, -26.5f), 5f, Mat("Leaves", new Color(0.25f, 0.6f, 0.25f)), collider: false);

        // toy box against the south wall
        Box("Toy Box", new Vector3(10, 0, -30), new Vector3(24, 8, -22), Mat("ToyBox", new Color(0.85f, 0.3f, 0.35f)), ClimbLayer);
        Box("Toy Box Lid", new Vector3(9.7f, 8, -30), new Vector3(24.3f, 8.8f, -21.7f), Mat("ToyBoxLid", new Color(0.95f, 0.8f, 0.3f)), ClimbLayer);

        // block tower in the middle of the rug (steps of 2)
        Box("Tower Block 1", new Vector3(-4, 0.15f, -4), new Vector3(0, 2.1f, 0), Mat("BlockYellow", new Color(1f, 0.8f, 0.2f)), ClimbLayer);
        Box("Tower Block 2", new Vector3(0, 0.15f, -4), new Vector3(4, 4.1f, 0), Mat("BlockBlue", new Color(0.25f, 0.5f, 0.95f)), ClimbLayer);
        Box("Tower Block 3", new Vector3(2, 4.1f, -4), new Vector3(4, 6.1f, 0), Mat("BlockRed", new Color(0.9f, 0.25f, 0.2f)), ClimbLayer);

        // big ball and a toy car to dodge around
        Sphere("Ball", new Vector3(10, 4, -8), 8f, Mat("Ball", new Color(0.95f, 0.55f, 0.15f), smoothness: 0.5f), ClimbLayer);
        var car = Mat("ToyCar", new Color(0.3f, 0.75f, 0.4f), smoothness: 0.6f);
        var tire = Mat("Tire", new Color(0.12f, 0.12f, 0.12f));
        Box("Toy Car Body", new Vector3(-22, 1, -10), new Vector3(-12, 3.5f, -4), car, ClimbLayer);
        Box("Toy Car Cabin", new Vector3(-19, 3.5f, -9.5f), new Vector3(-14, 5.5f, -4.5f), car, ClimbLayer);
        foreach (var (x, z) in new[] { (-20f, -10.2f), (-14f, -10.2f), (-20f, -3.8f), (-14f, -3.8f) })
        {
            var w = Cylinder("Wheel", new Vector3(x, 1.2f, z), new Vector3(2.4f, 0.3f, 2.4f), tire);
            w.transform.rotation = Quaternion.Euler(90, 0, 0);
        }
    }

    static void BuildLighting()
    {
        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.95f, 0.85f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sunGo.transform.rotation = Quaternion.Euler(58f, 200f, 0f); // streaming in from the window side

        var lampGo = new GameObject("Desk Lamp Light");
        lampGo.transform.position = new Vector3(26f, 15.5f, 17f);
        var lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.8f, 0.5f);
        lamp.range = 40f;
        lamp.intensity = 4f;
        lamp.shadows = LightShadows.None;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.6f, 0.65f, 0.75f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.47f, 0.43f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.26f, 0.22f);
        RenderSettings.skybox = null;
    }

    // --------------------------------------------------------------- players

    static Transform Spawn(string name, Vector3 pos, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        return t;
    }

    static GameObject BuildPlayer(int index, Transform spawn, Color color)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = $"Player {index + 1}";
        go.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        go.transform.localScale = Vector3.one * PlayerScale;
        go.layer = PlayerLayer;
        go.GetComponent<PlayerMovement>().moveSpeed = PlayerMoveSpeed;

        // the capsule stays as the collider, but is hidden: the toy model below is what you see
        go.GetComponent<MeshRenderer>().enabled = false;
        go.GetComponent<CapsuleCollider>().sharedMaterial = NoFriction();

        // model pivot sits at the feet so squash/stretch keeps it on the ground
        var model = new GameObject(index == 0 ? "Duck Model" : "Frog Model").transform;
        model.SetParent(go.transform, false);
        model.localPosition = new Vector3(0f, -1f, 0f);
        var waddle = model.gameObject.AddComponent<ToyWaddle>();
        if (index == 0) BuildDuck(model, color);
        else { BuildFrog(model, color); waddle.style = ToyWaddle.Style.Hop; }

        var input = go.GetComponent<PlayerInputHandler>();
        if (input == null) input = go.AddComponent<PlayerInputHandler>();
        input.playerIndex = index;

        var tagger = go.AddComponent<PlayerTagger>();
        tagger.playerIndex = index;
        tagger.displayName = index == 0 ? "DUCK" : "FROG";
        tagger.normalColor = color;
        tagger.markerMaterial = Mat("ItMarker", tagger.itColor, emission: tagger.itColor * 0.5f);
        return go;
    }

    // Toy models are made of squashed spheres. Positions are in the capsule's local space
    // (capsule spans y = -1..1, radius 0.5, facing +z); Part() shifts them onto the feet pivot.

    static void BuildDuck(Transform model, Color yellow)
    {
        var body = Mat("DuckYellow", yellow, smoothness: 0.75f); // shiny rubber
        var beak = Mat("DuckBeak", new Color(1f, 0.5f, 0.1f), smoothness: 0.75f);
        var eye = Mat("ToyEyeBlack", new Color(0.05f, 0.05f, 0.05f), smoothness: 0.9f);

        Part(model, "Body", new Vector3(0, -0.45f, -0.05f), new Vector3(1.05f, 0.85f, 1.25f), body);
        Part(model, "Tail", new Vector3(0, -0.18f, -0.62f), new Vector3(0.4f, 0.35f, 0.35f), body);
        Part(model, "Wing L", new Vector3(-0.5f, -0.4f, -0.05f), new Vector3(0.18f, 0.45f, 0.7f), body);
        Part(model, "Wing R", new Vector3(0.5f, -0.4f, -0.05f), new Vector3(0.18f, 0.45f, 0.7f), body);
        Part(model, "Head", new Vector3(0, 0.42f, 0.2f), new Vector3(0.75f, 0.72f, 0.72f), body);
        Part(model, "Beak", new Vector3(0, 0.32f, 0.6f), new Vector3(0.42f, 0.14f, 0.36f), beak);
        Part(model, "Eye L", new Vector3(-0.17f, 0.55f, 0.5f), Vector3.one * 0.13f, eye);
        Part(model, "Eye R", new Vector3(0.17f, 0.55f, 0.5f), Vector3.one * 0.13f, eye);
    }

    static void BuildFrog(Transform model, Color green)
    {
        var skin = Mat("FrogGreen", green, smoothness: 0.75f);
        var belly = Mat("FrogBelly", new Color(0.85f, 0.9f, 0.55f), smoothness: 0.6f);
        var eyeWhite = Mat("ToyEyeWhite", Color.white, smoothness: 0.9f);
        var eye = Mat("ToyEyeBlack", new Color(0.05f, 0.05f, 0.05f), smoothness: 0.9f);
        var mouth = Mat("FrogMouth", new Color(0.35f, 0.05f, 0.08f));

        Part(model, "Body", new Vector3(0, -0.5f, -0.05f), new Vector3(1.15f, 0.95f, 1.2f), skin);
        Part(model, "Belly", new Vector3(0, -0.55f, 0.3f), new Vector3(0.85f, 0.75f, 0.6f), belly);
        Part(model, "Head", new Vector3(0, 0.08f, 0.18f), new Vector3(1.0f, 0.7f, 0.85f), skin);
        Part(model, "Mouth", new Vector3(0, 0.0f, 0.58f), new Vector3(0.55f, 0.04f, 0.06f), mouth, PrimitiveType.Cube);
        foreach (float x in new[] { -0.26f, 0.26f })
        {
            Part(model, "Eye", new Vector3(x, 0.42f, 0.28f), Vector3.one * 0.38f, eyeWhite);
            Part(model, "Pupil", new Vector3(x, 0.46f, 0.44f), Vector3.one * 0.17f, eye);
        }
        foreach (float x in new[] { -0.55f, 0.55f })
            Part(model, "Back Leg", new Vector3(x, -0.82f, -0.2f), new Vector3(0.38f, 0.28f, 0.75f), skin);
        foreach (float x in new[] { -0.32f, 0.32f })
            Part(model, "Front Foot", new Vector3(x, -0.92f, 0.45f), new Vector3(0.25f, 0.12f, 0.3f), skin);
    }

    static void Part(Transform model, string name, Vector3 capsulePos, Vector3 scale, Material mat,
                     PrimitiveType shape = PrimitiveType.Sphere)
    {
        var go = GameObject.CreatePrimitive(shape);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.layer = PlayerLayer;
        go.transform.SetParent(model, false);
        go.transform.localPosition = capsulePos + Vector3.up; // model pivot is at the feet (y = -1)
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static ThirdPersonCamera BuildCamera(int index, GameObject player, Rect viewport)
    {
        var go = new GameObject($"Camera P{index + 1}");
        if (index == 0) go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.rect = viewport;
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 300f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.78f, 0.95f);
        if (index == 0) go.AddComponent<AudioListener>();

        var follow = go.AddComponent<ThirdPersonCamera>();
        follow.target = player.transform;
        follow.input = player.GetComponent<PlayerInputHandler>();
        follow.collisionMask = ~((1 << PlayerLayer) | (1 << IgnoreRaycastLayer));
        follow.distance = 4.5f;
        follow.pivotHeight = 0.9f;
        follow.collisionRadius = 0.2f;

        var behind = player.transform.position - player.transform.forward * follow.distance + Vector3.up * 2.5f;
        go.transform.SetPositionAndRotation(behind, Quaternion.LookRotation(player.transform.position - behind));

        player.GetComponent<PlayerMovement>().mainCamera = go.transform;
        return follow;
    }

    // --------------------------------------------------------------- helpers

    static GameObject Box(string name, Vector3 min, Vector3 max, Material mat, int layer = DefaultLayer, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Setup(go, name, (min + max) * 0.5f, max - min, mat, layer, collider);
        return go;
    }

    static GameObject Sphere(string name, Vector3 center, float diameter, Material mat, int layer = DefaultLayer, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Setup(go, name, center, Vector3.one * diameter, mat, layer, collider);
        return go;
    }

    static GameObject Cylinder(string name, Vector3 center, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Setup(go, name, center, scale, mat, DefaultLayer, false);
        return go;
    }

    static void Setup(GameObject go, string name, Vector3 pos, Vector3 scale, Material mat, int layer, bool collider)
    {
        go.name = name;
        go.transform.SetParent(envRoot, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.layer = layer;
        go.isStatic = true;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    static Material Mat(string name, Color color, Texture2D tex = null, Vector2? tiling = null,
                        float smoothness = 0.2f, Color? emission = null)
    {
        string path = $"{GenDir}/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = mat == null;
        if (isNew) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        if (isNew) AssetDatabase.CreateAsset(mat, path);
        else EditorUtility.SetDirty(mat);
        return mat;
    }

    static PhysicsMaterial NoFriction()
    {
        string path = $"{GenDir}/PlayerNoFriction.asset";
        var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (pm != null) return pm;
        pm = new PhysicsMaterial("PlayerNoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum,
        };
        AssetDatabase.CreateAsset(pm, path);
        return pm;
    }

    static void EnsureLayer(int index, string name)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(index);
        if (layer.stringValue == name) return;
        if (!string.IsNullOrEmpty(layer.stringValue))
            Debug.LogWarning($"TinyTag: layer {index} was '{layer.stringValue}', renaming to '{name}'");
        layer.stringValue = name;
        tagManager.ApplyModifiedProperties();
    }

    // ------------------------------------------------------- procedural textures

    static Texture2D PlankTexture()
    {
        const int size = 256, planks = 4;
        var rng = new System.Random(7);
        var tex = new Texture2D(size, size);
        var px = new Color[size * size];
        int plankW = size / planks;
        for (int p = 0; p < planks; p++)
        {
            Color baseCol = Color.Lerp(new Color(0.62f, 0.42f, 0.25f), new Color(0.76f, 0.55f, 0.34f), (float)rng.NextDouble());
            int endSeam = rng.Next(size);
            for (int x = p * plankW; x < (p + 1) * plankW; x++)
                for (int y = 0; y < size; y++)
                {
                    float grain = Mathf.PerlinNoise(x * 0.9f + p * 13f, y * 0.04f) * 0.18f;
                    Color c = baseCol * (0.9f + grain);
                    bool seam = x % plankW < 2 || Mathf.Abs(y - endSeam) < 2;
                    px[y * size + x] = seam ? baseCol * 0.55f : c;
                }
        }
        return SaveTexture("FloorPlanks", tex, px);
    }

    static Texture2D StripeTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size);
        var px = new Color[size * size];
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                px[y * size + x] = x < size / 2 ? Color.white : new Color(0.94f, 0.94f, 0.94f);
        return SaveTexture("WallStripes", tex, px);
    }

    static Texture2D RugTexture()
    {
        const int size = 256;
        var tex = new Texture2D(size, size);
        var px = new Color[size * size];
        Color[] bands = { new Color(0.7f, 0.2f, 0.25f), new Color(0.95f, 0.9f, 0.8f), new Color(0.25f, 0.35f, 0.6f), new Color(0.95f, 0.9f, 0.8f) };
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                px[y * size + x] = edge < 64 ? bands[(edge / 12) % bands.Length] : new Color(0.7f, 0.2f, 0.25f);
            }
        return SaveTexture("RugPattern", tex, px);
    }

    static Texture2D SaveTexture(string name, Texture2D tex, Color[] px)
    {
        tex.SetPixels(px);
        tex.Apply();
        string path = $"{GenDir}/Textures/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}

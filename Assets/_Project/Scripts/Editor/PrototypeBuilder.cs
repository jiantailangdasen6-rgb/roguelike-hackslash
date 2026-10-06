using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Core;
using Game.Gameplay;
using Game.UI;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Generates the survivors-style prototype from code: placeholder art, input actions, the networked
    /// player prefab, the NetworkManager prefab, and the Lobby/Game scenes.
    /// Re-running regenerates everything except the input actions asset (kept if it exists).
    ///
    /// Batch mode:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Game.EditorTools.PrototypeBuilder.Build
    /// </summary>
    public static class PrototypeBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ArtDir = Root + "/Art/Placeholder";
        private const string TilesDir = Root + "/Art/Tiles";
        private const string ResourcesDir = Root + "/Resources";
        private const string SettingsDir = Root + "/Settings";
        private const string ScenesDir = Root + "/Scenes";

        private const string InputActionsPath = SettingsDir + "/GameControls.inputactions";
        private const string PrefabsListPath = SettingsDir + "/NetworkPrefabs.asset";
        private const string GameArtPath = ResourcesDir + "/GameArt.asset";
        private const string PlayerPrefabPath = ResourcesDir + "/NetPlayer.prefab";
        private const string NetworkManagerPath = ResourcesDir + "/NetworkManager.prefab";
        private const string LobbyScenePath = ScenesDir + "/Lobby.unity";
        private const string GameScenePath = ScenesDir + "/Game.unity";

        private const string KeyboardMouse = "Keyboard&Mouse";
        private const string GamepadScheme = "Gamepad";
        private const float CameraOrthoSize = 7f;

        private sealed class Art
        {
            public Sprite Circle, Triangle, Diamond, ArcSprite, Ring;
            public Tile Floor, FloorAlt, Wall;
        }

        [MenuItem("Tools/Game/Rebuild Prototype")]
        public static void Build()
        {
            foreach (var dir in new[] { ArtDir, TilesDir, ResourcesDir, SettingsDir, ScenesDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            var art = CreateArt();
            var actions = LoadOrCreateInputActions();
            var player = CreatePlayerPrefab(art, actions);
            CreateNetworkManagerPrefab(player);

            BuildLobbyScene();
            BuildGameScene(art, player);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[PrototypeBuilder] Done");
        }

        // ================================================================ Art

        private static Art CreateArt()
        {
            var art = new Art
            {
                Circle = CreateSprite("Circle", 64, 64, FilterMode.Bilinear, (x, y, s) =>
                {
                    float d = Dist(x, y, s);
                    return White(Mathf.Clamp01(s / 2f - d));
                }),
                Triangle = CreateSprite("Triangle", 32, 64, FilterMode.Bilinear, (x, y, s) =>
                {
                    float halfWidth = (s - y - 0.5f) / 2f;
                    float dx = Mathf.Abs(x + 0.5f - s / 2f);
                    return White(Mathf.Clamp01(halfWidth - dx + 0.5f));
                }),
                Diamond = CreateSprite("Diamond", 32, 32, FilterMode.Bilinear, (x, y, s) =>
                {
                    float h = s / 2f;
                    float d = Mathf.Abs(x + 0.5f - h) + Mathf.Abs(y + 0.5f - h);
                    return White(Mathf.Clamp01(h - d));
                }),
                // Sector pointing up (+Y), used for the greatsword slash. Diameter = 1 unit.
                ArcSprite = CreateSprite("Arc", 128, 128, FilterMode.Bilinear, (x, y, s) =>
                {
                    float h = s / 2f;
                    float dx = x + 0.5f - h, dy = y + 0.5f - h;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / h;
                    float angle = Mathf.Abs(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);
                    if (r > 1f || angle > WeaponCatalog.SlashHalfAngle) return White(0f);
                    float edge = Mathf.Clamp01((1f - r) * h) * Mathf.Clamp01((WeaponCatalog.SlashHalfAngle - angle) * 0.5f);
                    return White(Mathf.SmoothStep(0f, 1f, r) * edge);
                }),
                Ring = CreateSprite("Ring", 128, 128, FilterMode.Bilinear, (x, y, s) =>
                {
                    float r = Dist(x, y, s) / (s / 2f);
                    return White(Mathf.Clamp01(Mathf.Min((r - 0.82f) * 40f, (1f - r) * 40f)));
                }),
            };

            var floorColor = new Color(0.17f, 0.2f, 0.17f);
            var floor = CreateSprite("FloorTile", 16, 16, FilterMode.Point, (x, y, s) =>
                x == 0 || y == 0 ? floorColor * 0.85f : floorColor);
            var floorAlt = CreateSprite("FloorTileAlt", 16, 16, FilterMode.Point, (x, y, s) =>
            {
                var c = floorColor * 1.12f;
                if ((x * 7 + y * 3) % 11 == 0) c *= 0.8f; // moss specks
                return x == 0 || y == 0 ? floorColor * 0.85f : c;
            });
            var wallColor = new Color(0.42f, 0.4f, 0.36f);
            var wall = CreateSprite("WallTile", 16, 16, FilterMode.Point, (x, y, s) =>
                y >= s - 2 ? wallColor * 1.25f : x == 0 || y == 0 ? wallColor * 0.7f : wallColor);

            art.Floor = CreateTile("Floor", floor);
            art.FloorAlt = CreateTile("FloorAlt", floorAlt);
            art.Wall = CreateTile("Wall", wall);

            var gameArt = LoadOrCreate<GameArt>(GameArtPath);
            gameArt.Circle = art.Circle;
            gameArt.Triangle = art.Triangle;
            gameArt.Diamond = art.Diamond;
            gameArt.Arc = art.ArcSprite;
            gameArt.Ring = art.Ring;
            EditorUtility.SetDirty(gameArt);
            return art;
        }

        private static float Dist(int x, int y, int s) =>
            Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f));

        private static Color White(float alpha) => new(1f, 1f, 1f, alpha);

        private static Sprite CreateSprite(string name, int size, int pixelsPerUnit, FilterMode filter, Func<int, int, int, Color> pixel)
        {
            string path = $"{ArtDir}/{name}.png";
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = pixel(x, y, size);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = filter;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Tile CreateTile(string name, Sprite sprite)
        {
            var tile = LoadOrCreate<Tile>($"{TilesDir}/{name}.asset");
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ================================================================ Input

        private static InputActionAsset LoadOrCreateInputActions()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "GameControls";
            asset.AddControlScheme(KeyboardMouse).WithRequiredDevice("<Keyboard>").WithOptionalDevice("<Mouse>");
            // The on-screen touch controls emulate a gamepad, so touch uses the Gamepad bindings.
            asset.AddControlScheme(GamepadScheme).WithRequiredDevice("<Gamepad>");

            var map = asset.AddActionMap(PlayerInputReader.MapName);
            var move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardMouse)
                .With("Down", "<Keyboard>/s", KeyboardMouse)
                .With("Left", "<Keyboard>/a", KeyboardMouse)
                .With("Right", "<Keyboard>/d", KeyboardMouse);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", KeyboardMouse)
                .With("Down", "<Keyboard>/downArrow", KeyboardMouse)
                .With("Left", "<Keyboard>/leftArrow", KeyboardMouse)
                .With("Right", "<Keyboard>/rightArrow", KeyboardMouse);
            move.AddBinding("<Gamepad>/leftStick", groups: GamepadScheme);

            var dash = map.AddAction("Dash", InputActionType.Button);
            dash.AddBinding("<Keyboard>/space", groups: KeyboardMouse);
            dash.AddBinding("<Gamepad>/buttonSouth", groups: GamepadScheme);

            var inventory = map.AddAction("Inventory", InputActionType.Button);
            inventory.AddBinding("<Keyboard>/i", groups: KeyboardMouse);
            inventory.AddBinding("<Keyboard>/tab", groups: KeyboardMouse);
            inventory.AddBinding("<Gamepad>/select", groups: GamepadScheme);

            File.WriteAllText(InputActionsPath, asset.ToJson());
            Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        }

        // ================================================================ Prefabs

        private static NetworkObject CreatePlayerPrefab(Art art, InputActionAsset actions)
        {
            var root = new GameObject("NetPlayer");
            root.AddComponent<NetworkObject>();

            var transform = root.AddComponent<NetworkTransform>();
            transform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            transform.SyncPositionZ = false;
            transform.SyncRotAngleX = transform.SyncRotAngleY = transform.SyncRotAngleZ = false;
            transform.SyncScaleX = transform.SyncScaleY = transform.SyncScaleZ = false;
            transform.Interpolate = true;

            var body = Child(root, "Body", art.Circle, 10, 0.8f);
            var aura = Child(root, "Aura", art.Circle, 1, 1f);

            var input = root.AddComponent<PlayerInputReader>();
            input.enabled = false;
            SetRef(input, "actions", actions);

            var mover = root.AddComponent<PlayerMover>();
            mover.enabled = false;
            SetRef(mover, "input", input);

            var netPlayer = root.AddComponent<NetPlayer>();
            SetRef(netPlayer, "body", body);
            SetRef(netPlayer, "aura", aura);
            SetRef(netPlayer, "mover", mover);
            SetRef(netPlayer, "input", input);
            SetRef(mover, "player", netPlayer);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            var networkObject = prefab.GetComponent<NetworkObject>();
            EnsureGlobalObjectIdHash(networkObject);
            PrefabUtility.SavePrefabAsset(prefab);
            return networkObject;
        }

        private static SpriteRenderer Child(GameObject parent, string name, Sprite sprite, int order, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private static void CreateNetworkManagerPrefab(NetworkObject player)
        {
            var list = LoadOrCreate<NetworkPrefabsList>(PrefabsListPath);
            var serialized = new SerializedObject(list);
            serialized.FindProperty("List").ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            list.Add(new NetworkPrefab { Prefab = player.gameObject });
            EditorUtility.SetDirty(list);

            var go = new GameObject("NetworkManager");
            var transport = go.AddComponent<UnityTransport>();
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = null;
            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.EnableSceneManagement = true;
            nm.NetworkConfig.TickRate = 30;
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { list };
            nm.RunInBackground = true;

            PrefabUtility.SaveAsPrefabAsset(go, NetworkManagerPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>NetworkObject only computes its id hash in OnValidate, which batch mode does not always run.</summary>
        private static void EnsureGlobalObjectIdHash(NetworkObject networkObject)
        {
            typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(networkObject, null);
            var hash = (uint)typeof(NetworkObject)
                .GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(networkObject);
            if (hash == 0) throw new InvalidOperationException($"{networkObject.name} has no GlobalObjectIdHash");
            EditorUtility.SetDirty(networkObject);
        }

        // ================================================================ Scenes

        private static void BuildLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(withBrain: false);
            CreateEventSystem();
            new GameObject("Lobby").AddComponent<LobbyView>();
            new GameObject("Inventory").AddComponent<InventoryView>();
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        private static void BuildGameScene(Art art, NetworkObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(withBrain: true);
            new GameObject("Global Light 2D").AddComponent<Light2D>().lightType = Light2D.LightType.Global;

            var bounds = CreateArena(art);
            CreateFollowCamera(bounds);

            var directorGo = new GameObject("RunDirector");
            var directorObject = directorGo.AddComponent<NetworkObject>();
            var director = directorGo.AddComponent<RunDirector>();
            SetRef(director, "playerPrefab", playerPrefab);

            new GameObject("EnemyView").AddComponent<EnemyView>();
            new GameObject("EventPresenter").AddComponent<EventPresenter>();
            new GameObject("LootSystem").AddComponent<LootSystem>();
            new GameObject("HUD").AddComponent<HudView>();
            new GameObject("LevelUp").AddComponent<LevelUpView>();
            new GameObject("Inventory").AddComponent<InventoryView>();
            new GameObject("Result").AddComponent<ResultView>();
            CreateMobileControls(AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/Circle.png"));
            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, GameScenePath);
            // In-scene NetworkObjects derive their id from the saved scene, so compute it after saving.
            EnsureGlobalObjectIdHash(directorObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void CreateCamera(bool withBrain)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraOrthoSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            if (!go.TryGetComponent<UniversalAdditionalCameraData>(out _)) go.AddComponent<UniversalAdditionalCameraData>();
            go.AddComponent<AudioListener>();
            if (withBrain) go.AddComponent<CinemachineBrain>();
        }

        private static Collider2D CreateArena(Art art)
        {
            // Earlier steps (NGO's prefab processing) can reload assets, leaving stale references; reload the tiles.
            AssetDatabase.SaveAssets();
            var floorTile = AssetDatabase.LoadAssetAtPath<Tile>($"{TilesDir}/Floor.asset");
            var floorAltTile = AssetDatabase.LoadAssetAtPath<Tile>($"{TilesDir}/FloorAlt.asset");
            var wallTile = AssetDatabase.LoadAssetAtPath<Tile>($"{TilesDir}/Wall.asset");

            var b = RunSimulation.DefaultBounds;
            var grid = new GameObject("Arena");
            grid.AddComponent<Grid>();
            var floor = CreateTilemap(grid.transform, "Floor", -10);
            var walls = CreateTilemap(grid.transform, "Walls", -9);

            var rng = new GameRandom(7);
            for (int x = (int)b.xMin; x < (int)b.xMax; x++)
            for (int y = (int)b.yMin; y < (int)b.yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                floor.SetTile(cell, rng.Chance(0.15f) ? floorAltTile : floorTile);
                bool border = x == (int)b.xMin || x == (int)b.xMax - 1 || y == (int)b.yMin || y == (int)b.yMax - 1;
                if (border) walls.SetTile(cell, wallTile);
            }
            if (floor.GetUsedTilesCount() == 0 || walls.GetUsedTilesCount() == 0)
                throw new InvalidOperationException("Arena tilemaps are empty (tile assets missing?)");

            var boundsGo = new GameObject("CameraBounds") { layer = 2 };
            boundsGo.transform.SetParent(grid.transform, false);
            var poly = boundsGo.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            poly.points = new[]
            {
                new Vector2(b.xMin, b.yMin), new Vector2(b.xMax, b.yMin),
                new Vector2(b.xMax, b.yMax), new Vector2(b.xMin, b.yMax),
            };
            return poly;
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = order;
            return tilemap;
        }

        private static void CreateFollowCamera(Collider2D bounds)
        {
            var go = new GameObject("CM Follow Camera");
            var vcam = go.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.OrthographicSize = CameraOrthoSize;
            vcam.Lens = lens;
            var composer = go.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.Damping = new Vector3(0.3f, 0.3f, 0f);
            go.AddComponent<CinemachineConfiner2D>().BoundingShape2D = bounds;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>(); // assigns default UI actions itself when enabled
        }

        private static void CreateMobileControls(Sprite circle)
        {
            var canvasGo = new GameObject("MobileControls", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<MobileControls>();

            var safeArea = UiObject("SafeArea", canvasGo.transform);
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
            safeArea.gameObject.AddComponent<SafeArea>();

            var stickBase = UiImage("MoveStick", safeArea, circle, new Color(1f, 1f, 1f, 0.15f), 300f, Vector2.zero, new Vector2(250f, 250f));
            stickBase.GetComponent<Image>().raycastTarget = false;
            var knob = UiImage("Knob", stickBase, circle, new Color(1f, 1f, 1f, 0.55f), 140f, new Vector2(0.5f, 0.5f), Vector2.zero);
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            var so = new SerializedObject(stick);
            so.FindProperty("m_ControlPath").stringValue = "<Gamepad>/leftStick";
            so.FindProperty("m_MovementRange").floatValue = 120f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var dash = UiImage("DashButton", safeArea, circle, new Color(1f, 1f, 1f, 0.3f), 190f, new Vector2(1f, 0f), new Vector2(-220f, 220f));
            var button = dash.gameObject.AddComponent<OnScreenButton>();
            var bso = new SerializedObject(button);
            bso.FindProperty("m_ControlPath").stringValue = "<Gamepad>/buttonSouth";
            bso.ApplyModifiedPropertiesWithoutUndo();
            var label = UiObject("Label", dash).gameObject.AddComponent<Text>();
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.text = "DASH";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 40;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
        }

        private static RectTransform UiImage(string name, Transform parent, Sprite sprite, Color color, float size, Vector2 anchor, Vector2 position)
        {
            var rect = UiObject(name, parent);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return rect;
        }

        private static RectTransform UiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            return (RectTransform)go.transform;
        }

        // ================================================================ Settings

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.runInBackground = true; // hosts must keep simulating when unfocused
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.productName = "LabyrinthSurvivors";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'.");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

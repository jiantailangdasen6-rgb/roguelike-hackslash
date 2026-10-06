using System;
using System.IO;
using Game.Player;
using Game.UI;
using Unity.Cinemachine;
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
    /// Builds the M0 sandbox from code: placeholder sprites, tiles, input actions, player prefab,
    /// a test room, the follow camera and the on-screen touch controls.
    /// Re-running it regenerates everything except the input actions asset (kept if it exists).
    ///
    /// Batch mode:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Game.EditorTools.M0SandboxBuilder.Build
    /// </summary>
    public static class M0SandboxBuilder
    {
        private const string Root = "Assets/_Project";
        private const string PlaceholderArtDir = Root + "/Art/Placeholder";
        private const string TilesDir = Root + "/Art/Tiles";
        private const string SettingsDir = Root + "/Settings";
        private const string PlayerPrefabDir = Root + "/Prefabs/Player";
        private const string ScenesDir = Root + "/Scenes";

        private const string InputActionsPath = SettingsDir + "/GameControls.inputactions";
        private const string PlayerPrefabPath = PlayerPrefabDir + "/Player.prefab";
        private const string ScenePath = ScenesDir + "/Dungeon.unity";
        private const string TemplateSampleScenesDir = "Assets/Scenes";

        private const string KeyboardMouse = "Keyboard&Mouse";
        private const string GamepadScheme = "Gamepad";

        private const int RoomWidth = 24;
        private const int RoomHeight = 14;
        private const float CameraOrthoSize = 6f;

        [MenuItem("Tools/Game/Rebuild M0 Sandbox")]
        public static void Build()
        {
            foreach (var dir in new[] { PlaceholderArtDir, TilesDir, SettingsDir, PlayerPrefabDir, ScenesDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            var sprites = CreatePlaceholderSprites();
            var floorTile = CreateTile("Floor", sprites.Floor, Tile.ColliderType.None);
            var wallTile = CreateTile("Wall", sprites.Wall, Tile.ColliderType.Grid);
            var actions = LoadOrCreateInputActions();
            var playerPrefab = CreatePlayerPrefab(sprites, actions);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCameraAndLight();
            var roomBounds = CreateRoom(floorTile, wallTile);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = Vector3.zero;
            CreateFollowCamera(player.transform, roomBounds);
            CreateMobileControls(sprites.Circle);
            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            ConfigurePlayerSettings();

            if (AssetDatabase.IsValidFolder(TemplateSampleScenesDir))
                AssetDatabase.DeleteAsset(TemplateSampleScenesDir);

            AssetDatabase.SaveAssets();
            Debug.Log($"[M0SandboxBuilder] Built {ScenePath}");
        }

        // ---------------------------------------------------------------- Sprites & tiles

        private readonly struct PlaceholderSprites
        {
            public readonly Sprite Circle;
            public readonly Sprite Triangle;
            public readonly Sprite Floor;
            public readonly Sprite Wall;

            public PlaceholderSprites(Sprite circle, Sprite triangle, Sprite floor, Sprite wall)
            {
                Circle = circle;
                Triangle = triangle;
                Floor = floor;
                Wall = wall;
            }
        }

        private static PlaceholderSprites CreatePlaceholderSprites()
        {
            var circle = CreateSprite("Circle", 64, 64, FilterMode.Bilinear, (x, y, s) =>
            {
                float r = s / 2f;
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                return new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
            });

            // Points up; 0.5 units tall.
            var triangle = CreateSprite("Triangle", 32, 64, FilterMode.Bilinear, (x, y, s) =>
            {
                float halfWidth = (s - y - 0.5f) / 2f;
                float dx = Mathf.Abs(x + 0.5f - s / 2f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(halfWidth - dx + 0.5f));
            });

            var floorColor = new Color(0.20f, 0.21f, 0.26f);
            var floor = CreateSprite("FloorTile", 16, 16, FilterMode.Point, (x, y, s) =>
                x == 0 || y == 0 ? floorColor * 0.85f : floorColor);

            var wallColor = new Color(0.45f, 0.42f, 0.38f);
            var wall = CreateSprite("WallTile", 16, 16, FilterMode.Point, (x, y, s) =>
            {
                if (y >= s - 2) return wallColor * 1.25f; // top highlight
                if (x == 0 || y == 0) return wallColor * 0.7f;
                return wallColor;
            });

            return new PlaceholderSprites(circle, triangle, floor, wall);
        }

        private static Sprite CreateSprite(string name, int size, int pixelsPerUnit, FilterMode filter,
            Func<int, int, int, Color> pixel)
        {
            string path = $"{PlaceholderArtDir}/{name}.png";

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

        private static Tile CreateTile(string name, Sprite sprite, Tile.ColliderType colliderType)
        {
            string path = $"{TilesDir}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.colliderType = colliderType;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        // ---------------------------------------------------------------- Input

        private static InputActionAsset LoadOrCreateInputActions()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "GameControls";
            asset.AddControlScheme(KeyboardMouse).WithRequiredDevice("<Keyboard>").WithRequiredDevice("<Mouse>");
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

            map.AddAction("AimPoint", InputActionType.PassThrough, "<Mouse>/position",
                groups: KeyboardMouse, expectedControlLayout: "Vector2");
            map.AddAction("AimStick", InputActionType.Value, "<Gamepad>/rightStick",
                groups: GamepadScheme, expectedControlLayout: "Vector2");

            AddButton(map, "Attack", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            AddButton(map, "Dodge", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton(map, "Skill1", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            AddButton(map, "Skill2", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            AddButton(map, "Interact", "<Keyboard>/f", "<Gamepad>/buttonWest");
            AddButton(map, "Potion", "<Keyboard>/r", "<Gamepad>/buttonNorth");

            File.WriteAllText(InputActionsPath, asset.ToJson());
            Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        }

        private static void AddButton(InputActionMap map, string name, string keyboardMouse, string gamepad)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding(keyboardMouse, groups: KeyboardMouse);
            action.AddBinding(gamepad, groups: GamepadScheme);
        }

        // ---------------------------------------------------------------- Player

        private static GameObject CreatePlayerPrefab(PlaceholderSprites sprites, InputActionAsset actions)
        {
            var root = new GameObject("Player");

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites.Circle;
            renderer.color = new Color(0.35f, 0.7f, 1f);
            renderer.sortingOrder = 10;

            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            root.AddComponent<CircleCollider2D>().radius = 0.4f;

            var indicator = new GameObject("AimIndicator");
            indicator.transform.SetParent(root.transform, false);
            var indicatorRenderer = indicator.AddComponent<SpriteRenderer>();
            indicatorRenderer.sprite = sprites.Triangle;
            indicatorRenderer.color = new Color(1f, 0.85f, 0.35f);
            indicatorRenderer.sortingOrder = 11;

            var input = root.AddComponent<PlayerInputReader>();
            SetReference(input, "actions", actions);

            var controller = root.AddComponent<PlayerController>();
            SetReference(controller, "input", input);

            var aim = root.AddComponent<PlayerAim>();
            SetReference(aim, "input", input);
            SetReference(aim, "controller", controller);
            SetReference(aim, "indicator", indicator.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------- Scene

        private static void CreateCameraAndLight()
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraOrthoSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.06f, 0.09f);
            GetOrAdd<UniversalAdditionalCameraData>(cameraGo);
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<CinemachineBrain>();

            var lightGo = new GameObject("Global Light 2D");
            lightGo.AddComponent<Light2D>().lightType = Light2D.LightType.Global;
        }

        /// <summary>Creates the test room and returns the collider used as camera bounds.</summary>
        private static Collider2D CreateRoom(TileBase floorTile, TileBase wallTile)
        {
            var grid = new GameObject("Room_Sandbox");
            grid.AddComponent<Grid>();

            var floor = CreateTilemap(grid.transform, "Floor", -10);
            var walls = CreateTilemap(grid.transform, "Walls", 0);
            walls.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var tilemapCollider = walls.gameObject.AddComponent<TilemapCollider2D>();
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = walls.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

            int xMin = -RoomWidth / 2;
            int yMin = -RoomHeight / 2;
            for (int x = xMin; x < xMin + RoomWidth; x++)
            for (int y = yMin; y < yMin + RoomHeight; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                floor.SetTile(cell, floorTile);
                bool border = x == xMin || x == xMin + RoomWidth - 1 || y == yMin || y == yMin + RoomHeight - 1;
                if (border) walls.SetTile(cell, wallTile);
            }

            // 2x2 pillars to test collision.
            foreach (var p in new[] { new Vector2Int(-7, 2), new Vector2Int(5, 2), new Vector2Int(-7, -4), new Vector2Int(5, -4) })
            for (int dx = 0; dx < 2; dx++)
            for (int dy = 0; dy < 2; dy++)
                walls.SetTile(new Vector3Int(p.x + dx, p.y + dy, 0), wallTile);

            // The composite's paths are serialized into the scene and NOT regenerated on load.
            // In batch mode the tilemap collider never gets its deferred update, so force it here.
            tilemapCollider.ProcessTilemapChanges();
            composite.GenerateGeometry();
            if (composite.pathCount == 0)
                throw new InvalidOperationException("Wall collider geometry is empty.");

            var bounds = new GameObject("CameraBounds") { layer = 2 }; // Ignore Raycast
            bounds.transform.SetParent(grid.transform, false);
            var poly = bounds.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            poly.points = new[]
            {
                new Vector2(xMin, yMin),
                new Vector2(xMin + RoomWidth, yMin),
                new Vector2(xMin + RoomWidth, yMin + RoomHeight),
                new Vector2(xMin, yMin + RoomHeight),
            };
            return poly;
        }

        private static Tilemap CreateTilemap(Transform grid, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid, false);
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            return tilemap;
        }

        private static void CreateFollowCamera(Transform target, Collider2D bounds)
        {
            var go = new GameObject("CM Follow Camera");
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Follow = target;
            var lens = vcam.Lens;
            lens.OrthographicSize = CameraOrthoSize;
            vcam.Lens = lens;

            var composer = go.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.Damping = new Vector3(0.4f, 0.4f, 0f);

            go.AddComponent<CinemachineConfiner2D>().BoundingShape2D = bounds;
        }

        // ---------------------------------------------------------------- Touch UI

        private static void CreateMobileControls(Sprite circle)
        {
            var canvasGo = new GameObject("MobileControls", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<MobileControls>();

            var safeArea = CreateUIObject("SafeArea", canvasGo.transform);
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
            safeArea.gameObject.AddComponent<SafeArea>();

            // Left: move stick
            var stickBase = CreateImage("MoveStick", safeArea, circle, new Color(1f, 1f, 1f, 0.15f), 280f,
                new Vector2(0f, 0f), new Vector2(240f, 240f));
            stickBase.GetComponent<Image>().raycastTarget = false;
            var knob = CreateImage("Knob", stickBase, circle, new Color(1f, 1f, 1f, 0.55f), 130f,
                new Vector2(0.5f, 0.5f), Vector2.zero);
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            var stickSo = new SerializedObject(stick);
            stickSo.FindProperty("m_ControlPath").stringValue = "<Gamepad>/leftStick";
            stickSo.FindProperty("m_MovementRange").floatValue = 110f;
            stickSo.ApplyModifiedPropertiesWithoutUndo();

            // Right: action buttons
            var bottomRight = new Vector2(1f, 0f);
            CreateButton("AttackButton", safeArea, circle, "ATK", 210f, bottomRight, new Vector2(-220f, 220f), "<Gamepad>/rightTrigger");
            CreateButton("DodgeButton", safeArea, circle, "DASH", 150f, bottomRight, new Vector2(-440f, 140f), "<Gamepad>/buttonSouth");
            CreateButton("Skill1Button", safeArea, circle, "S1", 120f, bottomRight, new Vector2(-410f, 340f), "<Gamepad>/leftShoulder");
            CreateButton("Skill2Button", safeArea, circle, "S2", 120f, bottomRight, new Vector2(-240f, 440f), "<Gamepad>/rightShoulder");
        }

        private static void CreateButton(string name, RectTransform parent, Sprite sprite, string label, float size,
            Vector2 anchor, Vector2 position, string controlPath)
        {
            var rect = CreateImage(name, parent, sprite, new Color(1f, 1f, 1f, 0.3f), size, anchor, position);
            var button = rect.gameObject.AddComponent<OnScreenButton>();
            var so = new SerializedObject(button);
            so.FindProperty("m_ControlPath").stringValue = controlPath;
            so.ApplyModifiedPropertiesWithoutUndo();

            var text = CreateUIObject("Label", rect).gameObject.AddComponent<Text>();
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.RoundToInt(size * 0.22f);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 1f, 1f, 0.9f);
            text.raycastTarget = false;
        }

        private static RectTransform CreateImage(string name, Transform parent, Sprite sprite, Color color, float size,
            Vector2 anchor, Vector2 position)
        {
            var rect = CreateUIObject(name, parent);
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

        private static RectTransform CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            return (RectTransform)go.transform;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // ---------------------------------------------------------------- Project settings

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        // ---------------------------------------------------------------- Helpers

        private static void SetReference(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field)
                       ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'.");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
    }
}

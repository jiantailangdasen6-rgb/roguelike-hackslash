using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Core;
using Game.EditorTools.Art;
using Game.Gameplay;
using Game.UI;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.SinglePlayer;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Generates the game from code: pixel art (via <see cref="ArtGenerator"/>), input actions, the networked
    /// player prefab, the NetworkManager prefab, post-processing, and the Lobby/Game scenes (dark crypt arena,
    /// candle lights, pixel-perfect camera). Re-running regenerates everything except the input actions asset.
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
        private const string PostProfilePath = SettingsDir + "/GamePost.asset";
        private const string GameArtPath = ResourcesDir + "/GameArt.asset";
        private const string PlayerPrefabPath = ResourcesDir + "/NetPlayer.prefab";
        private const string NetworkManagerPath = ResourcesDir + "/NetworkManager.prefab";
        private const string LobbyScenePath = ScenesDir + "/Lobby.unity";
        private const string GameScenePath = ScenesDir + "/Game.unity";
        private const string CampScenePath = ScenesDir + "/Camp.unity";

        private const string KeyboardMouse = "Keyboard&Mouse";
        private const string GamepadScheme = "Gamepad";

        [MenuItem("Tools/Game/Rebuild Game")]
        public static void Build()
        {
            foreach (var dir in new[] { ArtDir, TilesDir, ResourcesDir, SettingsDir, ScenesDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            ArtGenerator.Generate();
            if (!File.Exists(Audio.AudioGenerator.Dir + "/music_boss.wav")) Audio.AudioGenerator.Generate(); // slow: only when missing
            else if (!File.Exists(Audio.AudioGenerator.Dir + "/" + Audio.SoundPack.Marker)) Audio.SoundPack.Build();
            CreateGameArt();
            var actions = LoadOrCreateInputActions();
            var player = CreatePlayerPrefab(actions);
            CreateNetworkManagerPrefab(player);
            var post = CreatePostProfile();

            BuildLobbyScene(post);
            BuildCampScene(player, post);
            BuildGameScene(player, post);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(CampScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[PrototypeBuilder] Done");
        }

        // ================================================================ Shared art

        /// <summary>The arc sprite (sword wave) still comes from a tiny generated texture.</summary>
        private static void CreateGameArt()
        {
            var arc = CreateSprite("Arc", 64, (x, y, s) =>
            {
                float h = s / 2f, dx = x + 0.5f - h, dy = y + 0.5f - h;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / h;
                float angle = Mathf.Abs(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);
                return new Color(1f, 1f, 1f, r <= 1f && r > 0.7f && angle < 70f ? Mathf.Clamp01((r - 0.7f) * 6f) : 0f);
            });
            var gameArt = LoadOrCreate<GameArt>(GameArtPath);
            gameArt.Arc = arc;
            EditorUtility.SetDirty(gameArt);
        }

        private static Sprite CreateSprite(string name, int size, Func<int, int, int, Color> pixel)
        {
            string path = $"{ArtDir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, pixel(x, y, size));
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Sprite EnvSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtGenerator.TileDir}/{name}.png");

        private static Tile EnvTile(string name)
        {
            var tile = LoadOrCreate<Tile>($"{TilesDir}/{name}.asset");
            tile.sprite = EnvSprite(name);
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        // ================================================================ Input

        private static InputActionAsset LoadOrCreateInputActions()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "GameControls";
            asset.AddControlScheme(KeyboardMouse).WithRequiredDevice("<Keyboard>").WithOptionalDevice("<Mouse>");
            asset.AddControlScheme(GamepadScheme).WithRequiredDevice("<Gamepad>");

            var map = asset.AddActionMap(PlayerInputReader.MapName);
            var move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardMouse).With("Down", "<Keyboard>/s", KeyboardMouse)
                .With("Left", "<Keyboard>/a", KeyboardMouse).With("Right", "<Keyboard>/d", KeyboardMouse);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", KeyboardMouse).With("Down", "<Keyboard>/downArrow", KeyboardMouse)
                .With("Left", "<Keyboard>/leftArrow", KeyboardMouse).With("Right", "<Keyboard>/rightArrow", KeyboardMouse);
            move.AddBinding("<Gamepad>/leftStick", groups: GamepadScheme);

            map.AddAction("AimPoint", InputActionType.PassThrough, "<Mouse>/position", groups: KeyboardMouse, expectedControlLayout: "Vector2");
            map.AddAction("AimStick", InputActionType.Value, "<Gamepad>/rightStick", groups: GamepadScheme, expectedControlLayout: "Vector2");
            AddButton(map, "Attack", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            AddButton(map, "Skill1", "<Keyboard>/q", "<Gamepad>/rightShoulder", "<Mouse>/rightButton");
            AddButton(map, "Skill2", "<Keyboard>/e", "<Gamepad>/leftShoulder");
            AddButton(map, "Dodge", "<Keyboard>/space", "<Gamepad>/buttonSouth", "<Keyboard>/leftShift");

            File.WriteAllText(InputActionsPath, asset.ToJson());
            Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        }

        private static void AddButton(InputActionMap map, string name, string keyboardMouse, string gamepad, string extraKeyboardMouse = null)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding(keyboardMouse, groups: KeyboardMouse);
            if (extraKeyboardMouse != null) action.AddBinding(extraKeyboardMouse, groups: KeyboardMouse);
            action.AddBinding(gamepad, groups: GamepadScheme);
        }

        // ================================================================ Prefabs

        private static NetworkObject CreatePlayerPrefab(InputActionAsset actions)
        {
            var root = new GameObject("NetPlayer");
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncPositionZ = false;
            nt.SyncRotAngleX = nt.SyncRotAngleY = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            // Movement deltas unreliable (teleports and periodic full syncs stay reliable): over Relay a latency spike
            // otherwise makes the reliable pipeline resend 30 positions a second and overflow the send queue.
            nt.UseUnreliableDeltas = true;

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<SpriteRenderer>();
            var animator = body.AddComponent<PixelAnimator>();

            var shadow = Child(root, "Shadow", -500, new Vector3(0f, 0.02f), 1.1f);
            var marker = Child(root, "Marker", -490, new Vector3(0f, 0.02f), 0.3f);

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(root.transform, false);
            glowGo.transform.localPosition = new Vector3(0f, 0.6f);
            var glow = glowGo.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.pointLightOuterRadius = 6f;
            glow.pointLightInnerRadius = 0.3f;
            glow.intensity = 1f;
            glow.color = new Color(1f, 0.75f, 0.55f);
            glow.falloffIntensity = 0.65f;

            var input = root.AddComponent<PlayerInputReader>();
            input.enabled = false;
            SetRef(input, "actions", actions);
            var mover = root.AddComponent<PlayerMover>();
            mover.enabled = false;
            SetRef(mover, "input", input);
            var netPlayer = root.AddComponent<NetPlayer>();
            SetRef(netPlayer, "animator", animator);
            SetRef(netPlayer, "shadow", shadow);
            SetRef(netPlayer, "marker", marker);
            SetRef(netPlayer, "glow", glow);
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

        private static SpriteRenderer Child(GameObject parent, string name, int order, Vector3 offset, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = offset;
            go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>();
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
            // The default 128 packets is about a second of traffic with three players over Relay (WebSockets): a short
            // hitch filled it, a dropped reliable packet then held every later message and the run froze for everyone.
            transport.MaxPacketQueueSize = 1024;
            go.AddComponent<SinglePlayerTransport>(); // solo play without sockets (works in browsers)
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
            typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(networkObject, null);
            var hash = (uint)typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(networkObject);
            if (hash == 0) throw new InvalidOperationException($"{networkObject.name} has no GlobalObjectIdHash");
            EditorUtility.SetDirty(networkObject);
        }

        // ================================================================ Post-processing

        private static VolumeProfile CreatePostProfile()
        {
            if (File.Exists(PostProfilePath)) AssetDatabase.DeleteAsset(PostProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProfilePath);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.75f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.55f);
            vignette.color.Override(new Color(0.02f, 0f, 0.01f));
            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(18f);
            color.saturation.Override(-14f);
            color.postExposure.Override(0.15f);
            var lift = profile.Add<LiftGammaGain>(true);
            lift.lift.Override(new Vector4(0.98f, 0.97f, 1.04f, 0f));

            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Unlit sprite material for things that glow regardless of lighting (embers).</summary>
        private static Material UnlitMaterial()
        {
            const string path = SettingsDir + "/SpriteUnlit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                         ?? throw new InvalidOperationException("Sprite-Unlit-Default shader not found");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void CreatePostVolume(VolumeProfile profile)
        {
            var go = new GameObject("Post Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ================================================================ Scenes

        private static void BuildLobbyScene(VolumeProfile post)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(withBrain: false, pixelPerfect: false);
            CreatePostVolume(post);
            CreateEventSystem();
            new GameObject("Lobby").AddComponent<LobbyView>();
            new GameObject("Audio").AddComponent<SfxPlayer>();
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        /// <summary>
        /// The camp between runs: a night courtyard in front of a ruined chapel wall, lit by a big campfire. The gate
        /// into the labyrinth stands in the wall; the stations (<see cref="Camp.Stations"/>) are props around the fire.
        /// </summary>
        private static void BuildCampScene(NetworkObject playerPrefab, VolumeProfile post)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(withBrain: true, pixelPerfect: true);
            CreatePostVolume(post);
            var global = new GameObject("Global Light 2D").AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 0.62f;
            global.color = new Color(0.74f, 0.72f, 0.9f); // moonlight

            var bounds = CreateCampGround();
            CreateFollowCamera(bounds);

            var motes = new GameObject("Embers").AddComponent<AmbientMotes>();
            SetRef(motes, "sprite", EnvSprite("mote"));
            SetRef(motes, "unlit", UnlitMaterial());

            var directorGo = new GameObject("CampDirector");
            var directorObject = directorGo.AddComponent<NetworkObject>();
            SetRef(directorGo.AddComponent<CampDirector>(), "playerPrefab", playerPrefab);

            new GameObject("CampView").AddComponent<CampView>();
            new GameObject("PauseMenu").AddComponent<PauseMenuView>();
            new GameObject("Audio").AddComponent<SfxPlayer>();
            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, CampScenePath);
            EnsureGlobalObjectIdHash(directorObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CampScenePath);
        }

        private static Collider2D CreateCampGround()
        {
            AssetDatabase.SaveAssets();
            var b = Camp.Bounds;
            int xMin = (int)b.xMin, xMax = (int)b.xMax, yMin = (int)b.yMin, yMax = (int)b.yMax;
            var root = new GameObject("Camp").transform;
            root.gameObject.AddComponent<Grid>();

            var ground = new[] { EnvTile("camp_ground0"), EnvTile("camp_ground1"), EnvTile("camp_ground2"), EnvTile("camp_ground3") };
            var flags = new[] { EnvTile("floor0"), EnvTile("floor1"), EnvTile("floor2"), EnvTile("floor3") };
            var floor = CreateTilemap(root, "Floor", -1000);
            var walls = CreateTilemap(root, "Walls", -990);
            var tops = CreateTilemap(root, "WallTops", 900);
            var rng = new GameRandom(2026);
            for (int x = xMin; x < xMax; x++)
            for (int y = yMin; y < yMax; y++)
            {
                float r = rng.Value();
                // Old flagstones: a path from the fire to the gate and a landing before it; earth elsewhere.
                bool path = (x >= -2 && x <= 1 && y >= 0) || (x >= -4 && x <= 3 && y >= 4);
                bool worn = path && rng.Chance(x == -2 || x == 1 ? 0.35f : 0.1f); // ragged edges
                var tile = path && !worn
                    ? (r < 0.6f ? flags[0] : r < 0.8f ? flags[1] : r < 0.92f ? flags[2] : flags[3])
                    : (r < 0.55f ? ground[0] : r < 0.78f ? ground[1] : r < 0.93f ? ground[2] : ground[3]);
                floor.SetTile(new Vector3Int(x, y, 0), tile);
            }
            var wall = EnvTile("wall");
            var wallAlt = EnvTile("wall_alt");
            var wallTop = EnvTile("walltop");
            for (int x = xMin - 1; x <= xMax; x++)
            {
                walls.SetTile(new Vector3Int(x, yMax, 0), rng.Chance(0.3f) ? wallAlt : wall);
                walls.SetTile(new Vector3Int(x, yMax + 1, 0), rng.Chance(0.3f) ? wallAlt : wall);
                tops.SetTile(new Vector3Int(x, yMax + 2, 0), wallTop);
                tops.SetTile(new Vector3Int(x, yMin - 1, 0), wallTop);
            }
            for (int y = yMin - 1; y <= yMax + 2; y++)
            {
                tops.SetTile(new Vector3Int(xMin - 1, y, 0), wallTop);
                tops.SetTile(new Vector3Int(xMax, y, 0), wallTop);
            }
            var shade = CreateTilemap(root, "Shade", -980);
            for (int x = xMin; x < xMax; x++) shade.SetTile(new Vector3Int(x, yMax - 1, 0), EnvTile("shade_top"));
            for (int y = yMin; y < yMax - 1; y++)
            {
                shade.SetTile(new Vector3Int(xMin, y, 0), EnvTile("shade_left"));
                shade.SetTile(new Vector3Int(xMax - 1, y, 0), EnvTile("shade_right"));
            }

            var props = new GameObject("Props").transform;
            props.SetParent(root, false);
            Vector2 fire = Camp.FirePosition;

            // The fire: scorched stone ring, flames, and the light the whole camp lives in.
            Loop(props, "camp_stones", fire + new Vector2(0f, 0.1f), 2f, order: -900);
            Loop(props, "camp_fire", fire, 10f);
            var fireLight = new GameObject("FireLight");
            fireLight.transform.SetParent(props, false);
            fireLight.transform.position = fire + new Vector2(0f, 0.8f);
            var light = fireLight.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.6f, 0.3f);
            light.pointLightOuterRadius = 11f;
            light.pointLightInnerRadius = 0.6f;
            light.falloffIntensity = 0.6f;
            var flicker = fireLight.AddComponent<LightFlicker>();
            var fso = new SerializedObject(flicker);
            fso.FindProperty("baseIntensity").floatValue = 1.45f;
            fso.FindProperty("amount").floatValue = 0.22f;
            fso.ApplyModifiedPropertiesWithoutUndo();
            Loop(props, "camp_log", new Vector2(-3.2f, -2.6f), 1f);
            Loop(props, "camp_log", new Vector2(3.2f, -2.6f), 1f, flip: true);

            // The gate into the labyrinth, with a slow portal in its arch and candles at its feet.
            var gate = Camp.Get(CampStation.Gate);
            Vector2 gateBase = new(gate.Position.x, 6.15f);
            Loop(props, "camp_gate", gateBase, 1f);
            var portal = Loop(props, "fx_portal", gateBase + new Vector2(0f, 1.62f), 6f, order: EnemyView.SortOrder(gateBase.y) + 1);
            portal.transform.localScale = Vector3.one * 2.15f;
            portal.GetComponent<SpriteRenderer>().color = new Color(1f, 0.82f, 0.62f);
            Light(props, gateBase + new Vector2(0f, 1.6f), new Color(1f, 0.42f, 0.3f), 1.3f, 4.5f);
            foreach (float x in new[] { -3.4f, 3.0f }) Candle(props, new Vector2(x, 5.5f), "fx_candle", new Color(1f, 0.62f, 0.32f));
            foreach (float x in new[] { -7.5f, 7.5f }) Prop(props, "banner", new Vector2(x, yMax + 1.1f), 1f, order: -980);

            // Stations.
            Loop(props, "camp_tablet", Camp.Get(CampStation.Tablet).Position + new Vector2(0f, -0.3f), 4f);
            Loop(props, "camp_mirror", Camp.Get(CampStation.Mirror).Position + new Vector2(0f, -0.3f), 5f);
            Candle(props, new Vector2(-8.1f, 3.3f), "fx_candle_soul", new Color(0.6f, 0.7f, 1f));
            Loop(props, "camp_chest", Camp.Get(CampStation.Vault).Position + new Vector2(0f, -0.1f), 1f);
            Candle(props, new Vector2(10.9f, 3.4f), "fx_candle", new Color(1f, 0.62f, 0.32f));
            Loop(props, "npc_master", Camp.Get(CampStation.Master).Position + new Vector2(0f, -0.2f), 5f);
            Loop(props, "camp_rack", new Vector2(-11.4f, -3.6f), 1f);
            Loop(props, "camp_dummy", new Vector2(-6.6f, -2.0f), 1f);
            Loop(props, "npc_crone", new Vector2(9.7f, -2.9f), 5f, flip: true);
            Loop(props, "camp_table", new Vector2(9.6f, -3.95f), 1f);
            Light(props, new Vector2(9.0f, -3.2f), new Color(0.75f, 0.55f, 1f), 0.9f, 3.2f);
            Loop(props, "camp_board", Camp.Get(CampStation.Board).Position + new Vector2(0f, -0.25f), 1f);
            Loop(props, "npc_keeper", new Vector2(7.0f, -5.2f), 4f, flip: true);
            Light(props, Camp.Get(CampStation.Board).Position + new Vector2(0f, 1.6f), new Color(1f, 0.7f, 0.4f), 0.8f, 2.6f);

            // Tents, dead trees, crates and bones round the edges.
            Loop(props, "camp_tent", new Vector2(-11.2f, 4.95f), 1f);
            Loop(props, "camp_tent", new Vector2(11.2f, 4.95f), 1f, flip: true);
            Loop(props, "camp_tree", new Vector2(-12.4f, 0.4f), 1f);
            Loop(props, "camp_tree2", new Vector2(12.5f, -0.6f), 1f, flip: true);
            Loop(props, "camp_tree2", new Vector2(-6.5f, -7.3f), 1f);
            Loop(props, "prop_crates", new Vector2(-11.6f, -6.95f), 1f);
            Loop(props, "prop_crates", new Vector2(11.6f, -6.95f), 1f, flip: true);
            for (int i = 0; i < 5; i++)
                Prop(props, "bones", new Vector2(rng.Range(xMin + 1f, xMax - 1f), rng.Chance(0.5f) ? rng.Range(yMin + 0.4f, yMin + 1.6f) : rng.Range(yMax - 2.6f, yMax - 1.6f)), 1f, order: -950);

            var boundsGo = new GameObject("CameraBounds") { layer = 2 };
            boundsGo.transform.SetParent(root, false);
            var poly = boundsGo.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            poly.points = new[]
            {
                new Vector2(xMin - 1, yMin - 1), new Vector2(xMax + 1, yMin - 1),
                new Vector2(xMax + 1, yMax + 3), new Vector2(xMin - 1, yMax + 3),
            };
            return poly;
        }

        /// <summary>A sprite sheet played in a loop (one frame = a still prop), sorted by its feet.</summary>
        private static GameObject Loop(Transform parent, string sheet, Vector2 position, float fps, int? order = null, bool flip = false)
        {
            var go = new GameObject(sheet);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order ?? EnemyView.SortOrder(position.y);
            r.flipX = flip;
            go.AddComponent<SpriteLoop>().Configure(sheet, fps);
            return go;
        }

        private static void BuildGameScene(NetworkObject playerPrefab, VolumeProfile post)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(withBrain: true, pixelPerfect: true);
            CreatePostVolume(post);
            var global = new GameObject("Global Light 2D").AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 0.8f;
            global.color = new Color(0.9f, 0.82f, 0.8f);

            var bounds = CreateArena(global);
            CreateFollowCamera(bounds);

            var motes = new GameObject("Embers").AddComponent<AmbientMotes>();
            SetRef(motes, "sprite", EnvSprite("mote"));
            SetRef(motes, "unlit", UnlitMaterial());

            var directorGo = new GameObject("RunDirector");
            var directorObject = directorGo.AddComponent<NetworkObject>();
            SetRef(directorGo.AddComponent<RunDirector>(), "playerPrefab", playerPrefab);

            new GameObject("EnemyView").AddComponent<EnemyView>();
            new GameObject("EventPresenter").AddComponent<EventPresenter>();
            new GameObject("HUD").AddComponent<HudView>();
            new GameObject("Perks").AddComponent<PerkView>();
            new GameObject("Result").AddComponent<ResultView>();
            new GameObject("PauseMenu").AddComponent<PauseMenuView>();
            new GameObject("Audio").AddComponent<SfxPlayer>();
            CreateMobileControls();
            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, GameScenePath);
            EnsureGlobalObjectIdHash(directorObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
            WriteBuildStamp(directorObject);
        }

        /// <summary>
        /// The scene-placed RunDirector gets a new network id every time this scene is regenerated, so players from
        /// different builder runs can't share a session (the joiner never sees the director: empty HUD, frozen run).
        /// The id goes into Resources/BuildStamp.txt; joiners send it and the host refuses mismatches with a message.
        /// </summary>
        private static void WriteBuildStamp(NetworkObject director)
        {
            var hash = (uint)typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);
            string path = ResourcesDir + "/BuildStamp.txt";
            File.WriteAllText(path, hash.ToString("x8"));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void CreateCamera(bool withBrain, bool pixelPerfect)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.21875f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.015f, 0.025f);
            if (!go.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            go.AddComponent<AudioListener>();
            if (pixelPerfect)
            {
                var ppc = go.AddComponent<PixelPerfectCamera>();
                ppc.assetsPPU = 32;
                ppc.refResolutionX = 480;
                ppc.refResolutionY = 270;
                ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
                ppc.cropFrame = PixelPerfectCamera.CropFrame.None;
            }
            if (withBrain) go.AddComponent<CinemachineBrain>();
        }

        /// <summary>
        /// One look of the arena per land (crypt, blood chapel, abyss throne, sunken catacombs, frozen mausoleum, burnt forge): floor, back wall,
        /// black wall tops, wall shading, decorations and candles. <see cref="ArenaView"/> shows the current one
        /// and places each room's obstacles and traps at runtime.
        /// </summary>
        private static Collider2D CreateArena(Light2D globalLight)
        {
            AssetDatabase.SaveAssets();
            var b = RunSimulation.DefaultBounds;
            int xMin = (int)b.xMin, xMax = (int)b.xMax, yMin = (int)b.yMin, yMax = (int)b.yMax;

            var grid = new GameObject("Arena");
            grid.AddComponent<Grid>();
            var roots = new Transform[LandArt.Count];
            for (int area = 0; area < LandArt.Count; area++)
            {
                var root = new GameObject("Area" + area).transform;
                root.SetParent(grid.transform, false);
                roots[area] = root;
                BuildAreaLook(root, area, xMin, xMax, yMin, yMax);
                root.gameObject.SetActive(area == 0);
            }

            var view = grid.AddComponent<ArenaView>();
            var so = new SerializedObject(view);
            var list = so.FindProperty("areaRoots");
            list.arraySize = roots.Length;
            for (int i = 0; i < roots.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
            so.FindProperty("globalLight").objectReferenceValue = globalLight;
            so.ApplyModifiedPropertiesWithoutUndo();

            var boundsGo = new GameObject("CameraBounds") { layer = 2 };
            boundsGo.transform.SetParent(grid.transform, false);
            var poly = boundsGo.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            poly.points = new[]
            {
                new Vector2(xMin - 1, yMin - 1), new Vector2(xMax + 1, yMin - 1),
                new Vector2(xMax + 1, yMax + 3), new Vector2(xMin - 1, yMax + 3),
            };
            return poly;
        }

        private static void BuildAreaLook(Transform root, int area, int xMin, int xMax, int yMin, int yMax)
        {
            string suffix = area == 0 ? "" : "_" + area;
            var floorTiles = new[] { EnvTile("floor0" + suffix), EnvTile("floor1" + suffix), EnvTile("floor2" + suffix), EnvTile("floor3" + suffix) };
            var wall = EnvTile("wall" + suffix);
            var wallAlt = EnvTile("wall_alt" + suffix);
            var wallTop = EnvTile("walltop");

            var floor = CreateTilemap(root, "Floor", -1000);
            var walls = CreateTilemap(root, "Walls", -990);
            var tops = CreateTilemap(root, "WallTops", 900);

            var rng = new GameRandom((ulong)(11 + area * 17));
            for (int x = xMin; x < xMax; x++)
            for (int y = yMin; y < yMax; y++)
            {
                float r = rng.Value();
                var tile = r < 0.62f ? floorTiles[0] : r < 0.8f ? floorTiles[1] : r < 0.92f ? floorTiles[2] : floorTiles[3];
                // The chapel has a crimson carpet down the middle, from the entrance to the gate.
                if (area == 1 && x >= -2 && x <= 1) tile = EnvTile(x == -2 || x == 1 ? "carpet_edge_1" : "carpet_1");
                floor.SetTile(new Vector3Int(x, y, 0), tile);
            }
            for (int x = xMin - 1; x <= xMax; x++)
            {
                walls.SetTile(new Vector3Int(x, yMax, 0), rng.Chance(0.3f) ? wallAlt : wall);
                walls.SetTile(new Vector3Int(x, yMax + 1, 0), rng.Chance(0.3f) ? wallAlt : wall);
                tops.SetTile(new Vector3Int(x, yMax + 2, 0), wallTop);
                tops.SetTile(new Vector3Int(x, yMin - 1, 0), wallTop);
            }
            for (int y = yMin - 1; y <= yMax + 2; y++)
            {
                tops.SetTile(new Vector3Int(xMin - 1, y, 0), wallTop);
                tops.SetTile(new Vector3Int(xMax, y, 0), wallTop);
            }
            if (floor.GetUsedTilesCount() == 0) throw new InvalidOperationException($"Area {area} floor is empty (tiles missing?)");

            // Ambient occlusion along the walls.
            var shade = CreateTilemap(root, "Shade", -980);
            for (int x = xMin; x < xMax; x++) shade.SetTile(new Vector3Int(x, yMax - 1, 0), EnvTile("shade_top"));
            for (int y = yMin; y < yMax - 1; y++)
            {
                shade.SetTile(new Vector3Int(xMin, y, 0), EnvTile("shade_left"));
                shade.SetTile(new Vector3Int(xMax - 1, y, 0), EnvTile("shade_right"));
            }

            var props = new GameObject("Decor").transform;
            props.SetParent(root, false);
            string pillar = area == 0 ? "pillar" : "pillar_" + area;
            foreach (float x in new[] { -10.5f, -4.5f, 4.5f, 10.5f })
                Prop(props, pillar, new Vector2(x, yMax - 0.2f), 0.8f);

            string candleSheet;
            Color candleLight;
            switch (area)
            {
                case 0:
                    foreach (float x in new[] { -7.5f, 7.5f }) Prop(props, "banner", new Vector2(x, yMax + 1.1f), 1f, order: -980);
                    for (int i = 0; i < 7; i++) Prop(props, "bones", ScatterNearWalls(rng, xMin, xMax, yMin, yMax), 1f, order: -950);
                    candleSheet = "fx_candle";
                    candleLight = new Color(1f, 0.62f, 0.32f);
                    break;
                case 1:
                    foreach (float x in new[] { -12.5f, -1.6f, 1.6f, 12.5f }) Prop(props, "window_1", new Vector2(x, yMax + 0.25f), 1f, order: -985);
                    foreach (float x in new[] { -7.5f, 7.5f }) Prop(props, "banner_1", new Vector2(x, yMax + 0.6f), 1f, order: -980);
                    foreach (float x in new[] { -1.6f, 1.6f })
                        Light(props, new Vector2(x, yMax - 1.2f), new Color(0.9f, 0.35f, 0.4f), 0.7f, 3.5f); // light through the glass
                    candleSheet = "fx_candle_red";
                    candleLight = new Color(1f, 0.35f, 0.28f);
                    break;
                case 2:
                    foreach (float x in new[] { -12f, 12f }) Prop(props, "chains_2", new Vector2(x, yMax + 0.2f), 1f, order: -980);
                    foreach (float x in new[] { -7.5f, 7.5f }) Prop(props, "banner_2", new Vector2(x, yMax + 0.6f), 1f, order: -980);
                    for (int i = 0; i < 6; i++) Prop(props, "bones_2", ScatterNearWalls(rng, xMin, xMax, yMin, yMax), 1f, order: -950);
                    candleSheet = "fx_candle_soul";
                    candleLight = new Color(0.72f, 0.48f, 1f);
                    break;
                case 3:
                    // Sunken catacombs: roots and weed down the wall, broken urns, a cold teal glow off the water.
                    foreach (float x in new[] { -12.5f, -3.2f, 3.2f, 12.5f }) Prop(props, "roots_3", new Vector2(x, yMax + 0.4f), 1f, order: -982);
                    foreach (float x in new[] { -7.5f, 7.5f }) Prop(props, "banner_3", new Vector2(x, yMax + 0.5f), 1f, order: -980);
                    for (int i = 0; i < 6; i++) Prop(props, "urns_3", ScatterNearWalls(rng, xMin, xMax, yMin, yMax), 1f, order: -950);
                    foreach (float x in new[] { -6f, 6f })
                        Light(props, new Vector2(x, 0f), new Color(0.3f, 0.85f, 0.75f), 0.35f, 7f); // light off the water
                    candleSheet = "fx_candle_teal";
                    candleLight = new Color(0.4f, 0.95f, 0.8f);
                    break;
                case 4:
                    // Frozen mausoleum: icicles under the wall's lip, pale banners, snow heaped along the walls.
                    foreach (float x in new[] { -11f, -3.5f, 3.5f, 11f }) Prop(props, "icicles_4", new Vector2(x, yMax + 1.15f), 1f, order: -978);
                    foreach (float x in new[] { -7.5f, 7.5f }) Prop(props, "banner_4", new Vector2(x, yMax + 0.5f), 1f, order: -980);
                    for (int i = 0; i < 6; i++) Prop(props, "snow_4", ScatterNearWalls(rng, xMin, xMax, yMin, yMax), 1f, order: -950);
                    candleSheet = "fx_candle_ice";
                    candleLight = new Color(0.55f, 0.8f, 1f);
                    break;
                case 6:
                    // The heart: chains hanging over ash-red stone, bones of every land, embers glowing in the walls.
                    foreach (float x in new[] { -12f, -4f, 4f, 12f }) Prop(props, "chains_2", new Vector2(x, yMax + 0.2f), 1f, order: -980);
                    foreach (float x in new[] { -7.5f, 7.5f }) Prop(props, "banner_5", new Vector2(x, yMax + 0.5f), 1f, order: -980);
                    for (int i = 0; i < 8; i++) Prop(props, "bones_2", ScatterNearWalls(rng, xMin, xMax, yMin, yMax), 1f, order: -950);
                    foreach (float x in new[] { -10f, 0f, 10f })
                        Light(props, new Vector2(x, yMax - 0.6f), new Color(1f, 0.55f, 0.2f), 0.55f, 5f);
                    candleSheet = "fx_candle_red";
                    candleLight = new Color(1f, 0.45f, 0.25f);
                    break;
                case 7:
                    // Fungal grotto: glowing roots down the rock, mossy rags, toadstools along the walls, a green haze.
                    foreach (float x in new[] { -12.5f, -3.4f, 3.4f, 12.5f }) Prop(props, "vines_7", new Vector2(x, yMax + 0.4f), 1f, order: -982);
                    foreach (float x in new[] { -7.5f, 7.5f }) Prop(props, "banner_7", new Vector2(x, yMax + 0.5f), 1f, order: -980);
                    for (int i = 0; i < 8; i++) Prop(props, "shrooms_7", ScatterNearWalls(rng, xMin, xMax, yMin, yMax), 1f, order: -950);
                    foreach (float x in new[] { -8f, 0f, 8f })
                        Light(props, new Vector2(x, yMax - 0.8f), new Color(0.55f, 1f, 0.35f), 0.4f, 5f); // fungus glowing on the wall
                    candleSheet = "fx_candle_spore";
                    candleLight = new Color(0.6f, 1f, 0.4f);
                    break;
                case 8:
                    // Cursed library: candle stands by the shelves, navy banners, pages strewn over the parquet.
                    foreach (float x in new[] { -12f, -3.4f, 3.4f, 12f }) Prop(props, "candles_8", new Vector2(x, yMax - 0.3f), 1f, order: -979);
                    foreach (float x in new[] { -7.5f, 7.5f }) Prop(props, "banner_8", new Vector2(x, yMax + 0.5f), 1f, order: -980);
                    for (int i = 0; i < 7; i++) Prop(props, "papers_8", ScatterNearWalls(rng, xMin, xMax, yMin, yMax), 1f, order: -950);
                    foreach (float x in new[] { -12f, -3.4f, 3.4f, 12f })
                        Light(props, new Vector2(x, yMax - 0.1f), new Color(0.7f, 0.55f, 1f), 0.5f, 4f);
                    candleSheet = "fx_candle_violet";
                    candleLight = new Color(0.72f, 0.58f, 1f);
                    break;
                default: // 5: the forge
                    // Burnt forge: weapon racks, soot-black banners, slag cooling on the floor, the glow of the furnaces.
                    foreach (float x in new[] { -11.5f, 11.5f }) Prop(props, "rack_5", new Vector2(x, yMax + 0.2f), 1f, order: -980);
                    foreach (float x in new[] { -7.5f, 7.5f }) Prop(props, "banner_5", new Vector2(x, yMax + 0.5f), 1f, order: -980);
                    foreach (float x in new[] { -12f, 12f }) Prop(props, "chains_2", new Vector2(x + (x < 0 ? 4f : -4f), yMax + 0.2f), 1f, order: -981);
                    for (int i = 0; i < 7; i++) Prop(props, "slag_5", ScatterNearWalls(rng, xMin, xMax, yMin, yMax), 1f, order: -950);
                    foreach (float x in new[] { -9f, 9f })
                        Light(props, new Vector2(x, yMax - 0.5f), new Color(1f, 0.45f, 0.15f), 0.6f, 5f); // furnace mouths
                    candleSheet = "fx_candle_ember";
                    candleLight = new Color(1f, 0.5f, 0.2f);
                    break;
            }

            foreach (var c in new[]
                     {
                         new Vector2(-13.2f, 7.2f), new Vector2(13.2f, 7.2f), new Vector2(-13.2f, -7.4f), new Vector2(13.2f, -7.4f),
                         new Vector2(-7.5f, 7.4f), new Vector2(7.5f, 7.4f), new Vector2(-13.3f, 3.6f), new Vector2(13.3f, 3.6f),
                     })
                Candle(props, c, candleSheet, candleLight);
        }

        private static Vector2 ScatterNearWalls(GameRandom rng, int xMin, int xMax, int yMin, int yMax) =>
            new(rng.Range(xMin + 1f, xMax - 1f), rng.Chance(0.5f) ? rng.Range(yMin + 0.5f, yMin + 2.5f) : rng.Range(yMax - 3f, yMax - 1.5f));

        private static void Light(Transform parent, Vector2 at, Color color, float intensity, float radius)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = 0.2f;
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = order;
            return tilemap;
        }

        private static void Prop(Transform parent, string sprite, Vector2 position, float scale, int? order = null)
        {
            var go = new GameObject(sprite);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = EnvSprite(sprite);
            r.sortingOrder = order ?? EnemyView.SortOrder(position.y);
        }

        private static void Candle(Transform parent, Vector2 position, string sheet, Color color)
        {
            var go = new GameObject("Candle");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = EnemyView.SortOrder(position.y);
            var loop = go.AddComponent<SpriteLoop>();
            var so = new SerializedObject(loop);
            so.FindProperty("sheet").stringValue = sheet;
            so.FindProperty("fps").floatValue = 7f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.4f);
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.pointLightOuterRadius = 4.2f;
            light.pointLightInnerRadius = 0.2f;
            light.falloffIntensity = 0.7f;
            var flicker = lightGo.AddComponent<LightFlicker>();
            var fso = new SerializedObject(flicker);
            fso.FindProperty("baseIntensity").floatValue = 1.1f;
            fso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateFollowCamera(Collider2D bounds)
        {
            var go = new GameObject("CM Follow Camera");
            // Until the local player spawns there is nothing to follow: look at the arena from the usual distance.
            go.transform.position = new Vector3(0f, 0f, -10f);
            var vcam = go.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.OrthographicSize = 4.21875f;
            vcam.Lens = lens;
            var composer = go.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.Damping = new Vector3(0.25f, 0.25f, 0f);
            go.AddComponent<CinemachineConfiner2D>().BoundingShape2D = bounds;
            var offset = go.AddComponent<CinemachineCameraOffset>();
            go.AddComponent<CinemachinePixelPerfect>();
            SetRef(go.AddComponent<CameraShake>(), "offset", offset);
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>(); // assigns default UI actions itself when enabled
        }

        private static void CreateMobileControls()
        {
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/Circle.png");
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

            OnScreenButton(safeArea, circle, "ATK", 200f, new Vector2(-220f, 220f), "<Gamepad>/rightTrigger");
            OnScreenButton(safeArea, circle, "DODGE", 150f, new Vector2(-430f, 140f), "<Gamepad>/buttonSouth");
            OnScreenButton(safeArea, circle, "Q", 120f, new Vector2(-400f, 340f), "<Gamepad>/rightShoulder");
            OnScreenButton(safeArea, circle, "E", 120f, new Vector2(-230f, 440f), "<Gamepad>/leftShoulder");
        }

        private static void OnScreenButton(RectTransform parent, Sprite circle, string text, float size, Vector2 position, string path)
        {
            var rect = UiImage(text, parent, circle, new Color(1f, 1f, 1f, 0.3f), size, new Vector2(1f, 0f), position);
            var button = rect.gameObject.AddComponent<OnScreenButton>();
            var so = new SerializedObject(button);
            so.FindProperty("m_ControlPath").stringValue = path;
            so.ApplyModifiedPropertiesWithoutUndo();
            var label = UiObject("Label", rect).gameObject.AddComponent<Text>();
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 34;
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
            PlayerSettings.productName = "LabyrinthSurvivor";
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

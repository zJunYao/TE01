using System;
using System.IO;
using GameLogic.Test;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor.Commands.Capture;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic.Test.Editor
{
    /// <summary>
    /// 固定输入、固定输出的测试场景搭建命令；拒绝覆盖和保存其他场景。
    /// </summary>
    public static class GameTestSceneCommands
    {
        public const string ScenePath = "Assets/AssetRaw/Scenes/Game.unity";
        public const string BackgroundPath = "Assets/AssetRaw/UIRaw/Raw/Battle/BG_Battle_KitchenWarmWood.png";
        public const string BunPath = "Assets/AssetRaw/Actor/Food/Bun/Sprites/Food_Bun_Body.png";
        public const string EnemyPath = "Assets/AssetRaw/Actor/Enemy/Mold/Sprites/Enemy_Mold_Body.png";

        [CliCommand("tengine_create_game_test_scene", "预览或新建固定 Game 弹球测试场景，不覆盖已有资源。")]
        public static object Create(
            [CliArg("confirm", "明确执行新增场景。需先完成用户授权。")] bool confirm = false,
            [CliArg("dry_run", "只检查目标与输入，不修改 Unity 对象或资源。")] bool dryRun = false)
        {
            GuardEditor();
            if (File.Exists(ScenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Game 场景已存在，拒绝覆盖。");
            Sprite background = RequireSprite(BackgroundPath);
            Sprite bun = RequireSprite(BunPath);
            Sprite enemy = RequireSprite(EnemyPath);
            if (!AssetDatabase.IsValidFolder("Assets/AssetRaw/Scenes"))
                throw new InvalidOperationException("项目场景目录不存在，停止写入。");

            object preview = new
            {
                target = ScenePath,
                inputs = new[] { BackgroundPath, BunPath, EnemyPath },
                objects = new[] { "游戏相机", "木盘底图", "测试玩法", "包子主角", "紫色怪物", "瞄准线" },
                bounds = new { xMin = -4f, xMax = 4f, yMin = -7.35f, yMax = 7.25f },
                operation = "新建并打开独立场景；仅保存 Game；不改启动流程、构建列表、项目设置和导入设置"
            };
            if (dryRun) return preview;
            if (!confirm) throw new InvalidOperationException("请先预览并取得用户确认。");

            // 只在无脏场景时切换，不触发保存全部资源或静默丢弃用户工作。
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = new GameObject("游戏相机", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.085f, 0.045f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30f;

            SpriteRenderer board = CreateSprite("木盘底图", background, null, Vector2.zero, 9.41f, -10);
            // 底图宽高比保持原样；参考图约为 941 × 1672。
            board.transform.position = new Vector3(0f, 0f, 1f);
            GameObject root = new GameObject("测试玩法");
            GameObject bunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BunIdleAssetCommands.PrefabPath);
            if (bunPrefab == null) throw new InvalidOperationException("缺少分层包子预制体。");
            GameObject bunObject = (GameObject)PrefabUtility.InstantiatePrefab(bunPrefab, scene);
            bunObject.transform.SetParent(root.transform, false);
            bunObject.transform.position = GameTestSimulation.DefaultSpawn;
            bunObject.transform.localScale = Vector3.one * (1.12f / bun.bounds.size.x);
            SpriteRenderer bunRenderer = bunObject.transform.Find("Visual/Body").GetComponent<SpriteRenderer>();
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoldIdleAssetCommands.PrefabPath);
            if (enemyPrefab == null) throw new InvalidOperationException("缺少霉菌分层预制体。");
            var enemyObject = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
            enemyObject.transform.SetParent(root.transform, false);
            enemyObject.transform.position = GameTestSimulation.DefaultEnemy;
            enemyObject.transform.localScale = Vector3.one * (1.4f / enemy.bounds.size.x);
            SpriteRenderer enemyRenderer = enemyObject.transform.Find("Visual/Body").GetComponent<SpriteRenderer>();
            GameObject aimObject = new GameObject("瞄准线", typeof(LineRenderer));
            aimObject.transform.SetParent(root.transform, false);
            LineRenderer aim = aimObject.GetComponent<LineRenderer>();
            aim.useWorldSpace = true;
            aim.positionCount = 2;
            aim.startWidth = 0.055f;
            aim.endWidth = 0.018f;
            aim.startColor = new Color(1f, 0.98f, 0.8f, 0.95f);
            aim.endColor = new Color(1f, 0.98f, 0.8f, 0.55f);
            aim.sortingOrder = 5;
            aim.sharedMaterial = bunRenderer.sharedMaterial;
            aim.enabled = false;

            GameTestSceneController controller = root.AddComponent<GameTestSceneController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("_gameCamera").objectReferenceValue = camera;
            serialized.FindProperty("_bun").objectReferenceValue = bunRenderer;
            serialized.FindProperty("_enemy").objectReferenceValue = enemyRenderer;
            serialized.FindProperty("_aimLine").objectReferenceValue = aim;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Game 场景保存失败，请检查实际状态，不自动重试。");
            return new { created = ScenePath, validation = Validate() };
        }

        [CliCommand("tengine_validate_game_test_scene", "只读校验当前打开的 Game 测试场景与序列化引用。")]
        public static object Validate()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Game 场景未打开。");
            GameTestSceneController controller = null;
            SpriteRenderer board = null;
            int controllerCount = 0;
            int missing = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                foreach (GameTestSceneController candidate in root.GetComponentsInChildren<GameTestSceneController>(true))
                {
                    controllerCount++;
                    controller = candidate;
                }
                foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (AssetDatabase.GetAssetPath(renderer.sprite) == BackgroundPath) board = renderer;
            }
            if (missing != 0 || controllerCount != 1 || controller == null || board == null ||
                controller.GameCamera == null || controller.Bun == null ||
                controller.Enemy == null || controller.AimLine == null || controller.Bun.sprite == null || controller.Enemy.sprite == null)
                throw new InvalidOperationException("场景含丢失脚本或缺少测试引用。");
            if (AssetDatabase.GetAssetPath(controller.Bun.sprite) != BunPath ||
                AssetDatabase.GetAssetPath(controller.Enemy.sprite) != EnemyPath)
                throw new InvalidOperationException("角色图片不是本次确认的资源。");
            if (!controller.GameCamera.orthographic || !board.enabled || !controller.Bun.enabled || !controller.Enemy.enabled)
                throw new InvalidOperationException("场景相机或角色渲染配置无效。");
            return new { scene = ScenePath, missingScripts = missing, controller = controller.GetType().FullName,
                background = BackgroundPath, bun = BunPath, enemy = EnemyPath,
                boardSize = new { width = board.bounds.size.x, height = board.bounds.size.y }, sceneDirty = scene.isDirty };
        }

        [CliCommand("tengine_capture_game_test_scene", "预览或保存固定 Game 相机预览图，不保存场景或资源。")]
        public static object Capture(
            [CliArg("confirm", "确认写入一张新的测试预览图片。")] bool confirm = false,
            [CliArg("dry_run", "只预览截图位置与范围。")] bool dryRun = false)
        {
            GuardEditor();
            Validate();
            const string output = "Temp/GameTestScene-portrait.png";
            if (File.Exists(output)) throw new InvalidOperationException("测试预览图已存在，拒绝覆盖。");
            if (dryRun) return new { output, width = 540, height = 960,
                scope = "仅相机 RenderTexture 预览，不包含即时调试面板，不保存任何 Unity 资源" };
            if (!confirm) throw new InvalidOperationException("请先预览并确认截图写入。");
            // 调用已审核的包内相机渲染实现；只写 Temp，不触发资源导入或 SaveAssets。
            var result = CaptureCommands.CaptureGameView(540, 960);
            Directory.CreateDirectory("Temp");
            byte[] bytes = Convert.FromBase64String(result.Base64);
            using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write))
                stream.Write(bytes, 0, bytes.Length);
            return new { output = Path.GetFullPath(output), width = result.Width, height = result.Height,
                bytes = bytes.Length, source = "相机渲染，不等同于真实 GameView 截屏" };
        }

        private static void GuardEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器正在运行、编译或导入，停止场景写入。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("存在未保存场景，拒绝切换或保存其他用户资源。");
        }

        private static Sprite RequireSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("缺少已导入的 Sprite：" + path);
            return sprite;
        }

        private static SpriteRenderer CreateSprite(string name, Sprite sprite, Transform parent, Vector2 position, float width, int order)
        {
            GameObject go = new GameObject(name, typeof(SpriteRenderer));
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}

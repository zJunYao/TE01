using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using GameLogic.Test;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic.Test.Editor
{
    /// <summary>固定范围的分层包子资源生成与场景迁移，不保存其他脏资源。</summary>
    public static class BunIdleAssetCommands
    {
        public const string Root = "Assets/AssetRaw/Actor/Food/Bun";
        public const string PrefabPath = Root + "/Prefabs/Food_Bun.prefab";
        public const string ClipPath = Root + "/Animations/Food_Bun_Idle.anim";
        public const string ControllerPath = Root + "/Animations/Food_Bun.controller";
        private const string OldPath = Root + "/Sprites/Food_Bun_Idle.png";
        private const string Source = "../素材/包子分层素材/标准256";
        private static readonly string[] Names = { "Body", "Face_Idle", "Face_BlinkHalf", "Face_BlinkClosed", "Face_Focus", "Face_Impact", "Face_Celebrate" };

        [CliCommand("tengine_build_bun_idle", "导入固定七图、新建分层包子动画和预制体、迁移 Game 并删除旧包子图。")]
        public static object Build(
            [CliArg("confirm", "确认已获用户授权的新增、迁移与删除。 ")] bool confirm = false,
            [CliArg("dry_run", "只检查输入、引用与目标，不写入。 ")] bool dryRun = false)
        {
            Guard();
            string[] paths = Names.Select(n => Root + "/Sprites/Food_Bun_" + n + ".png").ToArray();
            string[] sources = Names.Select(n => Path.GetFullPath(Source + "/Food_Bun_" + n + ".png")).ToArray();
            foreach (string path in paths.Concat(new[] { PrefabPath, ClipPath, ControllerPath }))
                if (File.Exists(path)) throw new InvalidOperationException("拒绝覆盖已有资源：" + path);
            foreach (string path in sources)
            {
                if (!File.Exists(path)) throw new FileNotFoundException("缺少输入素材", path);
                var texture = new Texture2D(2, 2);
                try
                {
                    if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)) || texture.width != 256 || texture.height != 256)
                        throw new InvalidOperationException("输入必须为 256×256：" + path);
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            string oldGuid = AssetDatabase.AssetPathToGUID(OldPath);
            if (string.IsNullOrEmpty(oldGuid)) throw new InvalidOperationException("待删除旧图不存在，停止重复执行。");
            string[] references = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p != OldPath)
                .Where(p => AssetDatabase.GetDependencies(p, false).Contains(OldPath)).ToArray();
            if (references.Any(p => p != GameTestSceneCommands.ScenePath))
                throw new InvalidOperationException("旧图存在范围外引用：" + string.Join(",", references));
            if (!File.Exists(GameTestSceneCommands.ScenePath)) throw new InvalidOperationException("Game 场景不存在。");
            object preview = new { inputs = sources.Select(p => new { path = p, sha256 = Hash(p) }).ToArray(),
                sprites = paths, prefab = PrefabPath, clip = ClipPath, controller = ControllerPath,
                migrate = GameTestSceneCommands.ScenePath, delete = OldPath, oldGuid, references,
                settings = "256/Single/FullRect/PPU100/Center/Bilinear/Clamp/无Mipmap/无读写",
                scope = "仅保存指定资源与Game；不新增场景，不改变构建列表；最后删除旧图" };
            if (dryRun) return preview;
            if (!confirm) throw new InvalidOperationException("必须先预览和确认。");

            for (int i = 0; i < paths.Length; i++)
            {
                File.Copy(sources[i], paths[i], false);
                AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(paths[i]);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                importer.SetTextureSettings(settings);
                importer.spritePixelsPerUnit = 100;
                importer.maxTextureSize = 256;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            Folder(Root + "/Animations");
            Folder(Root + "/Prefabs");
            var clip = new AnimationClip { name = "包子待机", frameRate = 30 };
            SetCurve(clip, "m_LocalScale.x", 1f, 0.98f, 1f);
            SetCurve(clip, "m_LocalScale.y", 1f, 1.025f, 1f);
            SetCurve(clip, "m_LocalScale.z", 1f, 1f, 1f);
            // 身体底部约在中心以下 0.93 单位，补偿缩放使其保持稳定。
            SetCurve(clip, "m_LocalPosition.y", 0f, 0.02325f, 0f);
            var faceBinding = EditorCurveBinding.PPtrCurve("Visual/Face", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, faceBinding, new[]
            {
                Key(0f, "Idle"), Key(2.8f, "BlinkHalf"), Key(2.87f, "BlinkClosed"),
                Key(2.97f, "BlinkHalf"), Key(3.04f, "Idle"), Key(4f, "Idle")
            });
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            clipSettings.stopTime = 4f;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            AssetDatabase.CreateAsset(clip, ClipPath);
            var animatorController = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var state = animatorController.layers[0].stateMachine.AddState("待机");
            state.motion = clip;
            animatorController.layers[0].stateMachine.defaultState = state;
            AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.SaveAssetIfDirty(animatorController);
            var root = new GameObject("Food_Bun");
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                AddSprite(visual.transform, "Body", Sprite("Body"), 10);
                AddSprite(visual.transform, "Face", Sprite("Face_Idle"), 11);
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("预制体保存失败。");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            Migrate();
            // 迁移后只读检查全部资源的直接依赖，避免旧图仍被使用时删除。
            string[] remaining = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p != OldPath)
                .Where(p => AssetDatabase.GetDependencies(p, false).Contains(OldPath)).ToArray();
            if (remaining.Length != 0) throw new InvalidOperationException("迁移后仍有旧图依赖，未删除：" + string.Join(",", remaining));
            if (!AssetDatabase.DeleteAsset(OldPath)) throw new InvalidOperationException("旧图删除失败。");
            return Validate();
        }

        [CliCommand("tengine_validate_bun_idle", "只读检查分层Sprite、预制体及待机曲线，不创建场景。")]
        public static object Validate()
        {
            foreach (string name in Names)
            {
                string path = Root + "/Sprites/Food_Bun_" + name + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var sprite = Sprite(name);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (sprite.rect.size != new Vector2(256, 256) || importer.maxTextureSize != 256 ||
                    importer.spriteImportMode != SpriteImportMode.Single || importer.spritePixelsPerUnit != 100 ||
                    settings.spriteMeshType != SpriteMeshType.FullRect || settings.spritePivot != new Vector2(.5f, .5f) ||
                    importer.mipmapEnabled || importer.isReadable || !importer.alphaIsTransparency)
                    throw new InvalidOperationException("Sprite 规格错误：" + path);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (prefab == null || clip == null || !clip.isLooping || clip.length < 4f || clip.length > 4.04f ||
                Mathf.Abs(AnimationUtility.GetAnimationClipSettings(clip).stopTime - 4f) > .001f)
                throw new InvalidOperationException("预制体或动画无效。");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.NewPreviewScene());
            Scene temporary = instance.scene;
            try
            {
                Transform visual = instance.transform.Find("Visual");
                var body = visual.Find("Body").GetComponent<SpriteRenderer>();
                var face = visual.Find("Face").GetComponent<SpriteRenderer>();
                if (face.sortingOrder <= body.sortingOrder) throw new InvalidOperationException("表情层级错误。");
                clip.SampleAnimation(instance, .8f);
                if (Mathf.Abs(visual.localScale.y - 1.025f) > .001f || instance.transform.localScale != Vector3.one)
                    throw new InvalidOperationException("呼吸曲线或根节点错误。");
                clip.SampleAnimation(instance, 2.9f);
                if (face.sprite != Sprite("Face_BlinkClosed")) throw new InvalidOperationException("闭眼采样失败。");
                clip.SampleAnimation(instance, 3.1f);
                if (face.sprite != Sprite("Face_Idle")) throw new InvalidOperationException("睁眼恢复失败。");
            }
            finally { EditorSceneManager.ClosePreviewScene(temporary); }
            return new { prefab = PrefabPath, clip = ClipPath, sprites = Names.Length, duration = clip.length,
                loop = clip.isLooping, sampled = new[] { "呼吸", "闭眼", "睁眼", "根节点稳定", "层级" },
                oldDeleted = !File.Exists(OldPath), scope = "临时内存预览上下文已关闭，不创建或保存预览场景文件" };
        }

        [CliCommand("tengine_fix_bun_idle_duration", "仅修正本次包子动画的循环终点为四秒，并执行只读采样校验。")]
        public static object FixDuration(
            [CliArg("confirm", "确认本次待机动画时长修正。 ")] bool confirm = false,
            [CliArg("dry_run", "只显示固定目标与时长。 ")] bool dryRun = false)
        {
            Guard();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null) throw new InvalidOperationException("动画不存在。");
            if (dryRun) return new { asset = ClipPath, stopTime = 4f, loopTime = true };
            if (!confirm) throw new InvalidOperationException("必须确认。");
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.stopTime = 4f;
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            return Validate();
        }

        private static void Migrate()
        {
            Scene scene = SceneManager.GetSceneByPath(GameTestSceneCommands.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameTestSceneCommands.ScenePath, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameTestSceneController>(true)).Single();
                SpriteRenderer old = controller.Bun;
                if (old == null || AssetDatabase.GetAssetPath(old.sprite) != OldPath)
                    throw new InvalidOperationException("Game 包子引用变化，拒绝替换。");
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                actor.transform.SetParent(old.transform.parent, false);
                actor.transform.position = old.transform.position;
                actor.transform.rotation = old.transform.rotation;
                SpriteRenderer body = actor.transform.Find("Visual/Body").GetComponent<SpriteRenderer>();
                // 保持原角色可见宽度：旧图画布约86%为主体，新图主体同样约86%。
                actor.transform.localScale = old.transform.localScale;
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("_bun").objectReferenceValue = body;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Game 迁移保存失败。");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器忙，停止资源写入。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("存在未保存场景，停止写入。");
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Sprites/Food_Bun_" + name + ".png")
            ?? throw new InvalidOperationException("缺少 Sprite：" + name);
        private static ObjectReferenceKeyframe Key(float time, string name) => new ObjectReferenceKeyframe { time = time, value = Sprite("Face_" + name) };
        private static void SetCurve(AnimationClip clip, string property, float a, float b, float c)
        {
            var curve = AnimationCurve.EaseInOut(0, a, .8f, b);
            curve.AddKey(new Keyframe(1.6f, c));
            curve.AddKey(new Keyframe(2f, a));
            curve.AddKey(new Keyframe(2.8f, b));
            curve.AddKey(new Keyframe(3.6f, c));
            curve.AddKey(new Keyframe(4f, a));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Visual", typeof(Transform), property), curve);
        }
        private static void AddSprite(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
        }
        private static string Hash(string path)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
}

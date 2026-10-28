using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic.Test.Editor
{
    /// <summary>固定目标的霉菌分层资源导入与场景迁移，旧图最后删除。</summary>
    public static class MoldIdleAssetCommands
    {
        public const string Root = "Assets/AssetRaw/Actor/Enemy/Mold";
        public const string PrefabPath = Root + "/Prefabs/Enemy_Mold.prefab";
        public const string ClipPath = Root + "/Animations/Enemy_Mold_Idle.anim";
        public const string ControllerPath = Root + "/Animations/Enemy_Mold.controller";
        private const string OldPath = Root + "/Sprites/Enemy_Mold_Idle.png";
        private static readonly string[] Names = { "Body", "Face_Idle", "Face_BlinkHalf", "Face_BlinkClosed", "Face_Focus", "Face_Impact", "Face_Celebrate" };
        private static string SpritePath(string name) => Root + "/Sprites/Enemy_Mold_" + name + ".png";
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(name));
        private static string[] References() => AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p != OldPath)
            .Where(p => AssetDatabase.GetDependencies(p, false).Contains(OldPath)).ToArray();

        [CliCommand("tengine_build_mold_idle", "导入霉菌七图、新建待机与预制体，迁移Game并删除旧图。")]
        public static object Build(
            [CliArg("confirm", "确认新增、迁移与旧图删除已获授权。 ")] bool confirm = false,
            [CliArg("dry_run", "仅检查输入和引用，不写入。 ")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器忙，停止写入。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("存在未保存场景，停止写入。");
            string[] sources = Names.Select(n => Path.GetFullPath("../素材/霉菌分层素材/标准256/Enemy_Mold_" + n + ".png")).ToArray();
            foreach (string path in Names.Select(SpritePath).Concat(new[] { PrefabPath, ClipPath, ControllerPath }))
                if (File.Exists(path)) throw new InvalidOperationException("拒绝覆盖已有资源：" + path);
            foreach (string source in sources)
            {
                var texture = new Texture2D(2, 2);
                try
                {
                    if (!File.Exists(source) || !ImageConversion.LoadImage(texture, File.ReadAllBytes(source)) || texture.width != 256 || texture.height != 256)
                        throw new InvalidOperationException("输入缺失或不是256画布：" + source);
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            if (Sprite("Idle") == null) throw new InvalidOperationException("旧图不存在，停止重复执行。");
            string[] references = References();
            if (references.Any(p => p != GameTestSceneCommands.ScenePath)) throw new InvalidOperationException("旧图存在范围外引用：" + string.Join(",", references));
            if (!File.Exists(GameTestSceneCommands.ScenePath)) throw new InvalidOperationException("Game场景缺失。");
            if (dryRun) return new { inputs = sources.Select(p => new { path = p, sha256 = Hash(p) }).ToArray(),
                create = Names.Select(SpritePath).Concat(new[] { PrefabPath, ClipPath, ControllerPath }).ToArray(),
                migrate = GameTestSceneCommands.ScenePath, delete = OldPath, references,
                sceneHash = Hash(GameTestSceneCommands.ScenePath), duration = 3f,
                scope = "仅七图、新动画控制器预制体、Game敌人迁移与旧图删除；不新增场景，不改物理" };
            if (!confirm) throw new InvalidOperationException("必须先预览并取得确认。");

            for (int i = 0; i < Names.Length; i++)
            {
                string path = SpritePath(Names[i]);
                File.Copy(sources[i], path, false);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = Vector2.one * .5f;
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
            var clip = new AnimationClip { name = "Enemy_Mold_Idle", frameRate = 60 };
            Curve(clip, "m_LocalScale.x", new[] { 1f, .9825f, .965f, .9825f, 1f });
            Curve(clip, "m_LocalScale.y", new[] { 1f, 1.0225f, 1.045f, 1.0225f, 1f });
            Curve(clip, "m_LocalScale.z", new[] { 1f, 1f, 1f, 1f, 1f });
            Curve(clip, "localEulerAnglesRaw.z", new[] { 0f, 2.5f, 0f, -2.5f, 0f });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Visual/Face", typeof(SpriteRenderer), "m_Sprite"),
                new[] { Key(0, "Idle"), Key(2.15f, "BlinkHalf"), Key(2.21f, "BlinkClosed"), Key(2.33f, "BlinkHalf"), Key(2.39f, "Idle") });
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            clipSettings.stopTime = 3f;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            AssetDatabase.CreateAsset(clip, ClipPath);
            AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.AddState("待机");
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var root = new GameObject("Enemy_Mold");
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                // 动画轴心设在脚底附近，身体和表情同轴缩放与摇摆。
                visual.transform.localPosition = new Vector3(0, -.98f, 0);
                AddSprite(visual.transform, "Body", Sprite("Body"), 10);
                AddSprite(visual.transform, "Face", Sprite("Face_Idle"), 11);
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new InvalidOperationException("预制体保存失败。");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Migrate();
            string[] remaining = References();
            if (remaining.Length != 0) throw new InvalidOperationException("迁移后仍有旧图引用，不删除：" + string.Join(",", remaining));
            if (!AssetDatabase.DeleteAsset(OldPath)) throw new InvalidOperationException("旧图删除失败。");
            return Validate();
        }

        [CliCommand("tengine_fix_mold_idle_state", "仅修复本次霉菌控制器待机状态的保存，不重新创建资源或迁移场景。")]
        public static object FixState(
            [CliArg("confirm", "确认修复本次待机控制器。 ")] bool confirm = false,
            [CliArg("dry_run", "只显示固定目标与状态。 ")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器忙，停止修复。");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (controller == null || clip == null || controller.layers.Length != 1) throw new InvalidOperationException("缺少目标资源。");
            if (controller.layers[0].stateMachine.states.Any(s => s.state.name != "待机")) throw new InvalidOperationException("发现范围外状态。");
            if (dryRun) return new { target = ControllerPath, reuse = ClipPath,
                current = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray() };
            if (!confirm) throw new InvalidOperationException("必须确认。");
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == "待机") ?? machine.AddState("待机");
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            return Validate();
        }

        [CliCommand("tengine_validate_mold_idle", "只读检查霉菌分层、循环动画与根节点稳定，不保存场景。")]
        public static object Validate()
        {
            foreach (string name in Names)
            {
                var sprite = Sprite(name);
                var importer = AssetImporter.GetAtPath(SpritePath(name)) as TextureImporter;
                var settings = new TextureImporterSettings();
                if (sprite == null || importer == null) throw new InvalidOperationException("缺少分层图：" + name);
                importer.ReadTextureSettings(settings);
                if (sprite.rect.size != new Vector2(256, 256) || sprite.pivot != new Vector2(128, 128) ||
                    importer.spritePixelsPerUnit != 100 || importer.maxTextureSize != 256 || importer.mipmapEnabled || importer.isReadable ||
                    importer.spriteImportMode != SpriteImportMode.Single || settings.spriteMeshType != SpriteMeshType.FullRect)
                    throw new InvalidOperationException("导入规则错误：" + name);
            }
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (clip == null || !clip.isLooping || Mathf.Abs(clip.length - 3f) > .001f || controller == null || prefab == null ||
                controller.layers[0].stateMachine.defaultState == null ||
                controller.layers[0].stateMachine.defaultState.motion != clip || prefab.GetComponent<Animator>().runtimeAnimatorController != controller)
                throw new InvalidOperationException("动画控制器或预制体绑定错误。");
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.path != "Visual") throw new InvalidOperationException("动画影响范围错误。");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Transform visual = instance.transform.Find("Visual");
                var face = visual.Find("Face").GetComponent<SpriteRenderer>();
                if (face.sortingOrder <= visual.Find("Body").GetComponent<SpriteRenderer>().sortingOrder) throw new InvalidOperationException("表情层级错误。");
                clip.SampleAnimation(instance, 1.5f);
                if (Mathf.Abs(visual.localScale.y - 1.045f) > .001f) throw new InvalidOperationException("呼吸采样失败。");
                clip.SampleAnimation(instance, .75f);
                if (Mathf.Abs(Mathf.DeltaAngle(visual.localEulerAngles.z, 2.5f)) > .01f) throw new InvalidOperationException("摇摆采样失败。");
                clip.SampleAnimation(instance, 2.25f);
                if (face.sprite != Sprite("Face_BlinkClosed")) throw new InvalidOperationException("闭眼采样失败。");
                clip.SampleAnimation(instance, 2.5f);
                if (face.sprite != Sprite("Face_Idle")) throw new InvalidOperationException("睁眼采样失败。");
                clip.SampleAnimation(instance, 3f);
                if (visual.localScale != Vector3.one || Quaternion.Angle(visual.localRotation, Quaternion.identity) > .01f ||
                    instance.transform.localPosition != Vector3.zero || instance.transform.localScale != Vector3.one)
                    throw new InvalidOperationException("循环或根节点采样失败。");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            if (File.Exists(OldPath)) throw new InvalidOperationException("旧图尚未删除。");
            return new { prefab = PrefabPath, clip = ClipPath, sprites = Names.Length, duration = clip.length,
                sampled = new[] { "呼吸", "摇摆", "眨眼", "循环连续", "根节点稳定" }, oldDeleted = true, sceneFilesCreated = 0 };
        }

        private static void Migrate()
        {
            Scene scene = SceneManager.GetSceneByPath(GameTestSceneCommands.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameTestSceneCommands.ScenePath, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameTestSceneController>(true)).Single();
                var old = controller.Enemy;
                if (old == null || AssetDatabase.GetAssetPath(old.sprite) != OldPath || old.GetComponent<Animator>() != null)
                    throw new InvalidOperationException("旧敌人结构变化，停止迁移。");
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                actor.transform.SetParent(old.transform.parent, false);
                actor.transform.position = old.transform.position;
                actor.transform.rotation = old.transform.rotation;
                var body = actor.transform.Find("Visual/Body").GetComponent<SpriteRenderer>();
                // 保持原Sprite画布世界宽度；新旧主体留白略有差异，不改变模拟碰撞半径。
                actor.transform.localScale = old.transform.localScale * (old.sprite.bounds.size.x / body.sprite.bounds.size.x);
                actor.SetActive(old.gameObject.activeSelf);
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("_enemy").objectReferenceValue = body;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("场景保存失败，请检查实际结果。");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        private static void Curve(AnimationClip clip, string property, float[] values)
        {
            var curve = new AnimationCurve(values.Select((v, i) => new Keyframe(i * .75f, v)).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            // 摇摆首尾斜率相同，避免循环停顿；呼吸首尾零切线。
            if (property == "localEulerAnglesRaw.z")
            {
                var first = curve.keys[0]; first.inTangent = first.outTangent = 2.5f / .75f; curve.MoveKey(0, first);
                var last = curve.keys[4]; last.inTangent = last.outTangent = 2.5f / .75f; curve.MoveKey(4, last);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Visual", typeof(Transform), property), curve);
        }
        private static ObjectReferenceKeyframe Key(float time, string name) => new ObjectReferenceKeyframe { time = time, value = Sprite("Face_" + name) };
        private static void AddSprite(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, .98f, 0);
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }
    }
}

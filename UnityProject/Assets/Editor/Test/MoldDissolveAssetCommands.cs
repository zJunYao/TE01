using System;
using System.IO;
using System.Linq;
using GameLogic.Actor;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameLogic.Test.Editor
{
    public static class MoldDissolveAssetCommands
    {
        private const string ClipPath = MoldIdleAssetCommands.Root + "/Animations/Enemy_Mold_Dissolve.anim";
        private static readonly float[] Times = { 0, .045f, .079f, .08f, .72f };
        private static readonly float[] X = { 1, 1.22f, 1.12f, 1.12f, 1.12f };
        private static readonly float[] Y = { 1, .72f, 1.15f, 1.15f, 1.15f };
        private static readonly float[] Position = { -.98f, -.98f, -.98f, -.98f, -.98f };
        private static readonly float[] Alpha = { 1, 1, 1, 0, 0 };

        [CliCommand("tengine_build_mold_dissolve", "制作霉菌消散动画并更新指定控制器及预制体，不保存场景。")]
        public static object Build(
            [CliArg("confirm", "确认已授权的消散资源更新。 ")] bool confirm = false,
            [CliArg("dry_run", "只检查和展示目标。 ")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器忙。");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MoldIdleAssetCommands.ControllerPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoldIdleAssetCommands.PrefabPath);
            var face = AssetDatabase.LoadAssetAtPath<Sprite>(MoldIdleAssetCommands.Root + "/Sprites/Enemy_Mold_Face_Impact.png");
            if (controller == null || prefab == null || face == null || controller.layers.Length != 1)
                throw new InvalidOperationException("缺少已有霉菌资源。");
            if (File.Exists(ClipPath) || controller.layers[0].stateMachine.states.Any(s => s.state.name == "消散") ||
                prefab.GetComponent<MoldAnimationDriver>() != null) throw new InvalidOperationException("目标已存在，停止重复生成。");
            if (dryRun) return new { create = ClipPath,
                update = new[] { MoldIdleAssetCommands.ControllerPath, MoldIdleAssetCommands.PrefabPath },
                duration = .6f, scope = "仅消散动画、控制器和预制体驱动；不保存场景、不改物理图片" };
            if (!confirm) throw new InvalidOperationException("必须先预览并确认。");
            var clip = new AnimationClip { name = "Enemy_Mold_Dissolve", frameRate = 60 };
            Curve(clip, "Visual", typeof(Transform), "m_LocalScale.x", X);
            Curve(clip, "Visual", typeof(Transform), "m_LocalScale.y", Y);
            Curve(clip, "Visual", typeof(Transform), "m_LocalScale.z", Times.Select(_ => 1f).ToArray());
            Curve(clip, "Visual", typeof(Transform), "m_LocalPosition.y", Position);
            Curve(clip, "Visual", typeof(Transform), "localEulerAnglesRaw.z", new[] { 0f, -5f, 4f, -3f, 0f });
            foreach (string part in new[] { "Body", "Face" })
                Curve(clip, "Visual/" + part, typeof(SpriteRenderer), "m_Color.a", Alpha);
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Visual/Face", typeof(SpriteRenderer), "m_Sprite"),
                new[] { new ObjectReferenceKeyframe { time = 0, value = face } });
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.stopTime = MoldAnimationDriver.DissolveDuration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, ClipPath);
            AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            // 先同步已有控制器导入，再修改状态，避免新资源导入覆盖内存改动。
            AssetDatabase.ImportAsset(MoldIdleAssetCommands.ControllerPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MoldIdleAssetCommands.ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.AddState("消散");
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.ImportAsset(MoldIdleAssetCommands.ControllerPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var contents = PrefabUtility.LoadPrefabContents(MoldIdleAssetCommands.PrefabPath);
            try
            {
                contents.AddComponent<MoldAnimationDriver>();
                if (PrefabUtility.SaveAsPrefabAsset(contents, MoldIdleAssetCommands.PrefabPath) == null)
                    throw new InvalidOperationException("预制体保存失败，不自动重试。");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            return Validate();
        }

        [CliCommand("tengine_validate_mold_dissolve", "只读采样消散、检查绑定和根节点稳定，不保存场景。")]
        public static object Validate()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MoldIdleAssetCommands.ControllerPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoldIdleAssetCommands.PrefabPath);
            if (clip == null || clip.isLooping || Mathf.Abs(clip.length - MoldAnimationDriver.DissolveDuration) > .001f ||
                prefab == null || prefab.GetComponent<MoldAnimationDriver>() == null) throw new InvalidOperationException("动画或驱动错误。");
            var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == "消散").state;
            if (state.motion != clip || state.transitions.Length != 0) throw new InvalidOperationException("消散状态错误。");
            foreach (var binding in AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)))
                if (binding.path != "Visual" && binding.path != "Visual/Body" && binding.path != "Visual/Face")
                    throw new InvalidOperationException("动画影响范围错误。");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var visual = instance.transform.Find("Visual");
                for (int i = 0; i < Times.Length; i++)
                {
                    clip.SampleAnimation(instance, Times[i]);
                    if (Mathf.Abs(visual.localScale.x - X[i]) > .002f || Mathf.Abs(visual.localScale.y - Y[i]) > .002f ||
                        Mathf.Abs(visual.localPosition.y - Position[i]) > .002f) throw new InvalidOperationException("变换采样失败。");
                    foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>())
                        if (Mathf.Abs(renderer.color.a - Alpha[i]) > .002f) throw new InvalidOperationException("淡出采样失败。");
                    if (visual.Find("Face").GetComponent<SpriteRenderer>().sprite.name != "Enemy_Mold_Face_Impact")
                        throw new InvalidOperationException("受击表情错误。");
                    if (instance.transform.localPosition != Vector3.zero || instance.transform.localScale != Vector3.one ||
                        Quaternion.Angle(instance.transform.localRotation, Quaternion.identity) > .001f)
                        throw new InvalidOperationException("根节点被改变。");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            MoldBurstAssetCommands.ValidateParticles(prefab);
            return new { clip = ClipPath, duration = clip.length, keyframes = Times.Length, rootStable = true, sceneFilesCreated = 0 };
        }

        internal static void UpdateBurstClip(AnimationClip clip)
        {
            clip.ClearCurves();
            Curve(clip, "Visual", typeof(Transform), "m_LocalScale.x", X);
            Curve(clip, "Visual", typeof(Transform), "m_LocalScale.y", Y);
            Curve(clip, "Visual", typeof(Transform), "m_LocalScale.z", Times.Select(_ => 1f).ToArray());
            Curve(clip, "Visual", typeof(Transform), "m_LocalPosition.y", Position);
            Curve(clip, "Visual", typeof(Transform), "localEulerAnglesRaw.z", new[] { 0f, -5f, 0f, 0f, 0f });
            foreach (string part in new[] { "Body", "Face" })
            {
                Curve(clip, "Visual/" + part, typeof(SpriteRenderer), "m_Color.a", Alpha);
                var binding = EditorCurveBinding.FloatCurve("Visual/" + part, typeof(SpriteRenderer), "m_Color.a");
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                AnimationUtility.SetKeyRightTangentMode(curve, 2, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Visual/Face", typeof(SpriteRenderer), "m_Sprite"),
                new[] { new ObjectReferenceKeyframe { time = 0, value = AssetDatabase.LoadAssetAtPath<Sprite>(MoldIdleAssetCommands.Root + "/Sprites/Enemy_Mold_Face_Impact.png") } });
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.stopTime = MoldAnimationDriver.DissolveDuration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
        }

        private static void Curve(AnimationClip clip, string path, Type type, string property, float[] values)
        {
            var curve = new AnimationCurve(Times.Select((t, i) => new Keyframe(t, values[i], 0, 0)).ToArray());
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
        }
    }
}

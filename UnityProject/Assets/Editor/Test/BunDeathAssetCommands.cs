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
    /// <summary>固定目标的死亡动画制作和只读采样。</summary>
    public static class BunDeathAssetCommands
    {
        private const string Path = BunIdleAssetCommands.Root + "/Animations/Food_Bun_Death.anim";
        private static readonly float[] Times = { 0, .08f, .15f, .24f, .34f, .56f };
        private static readonly float[] X = { 1, 1.18f, .96f, 1.08f, .88f, .12f };
        private static readonly float[] Y = { 1, .72f, 1.04f, .84f, .72f, .10f };
        private static readonly float[] Angle = { 0, 0, -4, -10, -12, -18 };
        private static readonly float[] Alpha = { 1, 1, 1, 1, .85f, 0 };

        [CliCommand("tengine_build_bun_death", "新增固定包子死亡动画并更新现有控制器，不修改场景或图片。")]
        public static object Build(
            [CliArg("confirm", "确认已授权死亡动画及控制器更新。 ")] bool confirm = false,
            [CliArg("dry_run", "仅显示目标与当前状态。 ")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器忙，停止写入。");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BunIdleAssetCommands.ControllerPath);
            if (controller == null || controller.layers.Length != 1) throw new InvalidOperationException("控制器不符合预期。");
            var machine = controller.layers[0].stateMachine;
            if (File.Exists(Path) || machine.states.Any(s => s.state.name == "死亡"))
                throw new InvalidOperationException("死亡资源或状态已存在，拒绝重复创建。");
            var face = AssetDatabase.LoadAssetAtPath<Sprite>(BunIdleAssetCommands.Root + "/Sprites/Food_Bun_Face_Impact.png");
            if (face == null) throw new InvalidOperationException("缺少挤眼表情。");
            if (dryRun) return new { create = Path, update = BunIdleAssetCommands.ControllerPath,
                duration = BunAnimationDriver.DeathDuration, loop = false,
                states = machine.states.Select(s => s.state.name).ToArray(),
                scope = "只写死亡动画与控制器，不写预制体、场景、图片、物理；不接入死亡条件" };
            if (!confirm) throw new InvalidOperationException("必须先预览确认。");
            var clip = new AnimationClip { name = "Food_Bun_Death", frameRate = 60 };
            Curve(clip, "Visual", typeof(Transform), "m_LocalScale.x", X);
            Curve(clip, "Visual", typeof(Transform), "m_LocalScale.y", Y);
            Curve(clip, "Visual", typeof(Transform), "m_LocalScale.z", Times.Select(_ => 1f).ToArray());
            Curve(clip, "Visual", typeof(Transform), "m_LocalPosition.x", Times.Select(_ => 0f).ToArray());
            Curve(clip, "Visual", typeof(Transform), "m_LocalPosition.y", Y.Select(y => -.86f * (1 - y)).ToArray());
            Curve(clip, "Visual", typeof(Transform), "m_LocalPosition.z", Times.Select(_ => 0f).ToArray());
            Curve(clip, "Visual", typeof(Transform), "localEulerAnglesRaw.z", Angle);
            foreach (string part in new[] { "Body", "Face" })
                Curve(clip, "Visual/" + part, typeof(SpriteRenderer), "m_Color.a", Alpha);
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("Visual/Face", typeof(SpriteRenderer), "m_Sprite"),
                new[] { new ObjectReferenceKeyframe { time = 0, value = face } });
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.stopTime = BunAnimationDriver.DeathDuration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, Path);
            AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BunIdleAssetCommands.ControllerPath);
            machine = controller.layers[0].stateMachine;
            var death = machine.AddState("死亡");
            death.motion = clip;
            EditorUtility.SetDirty(death);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.ImportAsset(BunIdleAssetCommands.ControllerPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            return Validate();
        }

        [CliCommand("tengine_fix_bun_death_state", "修复本次死亡状态的持久化，复用已有动画，不修改其他资源。")]
        public static object FixState(
            [CliArg("confirm", "确认修复已授权死亡状态。 ")] bool confirm = false,
            [CliArg("dry_run", "只显示固定目标和当前状态。 ")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器忙。");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Path);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BunIdleAssetCommands.ControllerPath);
            if (clip == null || controller == null) throw new InvalidOperationException("缺少目标资源。");
            if (dryRun) return new { update = BunIdleAssetCommands.ControllerPath, reuse = Path,
                current = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray() };
            if (!confirm) throw new InvalidOperationException("必须确认。");
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(BunIdleAssetCommands.ControllerPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BunIdleAssetCommands.ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == "死亡") ?? machine.AddState("死亡");
            if (state.transitions.Length != 0) throw new InvalidOperationException("死亡状态存在意外过渡。");
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Path);
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.ImportAsset(BunIdleAssetCommands.ControllerPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            return Validate();
        }

        [CliCommand("tengine_validate_bun_death", "只读检查死亡状态、关键帧与根节点，不保存场景。")]
        public static object Validate()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Path);
            if (clip == null || clip.isLooping || Mathf.Abs(clip.length - .56f) > .001f)
                throw new InvalidOperationException("死亡动画时长或循环错误。");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BunIdleAssetCommands.ControllerPath);
            var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == "死亡").state;
            if (state.motion != clip || state.transitions.Length != 0) throw new InvalidOperationException("死亡状态配置错误。");
            foreach (var binding in AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)))
                if (binding.path != "Visual" && binding.path != "Visual/Body" && binding.path != "Visual/Face")
                    throw new InvalidOperationException("动画越界修改。");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(BunIdleAssetCommands.PrefabPath), scene);
                Transform visual = instance.transform.Find("Visual");
                for (int i = 0; i < Times.Length; i++)
                {
                    clip.SampleAnimation(instance, Times[i]);
                    if (Mathf.Abs(visual.localScale.x - X[i]) > .002f || Mathf.Abs(visual.localScale.y - Y[i]) > .002f ||
                        Mathf.Abs(Mathf.DeltaAngle(visual.localEulerAngles.z, Angle[i])) > .1f)
                        throw new InvalidOperationException("死亡变换采样失败。");
                    foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>())
                        if (Mathf.Abs(renderer.color.a - Alpha[i]) > .002f) throw new InvalidOperationException("淡出采样失败。");
                    if (instance.transform.localPosition != Vector3.zero || instance.transform.localScale != Vector3.one ||
                        Quaternion.Angle(instance.transform.localRotation, Quaternion.identity) > .001f)
                        throw new InvalidOperationException("动画影响根节点。");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return new { clip = Path, duration = clip.length, sampledKeyframes = Times.Length, rootStable = true, sceneFilesCreated = 0 };
        }

        private static void Curve(AnimationClip clip, string path, Type type, string property, float[] values)
        {
            // 零切线对应预览脚本的平滑插值，关键帧间无过冲。
            var curve = new AnimationCurve(Times.Select((time, i) => new Keyframe(time, values[i], 0, 0)).ToArray());
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
        }
    }
}

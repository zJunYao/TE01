using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using GameLogic.Actor;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameLogic.Test.Editor
{
    /// <summary>固定包子资源的飞行与碰撞动画制作，不保存场景或无关资源。</summary>
    public static class BunMotionAssetCommands
    {
        private const string Root = BunIdleAssetCommands.Root;
        private static readonly string[] ClipNames = { "Fly", "ImpactSide", "ImpactTop" };
        private static readonly string[] States = { "飞行", "侧撞", "上下撞" };
        private static string ClipPath(string name) => Root + "/Animations/Food_Bun_" + name + ".anim";
        private static Sprite Face(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Sprites/Food_Bun_Face_" + name + ".png");

        [CliCommand("tengine_build_bun_motion", "新增包子飞行和两种碰撞动画，修改指定控制器与预制体。")]
        public static object Build(
            [CliArg("confirm", "确认新增三段动画并更新指定控制器和预制体。 ")] bool confirm = false,
            [CliArg("dry_run", "只校验输入、路径和状态，不写资源。 ")] bool dryRun = false)
        {
            Guard();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BunIdleAssetCommands.ControllerPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BunIdleAssetCommands.PrefabPath);
            if (controller == null || prefab == null || Face("Focus") == null || Face("Impact") == null)
                throw new InvalidOperationException("缺少已有包子资源。");
            foreach (string name in ClipNames)
                if (File.Exists(ClipPath(name))) throw new InvalidOperationException("拒绝覆盖：" + ClipPath(name));
            if (controller.layers.Length != 1 || controller.layers[0].stateMachine.states.Length != 1 ||
                controller.layers[0].stateMachine.states[0].state.name != "待机")
                throw new InvalidOperationException("控制器不再是预期的单待机状态，停止修改。");
            if (prefab.GetComponent<BunAnimationDriver>() != null)
                throw new InvalidOperationException("动画驱动已存在，停止重复执行。");
            object preview = new { create = ClipNames.Select(ClipPath).ToArray(),
                update = new[] { BunIdleAssetCommands.ControllerPath, BunIdleAssetCommands.PrefabPath },
                inputs = new[] { Hash(BunIdleAssetCommands.ControllerPath), Hash(BunIdleAssetCommands.PrefabPath) },
                states = States, flightDuration = .6f, impactDuration = BunAnimationDriver.ImpactDuration,
                scope = "仅修改显示层动画与指定预制体，不写场景、不修改图片或物理设置" };
            if (dryRun) return preview;
            if (!confirm) throw new InvalidOperationException("必须先预览并取得确认。");

            var fly = Create("Fly", true, .6f, 0.97f, 1.04f, Face("Focus"));
            var side = Create("ImpactSide", false, BunAnimationDriver.ImpactDuration, .80f, 1.12f, Face("Impact"));
            var top = Create("ImpactTop", false, BunAnimationDriver.ImpactDuration, 1.12f, .80f, Face("Impact"));
            var machine = controller.layers[0].stateMachine;
            AnimatorState flying = machine.AddState("飞行");
            flying.motion = fly;
            AddImpact(machine, "侧撞", side, flying);
            AddImpact(machine, "上下撞", top, flying);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.ImportAsset(BunIdleAssetCommands.ControllerPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            GameObject contents = PrefabUtility.LoadPrefabContents(BunIdleAssetCommands.PrefabPath);
            try
            {
                contents.AddComponent<BunAnimationDriver>();
                if (PrefabUtility.SaveAsPrefabAsset(contents, BunIdleAssetCommands.PrefabPath) == null)
                    throw new InvalidOperationException("预制体保存失败，请检查实际结果，不自动重试。");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            return Validate();
        }

        [CliCommand("tengine_fix_bun_motion_states", "将已有三段动画的状态与返回关系持久化到指定包子控制器，不重建动画。")]
        public static object FixStates(
            [CliArg("confirm", "确认修复本次包子控制器状态保存。 ")] bool confirm = false,
            [CliArg("dry_run", "只显示控制器与当前状态。 ")] bool dryRun = false)
        {
            Guard();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BunIdleAssetCommands.ControllerPath);
            if (controller == null || controller.layers.Length != 1) throw new InvalidOperationException("控制器不符合预期。");
            var machine = controller.layers[0].stateMachine;
            if (machine.states.Any(s => !new[] { "待机", "飞行", "侧撞", "上下撞" }.Contains(s.state.name)))
                throw new InvalidOperationException("发现范围外状态，停止修复。");
            var clips = ClipNames.Select(n => AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(n))).ToArray();
            if (clips.Any(c => c == null)) throw new InvalidOperationException("已有动画缺失。");
            if (dryRun) return new { target = BunIdleAssetCommands.ControllerPath,
                current = machine.states.Select(s => s.state.name).ToArray(), states = States,
                scope = "仅持久化已授权飞行与碰撞状态，不覆盖动画或预制体" };
            if (!confirm) throw new InvalidOperationException("必须确认。");
            var fly = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == "飞行") ?? machine.AddState("飞行");
            fly.motion = clips[0];
            EditorUtility.SetDirty(fly);
            for (int i = 1; i < States.Length; i++)
            {
                var state = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == States[i]);
                if (state == null) AddImpact(machine, States[i], clips[i], fly);
                else if (state.motion != clips[i] || state.transitions.Length != 1 || state.transitions[0].destinationState != fly)
                    throw new InvalidOperationException("已有碰撞状态意外改变，停止修复。");
            }
            foreach (var state in machine.states.Select(s => s.state))
            {
                EditorUtility.SetDirty(state);
                foreach (var transition in state.transitions) EditorUtility.SetDirty(transition);
            }
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.ImportAsset(BunIdleAssetCommands.ControllerPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return Validate();
        }

        [CliCommand("tengine_validate_bun_motion", "只读采样包子飞行和碰撞动画，检查控制器返回与根节点稳定。")]
        public static object Validate()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BunIdleAssetCommands.ControllerPath);
            if (controller == null) throw new InvalidOperationException("控制器不存在。");
            var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
            foreach (string name in new[] { "待机", "飞行", "侧撞", "上下撞" })
                if (states.Count(s => s.name == name) != 1) throw new InvalidOperationException("状态不唯一：" + name);
            foreach (var state in states.Where(s => s.name == "侧撞" || s.name == "上下撞"))
                if (state.transitions.Length != 1 || state.transitions[0].destinationState.name != "飞行" ||
                    !state.transitions[0].hasExitTime || state.transitions[0].duration != 0f)
                    throw new InvalidOperationException("碰撞返回配置错误。");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BunIdleAssetCommands.PrefabPath);
            if (prefab == null || prefab.GetComponent<BunAnimationDriver>() == null)
                throw new InvalidOperationException("缺少动画驱动组件。");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Transform visual = instance.transform.Find("Visual");
                var face = visual.Find("Face").GetComponent<SpriteRenderer>();
                for (int i = 0; i < ClipNames.Length; i++)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(ClipNames[i]));
                    if (clip == null || clip.isLooping != (i == 0)) throw new InvalidOperationException("循环设置错误。");
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                        if (binding.path != "Visual") throw new InvalidOperationException("动画错误地控制了根节点。");
                    clip.SampleAnimation(instance, i == 0 ? .1f : .04f);
                    if (face.sprite != Face(i == 0 ? "Focus" : "Impact")) throw new InvalidOperationException("表情采样错误。");
                    if (i == 1 && !(visual.localScale.x < .9f && visual.localScale.y > 1f) ||
                        i == 2 && !(visual.localScale.y < .9f && visual.localScale.x > 1f))
                        throw new InvalidOperationException("撞击方向错误。");
                    if (instance.transform.localPosition != Vector3.zero || instance.transform.localScale != Vector3.one)
                        throw new InvalidOperationException("根节点被修改。");
                    if (i != 0)
                    {
                        clip.SampleAnimation(instance, BunAnimationDriver.ImpactDuration);
                        if ((visual.localScale - new Vector3(.97f, 1.04f, 1f)).sqrMagnitude > .0001f || face.sprite != Face("Focus"))
                            throw new InvalidOperationException("碰撞恢复飞行采样失败。");
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return new { clips = ClipNames.Select(ClipPath).ToArray(), sampled = true, rootStable = true,
                impactReturn = "飞行", cooldown = BunAnimationDriver.ImpactCooldown, sceneFilesCreated = 0 };
        }

        private static AnimationClip Create(string name, bool loop, float duration, float x, float y, Sprite face)
        {
            var clip = new AnimationClip { name = "Food_Bun_" + name, frameRate = 60 };
            Curve(clip, "m_LocalScale.x", loop ? Constant(duration, x) : Squash(duration, x, .97f));
            Curve(clip, "m_LocalScale.y", loop ? Constant(duration, y) : Squash(duration, y, 1.04f));
            Curve(clip, "m_LocalScale.z", Constant(duration, 1f));
            Curve(clip, "m_LocalPosition.x", Constant(duration, 0f));
            Curve(clip, "m_LocalPosition.y", Constant(duration, 0f));
            Curve(clip, "m_LocalPosition.z", Constant(duration, 0f));
            var binding = EditorCurveBinding.PPtrCurve("Visual/Face", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, loop
                ? new[] { new ObjectReferenceKeyframe { time = 0, value = face } }
                : new[] { new ObjectReferenceKeyframe { time = 0, value = face },
                    new ObjectReferenceKeyframe { time = .14f, value = Face("Focus") } });
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.stopTime = duration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, ClipPath(name));
            AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }
        private static AnimationCurve Constant(float duration, float value) => AnimationCurve.Linear(0f, value, duration, value);
        private static AnimationCurve Squash(float duration, float squash, float rest)
        {
            var curve = new AnimationCurve(new Keyframe(0f, rest), new Keyframe(.04f, squash),
                new Keyframe(.11f, rest + (rest - squash) * .15f), new Keyframe(duration, rest));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            return curve;
        }
        private static void Curve(AnimationClip clip, string property, AnimationCurve curve) =>
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Visual", typeof(Transform), property), curve);
        private static void AddImpact(AnimatorStateMachine machine, string name, AnimationClip clip, AnimatorState fly)
        {
            var state = machine.AddState(name);
            state.motion = clip;
            var transition = state.AddTransition(fly);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0f;
            transition.hasFixedDuration = true;
        }
        private static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器忙，停止写入。");
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }
    }
}

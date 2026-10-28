using System;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace GameLogic.Test.Editor
{
    /// <summary>仅更新已授权的霉菌爆散资源，不保存或新增场景。</summary>
    public static class MoldBurstAssetCommands
    {
        private const string Root = MoldIdleAssetCommands.Root;
        private const string TexturePath = Root + "/Sprites/Enemy_Mold_Puff.png";
        private const string MaterialPath = Root + "/Materials/Enemy_Mold_Puff.mat";
        private const string ClipPath = Root + "/Animations/Enemy_Mold_Dissolve.anim";

        [CliCommand("tengine_build_mold_burst", "固定范围更新霉菌爆散动画、预制体，新增碎团贴图及材质。")]
        public static object Build(
            [CliArg("confirm", "确认用户已授权爆散试作。 ")] bool confirm = false,
            [CliArg("dry_run", "仅预览固定输入和输出。 ")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器忙，停止写入。");
            string source = Path.GetFullPath("../素材/霉菌分层素材/爆散素材/Enemy_Mold_Puff.png");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoldIdleAssetCommands.PrefabPath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            var shader = Shader.Find("Sprites/Default");
            if (!File.Exists(source) || prefab == null || clip == null || shader == null || !shader.isSupported)
                throw new InvalidOperationException("输入或现有资源缺失。");
            if (File.Exists(TexturePath) || File.Exists(MaterialPath) || prefab.transform.Find("Burst") != null)
                throw new InvalidOperationException("爆散资源已存在，拒绝重复创建。");
            if (dryRun) return new { input = source, create = new[] { TexturePath, MaterialPath },
                update = new[] { ClipPath, MoldIdleAssetCommands.PrefabPath }, burstAt = .08f, duration = .72f,
                scope = "本体瞬间隐藏、18枚绒毛碎团平面爆散；不改场景、命中或物理" };
            if (!confirm) throw new InvalidOperationException("必须先预览并确认。");
            File.Copy(source, TexturePath, false);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
            var material = new Material(shader) { name = "Enemy_Mold_Puff", mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) };
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssetIfDirty(material);
            MoldDissolveAssetCommands.UpdateBurstClip(clip);
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var contents = PrefabUtility.LoadPrefabContents(MoldIdleAssetCommands.PrefabPath);
            try
            {
                var go = new GameObject("Burst");
                go.transform.SetParent(contents.transform, false);
                var particles = go.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = .6f;
                main.startLifetime = .575f;
                main.startSpeed = 0;
                main.gravityModifier = 0;
                main.maxParticles = 32;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var emission = particles.emission; emission.enabled = false;
                var shape = particles.shape; shape.enabled = false;
                var size = particles.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1), new Keyframe(.35f, .9f), new Keyframe(1, 0)));
                var color = particles.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(0, 1) });
                color.color = gradient;
                var limit = particles.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 1.2f;
                limit.dampen = .15f;
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 12;
                if (PrefabUtility.SaveAsPrefabAsset(contents, MoldIdleAssetCommands.PrefabPath) == null)
                    throw new InvalidOperationException("预制体保存失败，不自动重试。");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            return MoldDissolveAssetCommands.Validate();
        }

        internal static void ValidateParticles(GameObject prefab)
        {
            var node = prefab.transform.Find("Burst");
            var particles = node != null ? node.GetComponent<ParticleSystem>() : null;
            if (particles == null || particles.main.playOnAwake || particles.main.loop || !particles.emission.enabled ||
                particles.emission.burstCount != 1 || particles.emission.GetBurst(0).count.constant != 18 || !particles.shape.enabled ||
                particles.main.simulationSpace != ParticleSystemSimulationSpace.Local || !particles.sizeOverLifetime.enabled ||
                !particles.colorOverLifetime.enabled || particles.GetComponent<ParticleSystemRenderer>().sharedMaterial !=
                (AssetDatabase.LoadAssetAtPath<Material>(MoldEffectPrefabCommands.MaterialPath) ?? AssetDatabase.LoadAssetAtPath<Material>(MaterialPath)))
                throw new InvalidOperationException("爆散粒子绑定或模块配置错误。");
        }
    }
}

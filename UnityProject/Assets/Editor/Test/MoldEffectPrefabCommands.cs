using System;
using System.IO;
using System.Security.Cryptography;
using GameLogic.Actor;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace GameLogic.Test.Editor
{
    public static class MoldEffectPrefabCommands
    {
        public const string Root = "Assets/AssetRaw/Effects/Combat/Enemy/Mold";
        public const string PrefabPath = Root + "/Prefabs/FX_Mold_Burst.prefab";
        public const string TexturePath = Root + "/Textures/T_Mold_Puff.png";
        public const string MaterialPath = Root + "/Materials/M_Mold_Puff.mat";
        private const string OldTexture = MoldIdleAssetCommands.Root + "/Sprites/Enemy_Mold_Puff.png";
        private const string OldMaterial = MoldIdleAssetCommands.Root + "/Materials/Enemy_Mold_Puff.mat";

        [CliCommand("tengine_configure_mold_native_burst", "仅将独立霉菌特效改为原生Burst及二维Circle发散，支持编辑器预览。")]
        public static object ConfigureNativeBurst(
            [CliArg("confirm", "确认已授权原生粒子改造。 ")] bool confirm = false,
            [CliArg("dry_run", "只显示固定目标及配置。 ")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器忙，停止写入。");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<ParticleSystem>() == null) throw new InvalidOperationException("特效不存在。");
            if (dryRun) return new { target = PrefabPath, burst = "时间0，一次18枚", shape = "XY平面Circle，半径0.28",
                speed = "3.8至6.4", lifetime = "0.38至0.575秒", size = "0.24至0.66",
                scope = "仅保存独立特效粒子模块；不保存场景、角色、贴图或材质" };
            if (!confirm) throw new InvalidOperationException("必须先预览并确认。");
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var particles = contents.GetComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(3.8f, 6.4f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(.38f, .575f);
                main.startSize = new ParticleSystem.MinMaxCurve(.24f, .66f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(.89f, .825f, 1));
                var emission = particles.emission;
                emission.enabled = true;
                emission.rateOverTime = 0;
                emission.rateOverDistance = 0;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)18) });
                var shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = .28f;
                shape.radiusThickness = .6f;
                shape.arc = 360;
                shape.randomDirectionAmount = 0;
                var velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = 0;
                velocity.y = .5f;
                velocity.z = 0;
                var rotation = particles.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-130f * Mathf.Deg2Rad, 100f * Mathf.Deg2Rad);
                if (PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath) == null) throw new InvalidOperationException("特效保存失败，不自动重试。");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            Validate();
            return ValidateNativePreview();
        }

        [CliCommand("tengine_validate_mold_native_preview", "不运行播放组件，只模拟原生粒子验证发射、平面发散、清空及重播。")]
        public static object ValidateNativePreview()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                instance.GetComponent<MoldBurstEffect>().enabled = false;
                var particles = instance.GetComponent<ParticleSystem>();
                particles.Simulate(.02f, true, true, false);
                if (particles.particleCount != 18) throw new InvalidOperationException("原生预览未发射18枚粒子。");
                particles.Simulate(.1f, true, false, false);
                var buffer = new ParticleSystem.Particle[32];
                int count = particles.GetParticles(buffer);
                bool scattered = false;
                for (int i = 0; i < count; i++)
                {
                    if (Mathf.Abs(buffer[i].position.z) > .001f) throw new InvalidOperationException("粒子偏离二维平面。");
                    scattered |= buffer[i].position.magnitude > .3f;
                }
                if (count != 18 || !scattered) throw new InvalidOperationException("原生粒子未正常向外扩散。");
                particles.Simulate(1, true, false, false);
                if (particles.particleCount != 0) throw new InvalidOperationException("粒子未消失。");
                particles.Simulate(.02f, true, true, false);
                if (particles.particleCount != 18) throw new InvalidOperationException("原生重播失败。");
                return new { nativePreview = true, scriptDisabled = true, burstCount = 18, planar = true, replay = true, sceneFilesCreated = 0 };
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        [CliCommand("tengine_replace_mold_smoke", "仅用已确认烟雾候选替换固定贴图内容，保持GUID及导入设置。")]
        public static object ReplaceSmoke(
            [CliArg("confirm", "确认已授权替换烟雾贴图。 ")] bool confirm = false,
            [CliArg("dry_run", "只检查输入和目标。 ")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器忙，停止替换。");
            string source = Path.GetFullPath("../素材/霉菌分层素材/烟雾候选/T_Mold_Puff_256.png");
            if (!File.Exists(source) || AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) == null)
                throw new InvalidOperationException("候选或目标贴图不存在。");
            var image = new Texture2D(2, 2);
            try
            {
                if (!ImageConversion.LoadImage(image, File.ReadAllBytes(source)) || image.width != 256 || image.height != 256)
                    throw new InvalidOperationException("候选不是256透明贴图。");
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
            string guid = AssetDatabase.AssetPathToGUID(TexturePath);
            string sourceHash = Hash(source);
            string metaHash = Hash(TexturePath + ".meta");
            if (dryRun) return new { source, sourceHash, target = TexturePath, previousHash = Hash(TexturePath), guid,
                scope = "仅替换PNG并同步导入，不改材质、预制体、粒子参数、场景和导入设置" };
            if (!confirm) throw new InvalidOperationException("必须先预览并确认。");
            File.Copy(source, TexturePath, true);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.AssetPathToGUID(TexturePath) != guid || Hash(TexturePath) != sourceHash || Hash(TexturePath + ".meta") != metaHash)
                throw new InvalidOperationException("贴图替换内容、GUID或导入设置不符。");
            Validate();
            return new { texture = TexturePath, guidPreserved = true, importSettingsPreserved = true, sha256 = sourceHash };
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }

        [CliCommand("tengine_extract_mold_effect", "迁移固定碎团资源并抽取独立特效，更新怪物嵌套实例，不保存场景。")]
        public static object Extract(
            [CliArg("confirm", "确认已获迁移及预制体更新授权。 ")] bool confirm = false,
            [CliArg("dry_run", "只检查输入和输出。 ")] bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("编辑器忙，停止迁移。");
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>(MoldIdleAssetCommands.PrefabPath);
            if (actor == null || actor.transform.Find("Burst") == null ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(OldTexture) == null || AssetDatabase.LoadAssetAtPath<Material>(OldMaterial) == null)
                throw new InvalidOperationException("迁移输入缺失，禁止重复执行。");
            foreach (string path in new[] { PrefabPath, TexturePath, MaterialPath })
                if (File.Exists(path)) throw new InvalidOperationException("目标已存在，拒绝覆盖：" + path);
            string textureGuid = AssetDatabase.AssetPathToGUID(OldTexture);
            string materialGuid = AssetDatabase.AssetPathToGUID(OldMaterial);
            if (dryRun) return new { create = PrefabPath, move = new[] { new { from = OldTexture, to = TexturePath, guid = textureGuid },
                new { from = OldMaterial, to = MaterialPath, guid = materialGuid } }, update = MoldIdleAssetCommands.PrefabPath,
                scope = "独立特效、保留GUID、怪物嵌套实例及关闭实例自动播放；不改场景或打包配置" };
            if (!confirm) throw new InvalidOperationException("必须先预览并确认。");
            foreach (string folder in new[] { Root + "/Prefabs", Root + "/Textures", Root + "/Materials" }) Folder(folder);
            Move(OldTexture, TexturePath);
            Move(OldMaterial, MaterialPath);
            if (AssetDatabase.AssetPathToGUID(TexturePath) != textureGuid || AssetDatabase.AssetPathToGUID(MaterialPath) != materialGuid)
                throw new InvalidOperationException("迁移GUID不一致。");
            var contents = PrefabUtility.LoadPrefabContents(MoldIdleAssetCommands.PrefabPath);
            try
            {
                var old = contents.transform.Find("Burst");
                var effect = UnityEngine.Object.Instantiate(old.gameObject);
                try
                {
                    effect.name = "FX_Mold_Burst";
                    effect.transform.SetParent(null);
                    effect.transform.localPosition = Vector3.zero;
                    effect.transform.localRotation = Quaternion.identity;
                    effect.transform.localScale = Vector3.one;
                    effect.AddComponent<MoldBurstEffect>();
                    if (PrefabUtility.SaveAsPrefabAsset(effect, PrefabPath) == null) throw new InvalidOperationException("特效保存失败。");
                }
                finally { UnityEngine.Object.DestroyImmediate(effect); }
                var nested = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), contents.scene);
                nested.transform.SetParent(contents.transform, false);
                nested.name = "Burst";
                var serialized = new SerializedObject(nested.GetComponent<MoldBurstEffect>());
                serialized.FindProperty("_playOnEnable").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(nested.GetComponent<MoldBurstEffect>());
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                if (PrefabUtility.SaveAsPrefabAsset(contents, MoldIdleAssetCommands.PrefabPath) == null)
                    throw new InvalidOperationException("角色保存失败，不自动重试。");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            return Validate();
        }

        [CliCommand("tengine_validate_mold_effect", "只读检查独立特效、嵌套来源和迁移引用。")]
        public static object Validate()
        {
            var effect = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>(MoldIdleAssetCommands.PrefabPath);
            var node = actor != null ? actor.transform.Find("Burst") : null;
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (effect == null || effect.GetComponent<MoldBurstEffect>() == null || node == null || material == null ||
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node.gameObject) != PrefabPath ||
                node.GetComponent<MoldBurstEffect>() == null || effect.transform.localScale != Vector3.one ||
                material.mainTexture != AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) ||
                effect.GetComponent<ParticleSystemRenderer>().sharedMaterial != material || File.Exists(OldTexture) || File.Exists(OldMaterial))
                throw new InvalidOperationException("独立特效、迁移或嵌套引用错误。");
            if (!new SerializedObject(effect.GetComponent<MoldBurstEffect>()).FindProperty("_playOnEnable").boolValue ||
                new SerializedObject(node.GetComponent<MoldBurstEffect>()).FindProperty("_playOnEnable").boolValue)
                throw new InvalidOperationException("独立与嵌套播放策略错误。");
            MoldBurstAssetCommands.ValidateParticles(actor);
            return new { prefab = PrefabPath, material = MaterialPath, texture = TexturePath, nested = true, sceneFilesCreated = 0 };
        }

        private static void Move(string source, string target)
        {
            string error = AssetDatabase.MoveAsset(source, target);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

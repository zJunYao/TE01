using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GameLogic.Test.Tests
{
    public sealed class BunIdleAssetTests
    {
        private const string Root = "Assets/AssetRaw/Actor/Food/Bun/";

        [Test]
        public void 分层预制体与四秒循环绑定正确()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Food_Bun.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<Animator>(), Is.Not.Null);
            var body = prefab.transform.Find("Visual/Body").GetComponent<SpriteRenderer>();
            var face = prefab.transform.Find("Visual/Face").GetComponent<SpriteRenderer>();
            Assert.That(body.sprite.rect.size, Is.EqualTo(new Vector2(256, 256)));
            Assert.That(face.sortingOrder, Is.GreaterThan(body.sortingOrder));
            var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/Food_Bun_Idle.anim");
            Assert.That(controller.layers[0].stateMachine.defaultState.motion, Is.EqualTo(clip));
            Assert.That(clip.isLooping, Is.True);
            Assert.That(AnimationUtility.GetAnimationClipSettings(clip).stopTime, Is.EqualTo(4f).Within(.001f));
            Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip).Length, Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Sprites/Food_Bun_Idle.png"), Is.Null);
        }

        [Test]
        public void 七张素材保持统一画布与导入规则()
        {
            foreach (string name in new[] { "Body", "Face_Idle", "Face_BlinkHalf", "Face_BlinkClosed", "Face_Focus", "Face_Impact", "Face_Celebrate" })
            {
                string path = Root + "Sprites/Food_Bun_" + name + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(256, 256)), name);
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(128, 128)), name);
                Assert.That(importer.maxTextureSize, Is.EqualTo(256), name);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100), name);
                Assert.That(importer.mipmapEnabled || importer.isReadable, Is.False, name);
            }
        }
    }
}

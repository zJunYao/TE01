using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GameLogic.Actor;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace GameLogic.Test.Tests
{
    public sealed class GameTestSceneTests
    {
        private const string ScenePath = "Assets/AssetRaw/Scenes/Game.unity";
        private GameTestSceneController _controller;
        private bool _openedScene;

        [UnitySetUp]
        public IEnumerator 准备测试场景()
        {
#if UNITY_EDITOR
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                _openedScene = true;
            }
#endif
            _controller = Object.FindObjectOfType<GameTestSceneController>();
            Assert.That(_controller, Is.Not.Null, "必须运行已搭建的 Game 场景。");
            _controller.ResetRound();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator 释放测试状态()
        {
            if (_controller != null) _controller.ResetRound();
#if UNITY_EDITOR
            if (_openedScene)
            {
                yield return SceneManager.UnloadSceneAsync(ScenePath);
                _openedScene = false;
            }
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator 场景角色可见且命中会隐藏怪物()
        {
            Assert.That(_controller.Bun.sprite, Is.Not.Null);
            Assert.That(_controller.Enemy.sprite, Is.Not.Null);
            Assert.That(_controller.EnemyRoot.gameObject.activeSelf, Is.True);
            Assert.That(_controller.Launch(_controller.Simulation.EnemyPosition - _controller.Simulation.Position), Is.True);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!_controller.Simulation.HasWon && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_controller.Simulation.HasWon, Is.True);
            Assert.That(_controller.EnemyRoot.gameObject.activeSelf, Is.True, "命中后应先播放消散");
            Assert.That(_controller.EnemyRoot.GetComponent<MoldAnimationDriver>().IsDissolving, Is.True);
            yield return new WaitForSeconds(.8f);
            Assert.That(_controller.EnemyRoot.gameObject.activeSelf, Is.False);
            Assert.That(_controller.EnemyRoot.Find("Visual/Face").gameObject.activeInHierarchy, Is.False);
            _controller.ResetRound();
            Assert.That(_controller.EnemyRoot.gameObject.activeSelf, Is.True);
            Assert.That(_controller.EnemyRoot.Find("Visual/Face").gameObject.activeInHierarchy, Is.True);
            Assert.That((Vector2)_controller.BunRoot.position, Is.EqualTo(GameTestSimulation.DefaultSpawn));
        }

        [UnityTest]
        public IEnumerator 独立爆散特效自动播放重复播放及禁用清理()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/AssetRaw/Effects/Combat/Enemy/Mold/Prefabs/FX_Mold_Burst.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            try
            {
                var effect = instance.GetComponent<MoldBurstEffect>();
                var particles = instance.GetComponent<ParticleSystem>();
                Assert.That(instance.GetComponent<Animator>(), Is.Null);
                yield return null;
                Assert.That(particles.particleCount, Is.EqualTo(18));
                effect.Play();
                yield return null;
                Assert.That(particles.particleCount, Is.EqualTo(18), "重播应清空上次粒子，不叠加");
                instance.SetActive(false);
                Assert.That(particles.particleCount, Is.Zero);
                instance.SetActive(true);
                yield return null;
                Assert.That(particles.particleCount, Is.EqualTo(18));
                yield return new WaitForSeconds(.8f);
                Assert.That(effect.IsAlive, Is.False);
                Assert.That(instance.activeSelf, Is.True, "特效不擅自隐藏或销毁所属对象");
                effect.Play();
                yield return null;
                Assert.That(particles.particleCount, Is.EqualTo(18));
                effect.Clear();
                Assert.That(particles.particleCount, Is.Zero);
            }
            finally { Object.Destroy(instance); }
#else
            Assert.Ignore("独立预制体引用验证仅在Editor运行。");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator 霉菌消散不重复播放且中途重置可恢复()
        {
            var instance = Object.Instantiate(_controller.EnemyRoot.gameObject);
            try
            {
                var driver = instance.GetComponent<MoldAnimationDriver>();
                var animator = instance.GetComponent<Animator>();
                var visual = instance.transform.Find("Visual");
                Vector3 position = instance.transform.position;
                Vector3 scale = instance.transform.localScale;
                Assert.That(driver.PlayDissolve(), Is.True);
                Assert.That(driver.PlayDissolve(), Is.False);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("消散"), Is.True);
                animator.Update(.045f);
                Assert.That(visual.localScale.x, Is.EqualTo(1.22f).Within(.002f));
                animator.Update(.04f);
                // 普通帧等待可跨编辑器测试窗口运行，避免帧尾等待被窗口焦点挂起。
                yield return null;
                yield return null;
                var burst = instance.transform.Find("Burst").GetComponent<ParticleSystem>();
                Assert.That(visual.Find("Face").GetComponent<SpriteRenderer>().color.a, Is.EqualTo(0f).Within(.001f));
                Assert.That(burst.particleCount, Is.EqualTo(18), "爆开应只发射一批碎团");
                yield return new WaitForSeconds(.1f);
                var fragments = new ParticleSystem.Particle[32];
                int count = burst.GetParticles(fragments);
                Assert.That(count, Is.EqualTo(18));
                Assert.That(System.Array.Exists(fragments, p => p.remainingLifetime > 0 && p.position.magnitude > .3f), Is.True, "碎团应向外散开");
                driver.ResetToIdle();
                Assert.That(burst.particleCount, Is.Zero, "重置必须清除残留粒子");
                Assert.That(driver.IsDissolving, Is.False);
                Assert.That(instance.activeSelf, Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("待机"), Is.True);
                Assert.That(visual.localPosition.y, Is.EqualTo(-.98f).Within(.001f));
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
                foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>())
                    Assert.That(renderer.color.a, Is.EqualTo(1f).Within(.001f));
                yield return new WaitForSeconds(.7f);
                Assert.That(instance.activeSelf, Is.True, "重置不应受上次消散延迟影响");
                Assert.That(driver.PlayDissolve(), Is.True);
                yield return new WaitForSeconds(.8f);
                Assert.That(instance.activeSelf, Is.False);
                Assert.That(burst.particleCount, Is.Zero);
                Assert.That(instance.transform.position, Is.EqualTo(position));
                Assert.That(instance.transform.localScale, Is.EqualTo(scale));
                driver.ResetToIdle();
                Assert.That(instance.activeSelf, Is.True);
                Assert.That(visual.Find("Face").GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Enemy_Mold_Face_Idle"));
            }
            finally { Object.Destroy(instance); }
        }

        [UnityTest]
        public IEnumerator 霉菌分层待机呼吸眨眼且根节点稳定()
        {
            Transform root = _controller.EnemyRoot;
            var animator = root.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Transform visual = root.Find("Visual");
            var face = visual.Find("Face").GetComponent<SpriteRenderer>();
            Vector3 position = root.position;
            Vector3 scale = root.localScale;
            animator.Play("待机", 0, .5f);
            animator.Update(0);
            Assert.That(visual.localScale.y, Is.EqualTo(1.045f).Within(.001f));
            animator.Play("待机", 0, .75f);
            animator.Update(0);
            Assert.That(face.sprite.name, Is.EqualTo("Enemy_Mold_Face_BlinkClosed"));
            animator.Play("待机", 0, 0);
            animator.Update(0);
            yield return new WaitForSeconds(.2f);
            Assert.That(face.sprite.name, Is.EqualTo("Enemy_Mold_Face_Idle"));
            Assert.That(face.sortingOrder, Is.GreaterThan(_controller.Enemy.sortingOrder));
            Assert.That(root.position, Is.EqualTo(position));
            Assert.That(root.localScale, Is.EqualTo(scale));
        }

        [UnityTest]
        public IEnumerator 实际逐帧反弹且重置停止飞行()
        {
            _controller.Launch(Vector2.right);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_controller.Simulation.BounceCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_controller.Simulation.BounceCount, Is.GreaterThan(0));
            Assert.That(_controller.Simulation.EnemyAlive, Is.True);
            Assert.That(_controller.BunRoot.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("侧撞"), Is.True);
            _controller.ResetRound();
            yield return null;
            Assert.That(_controller.Simulation.IsFlying, Is.False);
            Assert.That(_controller.AimLine.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator 分层待机切换专注飞行且重置恢复()
        {
            Animator animator = _controller.BunRoot.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Transform visual = _controller.BunRoot.Find("Visual");
            Assert.That(visual.Find("Face").GetComponent<SpriteRenderer>().sortingOrder,
                Is.GreaterThan(_controller.Bun.sortingOrder));
            yield return new WaitForSeconds(.8f);
            Assert.That(visual.localScale.y, Is.GreaterThan(1.005f));
            Assert.That((Vector2)_controller.BunRoot.position, Is.EqualTo(GameTestSimulation.DefaultSpawn));
            _controller.Launch(Vector2.right);
            yield return null;
            Assert.That(animator.enabled, Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("飞行"), Is.True);
            Assert.That(visual.localScale.x, Is.EqualTo(.97f).Within(.001f));
            Assert.That(visual.localScale.y, Is.EqualTo(1.04f).Within(.001f));
            Assert.That(visual.Find("Face").GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Food_Bun_Face_Focus"));
            Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero));
            _controller.ResetRound();
            yield return null;
            Assert.That(animator.enabled, Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("待机"), Is.True);
        }

        [UnityTest]
        public IEnumerator 死亡保持消失屏蔽其他动画且重置恢复()
        {
            GameObject instance = Object.Instantiate(_controller.BunRoot.gameObject);
            try
            {
                var driver = instance.GetComponent<BunAnimationDriver>();
                var animator = instance.GetComponent<Animator>();
                Transform visual = instance.transform.Find("Visual");
                Vector3 position = instance.transform.position;
                Vector3 scale = instance.transform.localScale;
                driver.SetFlying(true);
                Assert.That(driver.PlayImpact(Vector2.right), Is.True);
                Assert.That(driver.PlayDeath(), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("死亡"), Is.True);
                Assert.That(driver.PlayDeath(), Is.False, "重复死亡不应从头播放");
                Assert.That(driver.IsDead, Is.True);
                Assert.That(driver.IsFlying, Is.False);
                driver.SetFlying(true);
                driver.SetFlying(false);
                Assert.That(driver.PlayImpact(Vector2.up), Is.False);
                animator.Update(.08f);
                Assert.That(visual.localScale.x, Is.EqualTo(1.18f).Within(.002f));
                yield return new WaitForSeconds(.7f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("死亡"), Is.True);
                foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>())
                    Assert.That(renderer.color.a, Is.EqualTo(0).Within(.001f));
                Assert.That(instance.activeSelf, Is.True, "表现驱动不负责隐藏或销毁根对象");
                Assert.That(instance.transform.position, Is.EqualTo(position));
                Assert.That(instance.transform.localScale, Is.EqualTo(scale));
                driver.ResetToIdle();
                Assert.That(driver.IsDead, Is.False);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("待机"), Is.True);
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
                Assert.That(Quaternion.Angle(visual.localRotation, Quaternion.identity), Is.LessThan(.01f));
                foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>())
                    Assert.That(renderer.color.a, Is.EqualTo(1).Within(.001f));
                driver.SetFlying(true);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("飞行"), Is.True);
                Assert.That(driver.PlayImpact(Vector2.up), Is.True);
                Assert.That(driver.PlayDeath(), Is.True);
                driver.ResetToIdle();
            }
            finally { Object.Destroy(instance); }
        }

        [UnityTest]
        public IEnumerator 碰撞区分方向自动回飞行且节流可重置()
        {
            // 独立实例避免测试场景运动在同一帧覆盖手动状态。
            GameObject instance = Object.Instantiate(_controller.BunRoot.gameObject);
            try
            {
                var driver = instance.GetComponent<BunAnimationDriver>();
                var animator = instance.GetComponent<Animator>();
                Transform visual = instance.transform.Find("Visual");
                Vector3 position = instance.transform.position;
                Vector3 scale = instance.transform.localScale;
                Assert.That(driver.PlayImpact(Vector2.right), Is.False);
                driver.SetFlying(true);
                Assert.That(driver.PlayImpact(Vector2.zero), Is.False);
                Assert.That(driver.PlayImpact(new Vector2(float.NaN, 0)), Is.False);
                Assert.That(driver.PlayImpact(Vector2.right), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("侧撞"), Is.True);
                Assert.That(driver.PlayImpact(Vector2.up), Is.False);
                animator.Update(.04f);
                Assert.That(visual.localScale.x, Is.LessThan(.9f));
                Assert.That(visual.Find("Face").GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Food_Bun_Face_Impact"));
                yield return new WaitForSeconds(.3f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("飞行"), Is.True);
                Assert.That(driver.PlayImpact(Vector2.up), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("上下撞"), Is.True);
                animator.Update(.04f);
                Assert.That(visual.localScale.y, Is.LessThan(.9f));
                driver.ResetToIdle();
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("待机"), Is.True);
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
                Assert.That(visual.Find("Face").GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Food_Bun_Face_Idle"));
                driver.SetFlying(true);
                Assert.That(driver.PlayImpact(Vector2.down), Is.True, "重置应清除冷却");
                yield return new WaitForSeconds(.3f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("飞行"), Is.True);
                Assert.That(instance.transform.position, Is.EqualTo(position));
                Assert.That(instance.transform.localScale, Is.EqualTo(scale));
            }
            finally { Object.Destroy(instance); }
        }
    }
}

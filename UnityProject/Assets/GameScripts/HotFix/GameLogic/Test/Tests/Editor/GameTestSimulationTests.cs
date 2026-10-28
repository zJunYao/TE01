using NUnit.Framework;
using UnityEngine;

namespace GameLogic.Test.Tests
{
    public sealed class GameTestSimulationTests
    {
        [Test]
        public void 初始等待发射且拒绝无效方向()
        {
            var game = new GameTestSimulation();
            Assert.That(game.Position, Is.EqualTo(GameTestSimulation.DefaultSpawn));
            Assert.That(game.EnemyAlive, Is.True);
            Assert.That(game.IsFlying, Is.False);
            Assert.That(game.Launch(Vector2.zero), Is.False);
            Assert.That(game.Launch(new Vector2(float.NaN, 1f)), Is.False);
        }

        [Test]
        public void 碰壁反弹保持速度且球心不越界()
        {
            var game = new GameTestSimulation();
            game.Launch(Vector2.right);
            game.Step(0.5f);
            Assert.That(game.BounceCount, Is.EqualTo(1));
            Assert.That(game.LastBounceNormal, Is.EqualTo(Vector2.left));
            Assert.That(game.Velocity.x, Is.LessThan(0f));
            Assert.That(game.Velocity.magnitude, Is.EqualTo(GameTestSimulation.Speed).Within(0.001f));
            Assert.That(game.Position.x, Is.InRange(game.Bounds.xMin + GameTestSimulation.BallRadius,
                game.Bounds.xMax - GameTestSimulation.BallRadius));
        }

        [Test]
        public void 直接命中消灭且停止飞行()
        {
            var game = new GameTestSimulation();
            game.Launch(game.EnemyPosition - game.Position);
            game.Step(2f);
            Assert.That(game.HasWon, Is.True);
            Assert.That(game.IsFlying, Is.False);
            Assert.That(game.Velocity, Is.EqualTo(Vector2.zero));
            Assert.That(game.Launch(Vector2.up), Is.False);
        }

        [Test]
        public void 单帧跨越怪物也不会穿透()
        {
            var game = new GameTestSimulation();
            game.Launch(game.EnemyPosition - game.Position);
            game.Step(8f);
            Assert.That(game.HasWon, Is.True);
            Assert.That(Vector2.Distance(game.Position, game.EnemyPosition),
                Is.EqualTo(GameTestSimulation.BallRadius + GameTestSimulation.EnemyRadius).Within(0.001f));
        }

        [Test]
        public void 反射路线可以消灭怪物()
        {
            var game = new GameTestSimulation();
            float wall = game.Bounds.xMax - GameTestSimulation.BallRadius;
            Vector2 mirroredEnemy = new Vector2(2f * wall - game.EnemyPosition.x, game.EnemyPosition.y);
            game.Launch(mirroredEnemy - game.Position);
            game.Step(2f);
            Assert.That(game.BounceCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(game.HasWon, Is.True);
        }

        [Test]
        public void 未命中超时回到起点可再次发射()
        {
            var game = new GameTestSimulation();
            game.Launch(Vector2.right);
            game.Step(8f);
            Assert.That(game.EnemyAlive, Is.True);
            Assert.That(game.IsFlying, Is.False);
            Assert.That(game.Position, Is.EqualTo(game.Spawn));
            Assert.That(game.Launch(Vector2.up), Is.True);
        }

        [Test]
        public void 消灭后重置恢复初始状态()
        {
            var game = new GameTestSimulation();
            game.Launch(game.EnemyPosition - game.Position);
            game.Step(2f);
            game.Reset();
            Assert.That(game.EnemyAlive, Is.True);
            Assert.That(game.Position, Is.EqualTo(game.Spawn));
            Assert.That(game.IsFlying, Is.False);
            Assert.That(game.BounceCount, Is.Zero);
            Assert.That(game.LastBounceNormal, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void 飞行中不接受重复发射且忽略无效时间()
        {
            var game = new GameTestSimulation();
            game.Launch(Vector2.right);
            Assert.That(game.Launch(Vector2.up), Is.False);
            Vector2 position = game.Position;
            game.Step(float.NaN);
            game.Step(-1f);
            Assert.That(game.Position, Is.EqualTo(position));
        }

        [Test]
        public void 角点同时反射两个方向()
        {
            var game = new GameTestSimulation(Rect.MinMaxRect(-2f, -2f, 2f, 2f), Vector2.zero, new Vector2(-1f, 1f));
            game.Launch(Vector2.one);
            game.Step(0.25f);
            Assert.That(game.Velocity.x, Is.LessThan(0f));
            Assert.That(game.Velocity.y, Is.LessThan(0f));
            Assert.That(Vector2.Distance(game.LastBounceNormal, -Vector2.one.normalized), Is.LessThan(.001f));
            Assert.That(game.BounceCount, Is.EqualTo(1));
        }

        [Test]
        public void 连续小步与大步结果一致()
        {
            var whole = new GameTestSimulation();
            var split = new GameTestSimulation();
            whole.Launch(new Vector2(1f, 0.15f));
            split.Launch(new Vector2(1f, 0.15f));
            whole.Step(3f);
            for (int i = 0; i < 300; i++) split.Step(0.01f);
            Assert.That(Vector2.Distance(whole.Position, split.Position), Is.LessThan(0.001f));
            Assert.That(whole.BounceCount, Is.EqualTo(split.BounceCount));
            Assert.That(whole.EnemyAlive, Is.EqualTo(split.EnemyAlive));
        }
    }
}

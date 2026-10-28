using System;
using UnityEngine;

namespace GameLogic.Test
{
    /// <summary>
    /// 独立弹球测试模型。使用连续碰撞检测，不修改项目的全局物理设置。
    /// </summary>
    public sealed class GameTestSimulation
    {
        public static readonly Rect DefaultBounds = Rect.MinMaxRect(-4f, -7.35f, 4f, 7.25f);
        public static readonly Vector2 DefaultSpawn = new Vector2(0f, -5.4f);
        public static readonly Vector2 DefaultEnemy = new Vector2(1.35f, 3.25f);
        public const float BallRadius = 0.42f;
        public const float EnemyRadius = 0.57f;
        public const float Speed = 11f;
        public const float ShotDuration = 8f;

        public Rect Bounds { get; }
        public Vector2 Spawn { get; }
        public Vector2 EnemyPosition { get; }
        public Vector2 Position { get; private set; }
        public Vector2 Velocity { get; private set; }
        public bool IsFlying { get; private set; }
        public bool EnemyAlive { get; private set; }
        public bool HasWon => !EnemyAlive;
        public int BounceCount { get; private set; }
        public Vector2 LastBounceNormal { get; private set; }
        private float _elapsed;

        public GameTestSimulation() : this(DefaultBounds, DefaultSpawn, DefaultEnemy) { }

        public GameTestSimulation(Rect bounds, Vector2 spawn, Vector2 enemyPosition)
        {
            if (bounds.width <= BallRadius * 2f || bounds.height <= BallRadius * 2f ||
                !ContainsBall(bounds, spawn) || !bounds.Contains(enemyPosition))
            {
                throw new ArgumentException("测试场地或角色位置无效。");
            }

            Bounds = bounds;
            Spawn = spawn;
            EnemyPosition = enemyPosition;
            Reset();
        }

        public void Reset()
        {
            EnemyAlive = true;
            BounceCount = 0;
            LastBounceNormal = Vector2.zero;
            ReturnToSpawn();
        }

        public bool Launch(Vector2 direction)
        {
            if (IsFlying || HasWon || !Finite(direction.x) || !Finite(direction.y) || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Velocity = direction.normalized * Speed;
            IsFlying = true;
            _elapsed = 0f;
            return true;
        }

        public void Step(float deltaTime)
        {
            if (!IsFlying || !Finite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            float remaining = Mathf.Min(deltaTime, ShotDuration - _elapsed);
            _elapsed += remaining;
            // 每段都寻找最近接触点，先判断怪物，再处理墙面反射，避免大步长穿透。
            while (remaining > 0.000001f && IsFlying)
            {
                float tx = WallTime(Position.x, Velocity.x, Bounds.xMin + BallRadius, Bounds.xMax - BallRadius);
                float ty = WallTime(Position.y, Velocity.y, Bounds.yMin + BallRadius, Bounds.yMax - BallRadius);
                float travel = Mathf.Min(remaining, Mathf.Min(tx, ty));
                float hit = EnemyHitTime();
                if (hit <= travel)
                {
                    Position += Velocity * hit;
                    EnemyAlive = false;
                    IsFlying = false;
                    Velocity = Vector2.zero;
                    break;
                }

                Position += Velocity * travel;
                remaining -= travel;
                bool hitX = tx <= travel + 0.000001f;
                bool hitY = ty <= travel + 0.000001f;
                if (hitX || hitY)
                {
                    LastBounceNormal = new Vector2(hitX ? -Mathf.Sign(Velocity.x) : 0f,
                        hitY ? -Mathf.Sign(Velocity.y) : 0f).normalized;
                    Velocity = new Vector2(hitX ? -Velocity.x : Velocity.x, hitY ? -Velocity.y : Velocity.y);
                    BounceCount++;
                }
            }

            if (IsFlying && _elapsed >= ShotDuration)
            {
                ReturnToSpawn();
            }
        }

        private float EnemyHitTime()
        {
            Vector2 offset = Position - EnemyPosition;
            float radius = BallRadius + EnemyRadius;
            float c = offset.sqrMagnitude - radius * radius;
            if (c <= 0f) return 0f;
            float a = Velocity.sqrMagnitude;
            float b = Vector2.Dot(offset, Velocity);
            float discriminant = b * b - a * c;
            if (b >= 0f || discriminant < 0f) return float.PositiveInfinity;
            return Mathf.Max(0f, (-b - Mathf.Sqrt(discriminant)) / a);
        }

        private void ReturnToSpawn()
        {
            Position = Spawn;
            Velocity = Vector2.zero;
            IsFlying = false;
            _elapsed = 0f;
        }

        private static float WallTime(float position, float velocity, float minimum, float maximum)
        {
            if (Mathf.Abs(velocity) < 0.000001f) return float.PositiveInfinity;
            return Mathf.Max(0f, ((velocity > 0f ? maximum : minimum) - position) / velocity);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool ContainsBall(Rect bounds, Vector2 point) =>
            point.x >= bounds.xMin + BallRadius && point.x <= bounds.xMax - BallRadius &&
            point.y >= bounds.yMin + BallRadius && point.y <= bounds.yMax - BallRadius;
    }
}

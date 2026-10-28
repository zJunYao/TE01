using UnityEngine;

namespace GameLogic.Actor
{
    /// <summary>只驱动显示动画，不修改角色根节点、碰撞或速度。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class BunAnimationDriver : MonoBehaviour
    {
        public const float ImpactDuration = 0.18f;
        public const float ImpactCooldown = 0.08f;
        public const float DeathDuration = 0.56f;
        private static readonly int Death = Animator.StringToHash("Base Layer.死亡");
        private static readonly int Idle = Animator.StringToHash("Base Layer.待机");
        private static readonly int Fly = Animator.StringToHash("Base Layer.飞行");
        private static readonly int Side = Animator.StringToHash("Base Layer.侧撞");
        private static readonly int Top = Animator.StringToHash("Base Layer.上下撞");
        private Animator _animator;
        private bool _flying;
        public bool IsDead { get; private set; }
        private float _lastImpact = float.NegativeInfinity;

        public bool IsFlying => _flying;

        private void Awake() => _animator = GetComponent<Animator>();

        public void SetFlying(bool flying)
        {
            if (IsDead || _flying == flying) return;
            _flying = flying;
            Play(flying ? Fly : Idle);
            if (!flying) _lastImpact = float.NegativeInfinity;
        }

        public bool PlayImpact(Vector2 normal)
        {
            if (IsDead || !_flying || normal.sqrMagnitude < 0.001f ||
                float.IsNaN(normal.x) || float.IsNaN(normal.y) ||
                float.IsInfinity(normal.x) || float.IsInfinity(normal.y) ||
                Time.time - _lastImpact < ImpactCooldown) return false;
            _lastImpact = Time.time;
            Play(Mathf.Abs(normal.x) >= Mathf.Abs(normal.y) ? Side : Top);
            return true;
        }

        public void ResetToIdle()
        {
            IsDead = false;
            _flying = false;
            _lastImpact = float.NegativeInfinity;
            Transform visual = transform.Find("Visual");
            if (visual != null)
            {
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
                foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    Color color = renderer.color;
                    color.a = 1f;
                    renderer.color = color;
                }
            }
            Play(Idle);
        }

        /// <summary>只播放死亡表现；运动停止和对象回收由玩法调用方负责。</summary>
        public bool PlayDeath()
        {
            if (IsDead) return false;
            IsDead = true;
            _flying = false;
            Play(Death);
            return true;
        }

        private void Play(int state)
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            _animator.enabled = true;
            _animator.Play(state, 0, 0f);
            // 同帧清除上一段动画的缩放与表情，避免重置或出击时残留。
            _animator.Update(0f);
        }
    }
}

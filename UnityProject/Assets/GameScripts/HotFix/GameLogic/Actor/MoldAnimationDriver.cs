using UnityEngine;

namespace GameLogic.Actor
{
    /// <summary>管理霉菌消散表现及完成后隐藏，不参与命中判定。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class MoldAnimationDriver : MonoBehaviour
    {
        public const float DissolveDuration = .72f;
        public const float BurstTime = .08f;
        private static readonly int Idle = Animator.StringToHash("Base Layer.待机");
        private static readonly int Dissolve = Animator.StringToHash("Base Layer.消散");
        private Animator _animator;
        private MoldBurstEffect _burst;
        private bool _hasBurst;
        public bool IsDissolving { get; private set; }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _burst = GetComponentInChildren<MoldBurstEffect>(true);
        }

        public bool PlayDissolve()
        {
            if (IsDissolving || !gameObject.activeInHierarchy) return false;
            IsDissolving = true;
            _hasBurst = false;
            Play(Dissolve);
            return true;
        }

        private void LateUpdate()
        {
            if (!IsDissolving) return;
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != Dissolve) return;
            if (!_hasBurst && state.normalizedTime * state.length >= BurstTime - .001f)
            {
                _hasBurst = true;
                EmitBurst();
            }
            // 粒子播完才隐藏；暂停时不以独立墙钟提前结束。
            if (state.normalizedTime >= 1f && (_burst == null || !_burst.IsAlive))
                gameObject.SetActive(false);
        }

        private void EmitBurst()
        {
            if (_burst == null) return;
            _burst.Play();
        }

        private void OnDisable()
        {
            if (_burst != null) _burst.Clear();
        }

        public void ResetToIdle()
        {
            IsDissolving = false;
            _hasBurst = false;
            if (_burst != null) _burst.Clear();
            gameObject.SetActive(true);
            Transform visual = transform.Find("Visual");
            visual.localPosition = new Vector3(0, -.98f, 0);
            visual.localScale = Vector3.one;
            visual.localRotation = Quaternion.identity;
            foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Color color = renderer.color;
                color.a = 1;
                renderer.color = color;
            }
            Play(Idle);
        }

        private void Play(int state)
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            _animator.enabled = true;
            _animator.Play(state, 0, 0);
            _animator.Update(0);
        }
    }
}

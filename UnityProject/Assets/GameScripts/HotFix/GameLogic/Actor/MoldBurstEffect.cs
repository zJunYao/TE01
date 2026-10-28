using UnityEngine;

namespace GameLogic.Actor
{
    /// <summary>独立霉菌碎团特效；只管理自身粒子，不销毁或隐藏所属对象。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class MoldBurstEffect : MonoBehaviour
    {
        [SerializeField, Tooltip("独立使用时启用；由角色触发的嵌套实例关闭此项。")]
        private bool _playOnEnable = true;
        private ParticleSystem _particles;
        private ParticleSystem Particles => _particles != null ? _particles : (_particles = GetComponent<ParticleSystem>());
        public bool IsAlive => Particles.IsAlive(true);

        private void OnEnable()
        {
            if (_playOnEnable) Play();
        }

        public void Play()
        {
            Clear();
            Particles.Play(false);
        }

        public void Clear() => Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        private void OnDisable() => Clear();
    }
}

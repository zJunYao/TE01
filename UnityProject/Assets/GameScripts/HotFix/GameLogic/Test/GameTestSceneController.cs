using UnityEngine;
using GameLogic.Actor;

namespace GameLogic.Test
{
    /// <summary>
    /// 仅供 Game 场景使用的测试入口，不参与正式 GameApp 和 UIWindow 生命周期。
    /// 图片由场景序列化持有，不创建框架模块，也不持有额外的资源加载句柄。
    /// </summary>
    public sealed class GameTestSceneController : MonoBehaviour
    {
        [SerializeField] private Camera _gameCamera;
        [SerializeField] private SpriteRenderer _bun;
        [SerializeField] private SpriteRenderer _enemy;
        [SerializeField] private LineRenderer _aimLine;

        public GameTestSimulation Simulation { get; private set; }
        public Camera GameCamera => _gameCamera;
        public SpriteRenderer Bun => _bun;
        public SpriteRenderer Enemy => _enemy;
        public LineRenderer AimLine => _aimLine;
        public Transform BunRoot => _bunRoot != null ? _bunRoot : _bun.transform;
        public Transform EnemyRoot => _enemyRoot != null ? _enemyRoot : _enemy.transform;

        private bool _aiming;
        private int _fingerId = -1;
        private Vector2 _aimDirection;
        private Font _debugFont;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private Transform _bunRoot;
        private BunAnimationDriver _bunAnimation;
        private Transform _enemyRoot;
        private MoldAnimationDriver _enemyAnimation;

        private void Awake()
        {
            if (_gameCamera == null || _bun == null || _enemy == null || _aimLine == null)
            {
                Debug.LogError("Game 测试场景引用不完整，请重新检查场景搭建结果。", this);
                enabled = false;
                return;
            }

            _bunAnimation = _bun.GetComponentInParent<BunAnimationDriver>();
            _bunRoot = _bunAnimation != null ? _bunAnimation.transform : _bun.transform;
            var enemyAnimator = _enemy.GetComponentInParent<Animator>();
            _enemyRoot = enemyAnimator != null ? enemyAnimator.transform : _enemy.transform;
            _enemyAnimation = _enemyRoot.GetComponent<MoldAnimationDriver>();
            Simulation = new GameTestSimulation();
            _aimLine.sharedMaterial = _bun.sharedMaterial;
            ResetRound();
            FitCamera();
        }

        private void Update()
        {
            FitCamera();
            if (Input.GetKeyDown(KeyCode.R)) ResetRound();
            ReadPointer();
            int previousBounces = Simulation.BounceCount;
            Simulation.Step(Time.deltaTime);
            RefreshActors();
            if (Simulation.BounceCount > previousBounces && Simulation.IsFlying && _bunAnimation != null)
                _bunAnimation.PlayImpact(Simulation.LastBounceNormal);
        }

        private void OnDisable()
        {
            CancelAim();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelAim();
        }

        public bool Launch(Vector2 direction)
        {
            CancelAim();
            bool launched = Simulation != null && Simulation.Launch(direction);
            if (launched && _bunAnimation != null) _bunAnimation.SetFlying(true);
            return launched;
        }

        public void ResetRound()
        {
            CancelAim();
            Simulation?.Reset();
            if (_bunAnimation != null) _bunAnimation.ResetToIdle();
            if (_enemyAnimation != null) _enemyAnimation.ResetToIdle();
            RefreshActors();
        }

        private void FitCamera()
        {
            // 竖屏完整显示木盘；横屏保留左右留边，不拉伸底图。
            _gameCamera.orthographicSize = Mathf.Max(8.5f, 4.8f / Mathf.Max(_gameCamera.aspect, 0.01f));
        }

        private void RefreshActors()
        {
            if (Simulation == null) return;
            _bunRoot.position = new Vector3(Simulation.Position.x, Simulation.Position.y, 0f);
            if (_bunAnimation != null) _bunAnimation.SetFlying(Simulation.IsFlying);
            if (_enemyAnimation != null)
            {
                if (!Simulation.EnemyAlive) _enemyAnimation.PlayDissolve();
            }
            else EnemyRoot.gameObject.SetActive(Simulation.EnemyAlive);
        }

        private void ReadPointer()
        {
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began && _fingerId == -1)
                    {
                        BeginAim(touch.position);
                        if (_aiming) _fingerId = touch.fingerId;
                    }
                    if (touch.fingerId != _fingerId) continue;
                    UpdateAim(touch.position);
                    if (touch.phase == TouchPhase.Ended) EndAim();
                    else if (touch.phase == TouchPhase.Canceled) CancelAim();
                }
                return;
            }

            if (_fingerId != -1)
            {
                CancelAim();
                return;
            }
            if (Input.GetMouseButtonDown(0)) BeginAim(Input.mousePosition);
            if (_aiming) UpdateAim(Input.mousePosition);
            if (Input.GetMouseButtonUp(0)) EndAim();
        }

        private void BeginAim(Vector2 screenPosition)
        {
            if (Simulation.IsFlying || Simulation.HasWon) return;
            Vector2 world = ScreenToWorld(screenPosition);
            if (Vector2.Distance(world, Simulation.Position) > 0.95f) return;
            _aiming = true;
            _aimDirection = Vector2.zero;
        }

        private void UpdateAim(Vector2 screenPosition)
        {
            if (!_aiming) return;
            _aimDirection = Simulation.Position - ScreenToWorld(screenPosition);
            _aimLine.enabled = _aimDirection.magnitude >= 0.2f;
            _aimLine.SetPosition(0, BunRoot.position);
            Vector2 end = Simulation.Position + _aimDirection.normalized * Mathf.Min(_aimDirection.magnitude * 1.5f, 3f);
            _aimLine.SetPosition(1, new Vector3(end.x, end.y, 0f));
        }

        private void EndAim()
        {
            if (!_aiming) return;
            Vector2 direction = _aimDirection;
            bool valid = direction.magnitude >= 0.2f;
            CancelAim();
            if (valid) Launch(direction);
        }

        private void CancelAim()
        {
            _aiming = false;
            _fingerId = -1;
            if (_aimLine != null) _aimLine.enabled = false;
        }

        private Vector2 ScreenToWorld(Vector2 point) =>
            _gameCamera.ScreenToWorldPoint(new Vector3(point.x, point.y, -_gameCamera.transform.position.z));

        private void OnGUI()
        {
            if (Simulation == null) return;
            // 仅测试用即时调试面板；正式业务 UI 应使用框架 UIWindow 和 AddUIEvent。
            if (_labelStyle == null)
            {
                _debugFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 28);
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    font = _debugFont,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                _buttonStyle = new GUIStyle(GUI.skin.button) { font = _debugFont };
            }

            Vector3 left = _gameCamera.WorldToScreenPoint(new Vector3(-4f, 0f, 0f));
            Vector3 right = _gameCamera.WorldToScreenPoint(new Vector3(4f, 0f, 0f));
            float width = right.x - left.x;
            float unit = width / 720f;
            _labelStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(25f * unit));
            _labelStyle.normal.textColor = new Color(0.29f, 0.13f, 0.05f);
            _buttonStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(25f * unit));
            float top = Screen.height - _gameCamera.WorldToScreenPoint(new Vector3(0f, 6.8f, 0f)).y;
            string status = Simulation.HasWon ? "消灭成功！点击重置再玩一次" :
                Simulation.IsFlying ? $"反弹中 · 已反弹 {Simulation.BounceCount} 次" : "按住包子向后拖动，松开发射";
            GUI.Label(new Rect(left.x, top, width, 90f * unit), status, _labelStyle);
            float bottom = Screen.height - _gameCamera.WorldToScreenPoint(new Vector3(0f, -6.5f, 0f)).y;
            if (GUI.Button(new Rect(left.x + width * 0.3f, bottom, width * 0.4f, 65f * unit), "重置 / R", _buttonStyle))
            {
                ResetRound();
            }
        }

        private void OnDestroy()
        {
            if (_debugFont != null) Destroy(_debugFont);
        }

        private void OnDrawGizmosSelected()
        {
            Rect bounds = GameTestSimulation.DefaultBounds;
            Gizmos.color = new Color(0.2f, 0.9f, 0.5f);
            Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.width, bounds.height, 0f));
        }
    }
}

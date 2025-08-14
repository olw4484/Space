// ================================
// Project : SpaceWorker
// Script  : OrbitRing.cs
// Desc    : (TODO: 스크립트 한 줄 설명)
// Author  : (TODO: 이름/이니셜)
// Note    : 팀 컨벤션 준수 - 필드/메서드 구간 주석, 한글 주석
// ================================

using UnityEngine;

namespace SpaceWorker
{
    /// <summary>
    /// OrbitRing
    /// - (TODO: 핵심 역할을 2~3줄로 서술)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game/OrbitRing")]
    [RequireComponent(typeof(LineRenderer))]
    public partial class OrbitRing : MonoBehaviour
    {
        // =====================================
        // # Fields (Serialized / Private)
        // =====================================
        [Header("설정")] [SerializeField]
        private bool _enableLog = true;

        [Header("참조")] [SerializeField]
        private Transform _pivot;
        public float radius = 10f;
        public int segments = 256;

        // =====================================
        // # Unity Messages
        // =====================================
        private void Awake()  { Log("[Awake] 초기화 시작"); }

        private void Start()
        {
            var lr = GetComponent<LineRenderer>();
            lr.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float t = (i / (float)segments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, 0, Mathf.Sin(t) * radius));
            }
        }

        private void OnEnable()  { }
        private void OnDisable() { }
        private void Update()    { }
        private void OnDestroy() { }

        // =====================================
        // # Private Methods
        // =====================================
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void Log(object msg)
        {
            if (_enableLog) Debug.Log($"[OrbitRing] {msg}", this);
        }
    }
}
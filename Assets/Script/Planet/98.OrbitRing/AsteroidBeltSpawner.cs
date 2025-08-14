// ================================
// Project : SpaceWorker
// Script  : AsteroidBeltSpawner.cs
// Desc    : 운석 생성기
// Author  : KMS
// Note    : 팀 컨벤션 준수 - 필드/메서드 구간 주석, 한글 주석
// ================================

using System.Collections.Generic;
using UnityEngine;

namespace SpaceWorker
{
    /// <summary>
    /// AsteroidBeltSpawner
    /// - 지정된 반경과 범위 내에 다수의 운석을 생성하여 띠 형태로 배치
    /// - 운석의 위치, 크기, 회전을 랜덤으로 설정하고, 반경에 비례한 각속도로 공전 시킴
    /// - Mesh Instancing을 사용해 대량의 운석을 효율적으로 렌더링
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game/AsteroidBeltSpawner")]
    public partial class AsteroidBeltSpawner : MonoBehaviour
    {
        // =====================================
        // # Fields (Serialized / Private)
        // =====================================
        [Header("설정")] [SerializeField]
        private bool _enableLog = true;

        [Header("참조")] [SerializeField]
        private Transform _pivot;
        public Mesh asteroidMesh;
        public Material asteroidMat;
        public int count = 5000;
        public float innerRadius = 22000f;
        public float outerRadius = 24000f;
        public float beltInclination = 2f; // 띠 기울기(도)
        public Vector2 scaleRange = new(.2f, 1.8f);
        public float baseAngularDegPerSec = 0.05f; // r에 따라 가감

        List<Matrix4x4> matrices = new();
        List<float> angVel = new();

        // =====================================
        // # Unity Messages
        // =====================================
        private void Awake()  { Log("[Awake] 초기화 시작"); }
        private void Start()
        {
            var tilt = Quaternion.Euler(beltInclination, 0, 0);
            var center = transform.position;
            var rnd = new System.Random();

            for (int i = 0; i < count; i++)
            {
                float r = Mathf.Lerp(innerRadius, outerRadius, Random.value);
                float ang = Random.value * Mathf.PI * 2f;
                float y = (Random.value - .5f) * (outerRadius - innerRadius) * 0.01f; // 소량의 두께
                Vector3 posLocal = new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r);
                Vector3 posWorld = center + tilt * posLocal;

                float s = Random.Range(scaleRange.x, scaleRange.y);
                Quaternion rot = Random.rotation;

                matrices.Add(Matrix4x4.TRS(posWorld, rot, Vector3.one * s));

                // 단순 근사: 각속도 ~ 1/sqrt(r)
                angVel.Add(baseAngularDegPerSec * (1f / Mathf.Sqrt(r)));
            }
        }
        private void OnEnable()  { }
        private void OnDisable() { }

        private void Update()
        {
            // 공전 업데이트(간단히 Y축 회전)
            for (int i = 0; i < matrices.Count; i++)
            {
                var m = matrices[i];
                Vector3 p = m.GetColumn(3);
                var center = transform.position;
                var dir = (p - center);
                float r = dir.magnitude;
                Quaternion rot = Quaternion.AngleAxis(angVel[i] * Time.deltaTime, Vector3.up);
                Vector3 newPos = center + rot * dir;

                // 회전만 반영(방향 무관)
                m.SetColumn(3, new Vector4(newPos.x, newPos.y, newPos.z, 1));
                matrices[i] = m;
            }

            // 배치 렌더 (한 번에 1023개 제한 → 배치 분할)
            for (int i = 0; i < matrices.Count; i += 1023)
            {
                int batch = Mathf.Min(1023, matrices.Count - i);
                Graphics.DrawMeshInstanced(asteroidMesh, 0, asteroidMat, matrices.GetRange(i, batch));
            }
        }

        private void OnDestroy() { }

        // =====================================
        // # Private Methods
        // =====================================
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void Log(object msg)
        {
            if (_enableLog) Debug.Log($"[AsteroidBeltSpawner] {msg}", this);
        }
    }
}
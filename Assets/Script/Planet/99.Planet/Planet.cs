// ================================
// Project : SpaceWorker
// Script  : Planet.cs
// Desc    : 행성의 회전을 담당하는 코드
// Author  : KMS
// Note    : 팀 컨벤션 준수 - 필드/메서드 구간 주석, 한글 주석
// ================================

using UnityEngine;

namespace SpaceWorker
{
    /// <summary>
    /// Planet
    /// - <see cref="PlanetData"/>의 천문/렌더링 파라미터를 바탕으로
    ///   행성의 공전(orbit)과 자전(rotation) 상태를 구성/갱신한다.
    /// - 실행 시 피벗 GameObject(공전 중심)를 동적으로 생성하고,
    ///   메쉬/머티리얼을 자동 세팅한다.
    /// </summary>
    /// <remarks>
    /// [의존성]
    /// - <see cref="data"/>: 궤도 반지름·공전/자전 주기·자전축 기울기·머티리얼·링 프리팹 등.
    ///   값이 비어 있으면 초기화가 실패하거나 기본값이 적용될 수 있다.
    /// 
    /// [생성/부작용]
    /// - Awake에서 "<planetName>_Pivot" 트랜스폼을 생성하고, 해당 피벗 하위로 자신을 재배치한다.
    /// - Sphere 기본 메쉬/렌더러를 동적으로 추가/세팅한다(링 프리팹 포함 가능).
    /// 
    /// [업데이트 정책]
    /// - 공전은 월드 Y축, 자전은 로컬 Y축 기준으로 프레임마다 갱신된다.
    /// - 주기 단위는 "초=일(day)"의 단순화 스케일을 사용하며, 필요 시 외부에서 조정 가능.
    /// 
    /// [주의/보장]
    /// - 런타임 중에는 pivot/메쉬가 동적으로 생성되므로, 파괴 시 리소스 정리가 필요할 수 있다.
    /// - Editor 테스트 유틸은 partial(.Editor.cs)로 분리되어 빌드에 포함되지 않는다.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game/Planet")]
    public partial class Planet : MonoBehaviour
    {
        // =====================================
        // # Fields (Serialized / Private)
        // =====================================
        [Header("설정")]
        [SerializeField]
        private bool _enableLog = true;
        [Header("참조")]
        [SerializeField]
        private float _orbitDegPerSec;
        private float _rotationDegPerSec;
        public PlanetData data;
        Transform pivot; // 공전 중심의 더미(원점 이동 시 함께 이동)

        // =====================================
        // # Unity Messages
        // =====================================

        /// <summary>피벗/메쉬/머티리얼 등 런타임 의존 리소스를 구성한다.</summary>
        private void Awake()
        {

            pivot = new GameObject($"{data.planetName}_Pivot").transform;
            pivot.parent = data.orbitCenter != null ? data.orbitCenter : null;
            pivot.position = data.orbitCenter ? data.orbitCenter.position : Vector3.zero;
            transform.SetParent(pivot);
            transform.localPosition = new Vector3(data.orbitRadiusU, 0, 0);

            transform.localRotation = Quaternion.Euler(data.axialTiltDeg, 0, 0);
            var mf = gameObject.AddComponent<MeshFilter>();
            var mr = gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            mr.sharedMaterial = data.material;
            transform.localScale = Vector3.one * (data.radiusU * 2f); // 지름 스케일

            if (data.ringPrefab) Instantiate(data.ringPrefab, transform, false);


            Log("[Awake] 초기화 시작");
        }

        /// <summary>초당 각속도를 계산해 1프레임 분 공전/자전을 적용한다.</summary>
        private void Start()
        {
            float dayPerSec = 1f;
            _orbitDegPerSec = 360f / (data.orbitPeriodDays * (1f / dayPerSec));
            _rotationDegPerSec = 360f / ((data.rotationPeriodHours / 24f) * (1f / dayPerSec));
        }
        private void OnEnable() { }
        private void OnDisable() { }
        /// <summary>
        /// 매 프레임 행성의 공전/자전 상태를 업데이트한다.
        /// </summary>
        /// <remarks>
        /// - Start()에서 계산된 초당 회전 각도(_orbitDegPerSec, _rotationDegPerSec)를 기반으로 함.
        /// - 공전은 월드 좌표계 Y축, 자전은 로컬 좌표계 Y축 기준.
        /// - pivot이 null이면 공전은 생략.
        /// </remarks>
        private void Update()
        {
            if (pivot != null)
                pivot.Rotate(Vector3.up, _orbitDegPerSec * Time.deltaTime, Space.World);

            transform.Rotate(Vector3.up, _rotationDegPerSec * Time.deltaTime, Space.Self);
        }
        private void OnDestroy() { }

        // =====================================
        // # Private Methods
        // =====================================
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void Log(object msg)
        {
            if (_enableLog) Debug.Log($"[Planet] {msg}", this);
        }
    }
}
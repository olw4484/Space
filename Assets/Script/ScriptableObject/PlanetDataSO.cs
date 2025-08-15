// ================================
// Project : SpaceWorker
// Script  : NewScriptableObject.cs
// Desc    : (TODO: 스크립트 한 줄 설명)
// Author  : (TODO: 이름/이니셜)
// Note    : 팀 컨벤션 준수 - 필드/메서드 구간 주석, 한글 주석
// ================================

using UnityEngine;

namespace SpaceWorker
{
    [CreateAssetMenu(menuName = "Game/SO/NewScriptableObject", fileName = "SO_NewScriptableObject")]
    public class PlanetData : ScriptableObject
    {
        // =====================================
        // # Fields
        // =====================================

        [Header("Meta")]
        [SerializeField] private string _id;           // 유니크 ID(직접 부여 or GUID)
        [SerializeField] private string _displayName;  // 에셋 표시 이름
        [TextArea] [SerializeField] private string _description;

        [Header("Data")]
        public string planetName;
        public float radiusU;              // 행성 반지름(유닛)
        public float orbitRadiusU;         // 공전 반경(유닛, 축소 후)
        public float orbitPeriodDays;      // 공전 주기(일)
        public float rotationPeriodHours;  // 자전 주기(시간)
        public float axialTiltDeg;         // 자전축 기울기
        public Transform orbitCenter;      // 보통 Sun.transform
        public Material material;          // 행성 머티리얼
        public GameObject ringPrefab;      // 토성용(없으면 null)
        // =====================================
        // # Properties
        // =====================================

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;

        // =====================================
        // # Editor Utilities
        // =====================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 필수값 빠짐 방지 & 기본값 보정
            if (string.IsNullOrWhiteSpace(_displayName))
                _displayName = name.Replace("SO_", "");

            if (string.IsNullOrWhiteSpace(_id))
                _id = GUIDIfEmpty(_id);
        }

        private string GUIDIfEmpty(string current)
        {
            if (!string.IsNullOrWhiteSpace(current)) return current;
            return System.Guid.NewGuid().ToString("N");
        }
#endif
    }
}
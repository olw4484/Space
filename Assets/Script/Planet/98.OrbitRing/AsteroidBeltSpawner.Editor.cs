#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace SpaceWorker
{
    /// <summary>
    /// AsteroidBeltSpawner의 에디터 전용 유틸/테스트 코드.
    /// - 빌드에 포함되지 않음(#if UNITY_EDITOR)
    /// - 원본 partial 클래스와 합쳐져 동작
    /// </summary>
    public partial class AsteroidBeltSpawner
    {
        // ─────────────────────────────────────
        // ContextMenu: 인스펙터 우클릭 메뉴에서 실행
        // ─────────────────────────────────────
        [ContextMenu("TEST/Print State")]
        private void __Test_PrintState()
        {
            if (!EditorTestConfig.Enabled) return;
            Debug.Log($"[TEST] AsteroidBeltSpawner on '{name}'", this);
        }

        [ContextMenu("TEST/Ping Pivot")]
        private void __Test_PingPivot()
        {
            if (!EditorTestConfig.Enabled) return;
            if (_pivot != null)
            {
                EditorGUIUtility.PingObject(_pivot);
                Debug.Log("[TEST] Pivot ping!", _pivot);
            }
            else
            {
                Debug.LogWarning("[TEST] Pivot is null", this);
            }
        }

        // (선택) 에디터 보조 메시지
        private void OnValidate()
        {
            if (!EditorTestConfig.Enabled) return;
            // 인스펙터 값 변경 시 호출
        }

        private void OnDrawGizmosSelected()
        {
            if (!EditorTestConfig.Enabled) return;
            if (_pivot != null) Gizmos.DrawWireSphere(_pivot.position, 0.2f);
        }
    }
}
#endif
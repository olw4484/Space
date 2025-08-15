#if UNITY_EDITOR
namespace SpaceWorker
{
    /// <summary>
    /// 에디터 전용 테스트 코드들의 전역 On/Off 스위치.
    /// </summary>
    public static class EditorTestConfig
    {
        /// <summary>true면 에디터 테스트 코드 실행.</summary>
        public static bool Enabled = true;
    }
}
#endif

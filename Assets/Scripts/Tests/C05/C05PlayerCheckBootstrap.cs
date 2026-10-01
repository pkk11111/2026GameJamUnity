// 职责：仅专用C05检查包自动执行真实地图检查并以退出码/日志报告；普通包不编译此类型。
// 维护：controller；依赖NewMapCheckLauncher与真实地图。没有任何生产场景序列化引用。
// 规范：根AGENTS.md；交接：docs/handoffs/controller.handoff。
#if C05_PLAYER_CHECKS && !UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

namespace Regrowth.Tests.C05
{
    public sealed class C05PlayerCheckBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartVerification()
        {
            new GameObject("C05 Verification Only").AddComponent<C05PlayerCheckBootstrap>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null;
            yield return null;
            NewMapPlayChecks driver = null;
            try
            {
                driver = NewMapCheckLauncher.StartChecks();
            }
            catch (Exception error)
            {
                Debug.LogError("C05_PLAYER_RESULT startup failure: " + error);
                Application.Quit(2);
                yield break;
            }
            float deadline = Time.realtimeSinceStartup + 90f;
            while (!driver.Finished && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            foreach (string result in driver.Results)
            {
                Debug.Log("C05_PLAYER_CHECK " + result);
            }
            Debug.Log("C05_PLAYER_RESULT finished=" + driver.Finished
                + " passed=" + driver.Passed + " failed=" + driver.Failed);
            Application.Quit(driver.Finished && driver.Failed == 0 && driver.Passed > 0 ? 0 : 1);
        }
    }
}
#endif

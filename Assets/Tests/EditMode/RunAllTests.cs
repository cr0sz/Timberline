using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Runs the EditMode suite from a menu item and prints one [TESTS] summary line.
//
// This exists because the suite otherwise can only be driven from the Test Runner
// window, which an automation session has no way to click. It lives in the test
// assembly rather than Survival.Editor because that is the one that already
// references UnityEditor.TestRunner.
//
// Menu: Tools/Survival/Run EditMode Tests.
public static class RunAllTests
{
    class Callbacks : ICallbacks
    {
        readonly StringBuilder failures = new StringBuilder();

        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;                 // suite nodes, not cases
            if (result.TestStatus == TestStatus.Passed) return;
            failures.AppendLine($"  {result.TestStatus}  {result.FullName}\n      {result.Message}");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            string body = failures.Length == 0 ? "  all green" : failures.ToString();
            string line = $"[TESTS] passed={result.PassCount} failed={result.FailCount} " +
                          $"skipped={result.SkipCount} inconclusive={result.InconclusiveCount}\n{body}";
            if (result.FailCount > 0) Debug.LogError(line); else Debug.Log(line);
        }
    }

    [MenuItem("Tools/Survival/Run EditMode Tests")]
    public static void Run()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
    }
}


using System;
using System.Threading.Tasks;
using UnityEngine;

namespace XPlan.ILWeave.Demo
{
    public class DemoComponent : MonoBehaviour
    {
        private async void Start()
        {
            Debug.Log("DemoComponent.Start()");
            Debug.Log($"AddSlow(3, 9) = {AddSlow(3, 9)}");

            try { MightThrow(true); }
            catch (Exception ex) { Debug.Log($"Caught: {ex.Message}"); }

            await DoAsyncTask();
        }

        [LogExecution]
        public int AddSlow(int a, int b)
        {
            // 模擬耗時
            var t = DateTime.UtcNow.AddMilliseconds(120);
            while (DateTime.UtcNow < t) { }
            return a + b;
        }

        [LogExecution]
        public void MightThrow(bool shouldThrow)
        {
            if (shouldThrow)
                throw new InvalidOperationException("Boom!");
        }

        [LogExecution]
        private async Task DoAsyncTask()
        {
            await Task.Delay(50);
            Debug.Log("Async work done.");
        }
    }
}

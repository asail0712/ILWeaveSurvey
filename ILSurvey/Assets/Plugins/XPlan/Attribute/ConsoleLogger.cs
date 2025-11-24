// Assets/WeavingDemo/WeaveTarget/ConsoleLogger.cs
using UnityEngine;

namespace WeaveTarget
{
    public class ConsoleLogger
    {
        public void Before(string methodName)
        {
            Debug.Log($"[Before] {methodName}");
        }

        public void After(string methodName, long elapsedMs)
        {
            Debug.Log($"[After]  {methodName} 花費 {elapsedMs} ms");
        }
    }
}

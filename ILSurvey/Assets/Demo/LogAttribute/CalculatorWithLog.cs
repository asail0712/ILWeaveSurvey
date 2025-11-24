using UnityEngine;

namespace WeaveTarget
{
    public class CalculatorWithLog : MonoBehaviour
    {
        void Awake()
        {
            int r = Add(3, 4);
        }

        [LogAspect(typeof(ConsoleLogger))]
        public int Add(int a, int b)
        {
            // 這裡寫原本的業務邏輯
            return a + b;
        }
    }
}
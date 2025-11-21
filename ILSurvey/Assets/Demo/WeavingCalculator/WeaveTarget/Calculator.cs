// Assets/WeavingDemo/WeaveTarget/Calculator.cs
using UnityEngine;

namespace WeaveTarget
{
    public class Calculator
    {
        // 原始：回傳 (a + b)
        public int Add(int a, int b)
        {
            Debug.Log($"[Before Weave] Add({a}, {b}) called.");
            return a + b;
        }
    }
}

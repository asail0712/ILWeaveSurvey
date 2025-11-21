// Assets/WeavingDemo/Runner/WeaveRunner.cs
using UnityEngine;
using WeaveTarget;

public class WeaveRunner : MonoBehaviour
{
    void Start()
    {
        var asm = typeof(Calculator).Assembly;
        Debug.Log($"[Runner] Calculator assembly = {asm.FullName}");
        Debug.Log($"[Runner] Calculator location = {asm.Location}");

        var calc = new Calculator();
        int result = calc.Add(3, 4);
        Debug.Log($"[Runner] Calculator.Add(3,4) => {result} (expect 70 if weaved)");
    }
}
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;

namespace XPlan.Editors.Weaver
{
    internal static class CecilHelper
    {
        /// <summary>
        /// 把 targetMethod 的 body / 參數 / 泛型 / locals / EH
        /// 通通複製到一個新的方法，並回傳這個「原始邏輯方法」。
        /// （不會自動 Add 到 DeclaringType.Methods，呼叫端自己決定何時加）
        /// </summary>
        public static MethodDefinition CloneAsOriginalMethod(MethodDefinition targetMethod, string suffix = "__Weaved")
        {
            var declaringType   = targetMethod.DeclaringType;
            var userMethodName  = targetMethod.Name;

            // 建立新的方法：Add__Weaved
            var originalMethod = new MethodDefinition(
                userMethodName + suffix,
                targetMethod.Attributes,
                targetMethod.ReturnType
            );

            // 1) 複製參數
            foreach (var p in targetMethod.Parameters)
            {
                originalMethod.Parameters.Add(
                    new ParameterDefinition(p.Name, p.Attributes, p.ParameterType));
            }

            // 2) 複製 generic 參數
            foreach (var gp in targetMethod.GenericParameters)
            {
                var newGp = new GenericParameter(gp.Name, originalMethod);
                originalMethod.GenericParameters.Add(newGp);
            }

            // 3) 複製 body (IL、locals、EH)
            var oldBody         = targetMethod.Body;
            originalMethod.Body = new MethodBody(originalMethod)
            {
                InitLocals = oldBody.InitLocals
            };

            // locals
            foreach (var v in oldBody.Variables)
                originalMethod.Body.Variables.Add(new VariableDefinition(v.VariableType));

            // IL 指令
            var ilOrig = originalMethod.Body.GetILProcessor();
            foreach (var instr in oldBody.Instructions)
                ilOrig.Append(instr);

            // ExceptionHandlers
            foreach (var eh in oldBody.ExceptionHandlers)
                originalMethod.Body.ExceptionHandlers.Add(eh);

            return originalMethod;
        }

        /// <summary>
        /// 從指定 type 一路往上找，回傳第一個符合 predicate 的 MethodDefinition。
        /// 找不到就回傳 null。
        /// </summary>
        public static MethodDefinition FindMethodInHierarchy(
            TypeDefinition typeDef,
            Func<MethodDefinition, bool> predicate)
        {
            while (typeDef != null)
            {
                var method = typeDef.Methods.FirstOrDefault(predicate);
                if (method != null)
                    return method;

                typeDef = typeDef.BaseType?.Resolve();
            }

            return null;
        }

        /// <summary>
        /// 從指定 type 一路往上找，判斷該type是否為MonoBehaviour的子類
        /// 找不到就回傳 null。
        /// </summary>
        public static bool IsMonoBehaviourSubclass(TypeDefinition type)
        {
            if (type == null) return false;

            TypeDefinition cur = type;

            // 防止意外循環，設個上限
            for (int i = 0; i < 16 && cur != null; i++)
            {
                // 自己就是 MonoBehaviour
                if (cur.FullName == "UnityEngine.MonoBehaviour")
                    return true;

                var baseRef = cur.BaseType;
                if (baseRef == null)
                    break;

                // 先用 FullName 判一次，不用 Resolve 也能抓到直接繼承的情況
                if (baseRef.FullName == "UnityEngine.MonoBehaviour")
                    return true;

                try
                {
                    // 再往上爬
                    cur = baseRef.Resolve();
                }
                catch
                {
                    break;  // 然後最後 return false;
                }
            }

            return false;
        }
    }
}

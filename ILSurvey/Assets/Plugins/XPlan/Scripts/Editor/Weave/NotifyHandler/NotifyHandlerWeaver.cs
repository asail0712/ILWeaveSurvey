using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;

using UnityEngine;

using static XPlan.Editors.Weaver.CecilWeaver;

namespace XPlan.Editors.Weaver
{
    /// <summary>
    /// [NotifyHandler(typeof(TMsg))] 的 IL Weaving 實作：
    /// 會在宣告該方法的型別建構子中，自動插入：
    ///   RegisterNotify<TMsg>(msg => Handler(msg));
    /// </summary>
    internal sealed class NotifyHandlerWeaver : IMethodAspectWeaver
    {
        // 要跟 Attribute 的 FullName 對得起來
        public string AttributeFullName => "XPlan.NotifyHandlerAttribute";

        public void Apply(ModuleDefinition module, MethodDefinition targetMethod, CustomAttribute attr)
        {
            // targetMethod 就是貼了 [NotifyHandler] 的那個方法，例如：
            // private void ShowError(LoginErrorMsg msg)

            if (attr.ConstructorArguments.Count != 1)
                throw new InvalidOperationException("[NotifyHandler] 構造參數錯誤，缺少 messageType");

            var declaringType   = targetMethod.DeclaringType;

            // 1) 從 Attribute 取得訊息型別：typeof(LoginErrorMsg)
            var msgTypeRef      = module.ImportReference((TypeReference)attr.ConstructorArguments[0].Value);

            // 2) 找到宣告型別（或其 base type）上的 RegisterNotify<T>(Action<T>) 定義
            var registerNotifyDef = CecilHelper.FindMethodInHierarchy(
                                    declaringType,
                                    m =>
                                        m.Name == "RegisterNotify" &&
                                        m.HasGenericParameters &&
                                        m.GenericParameters.Count == 1 &&
                                        m.Parameters.Count == 1 &&
                                        m.Parameters[0].ParameterType.Namespace == "System" &&
                                        m.Parameters[0].ParameterType.Name.StartsWith("Action`1")
                                    );

            if (registerNotifyDef == null)
            {
                Debug.LogWarning($"[Weaver] 無法在 {declaringType.FullName} 或其基底類別上找到 " +
                                 "RegisterNotify<T>(Action<T>)，略過 NotifyHandler 注入。");
                return;
            }

            // 3) 準備 Action<TMsg> 的 .ctor 參考
            var actionOpenType  = module.ImportReference(typeof(Action<>));             // System.Action`1
            var actionGeneric   = new GenericInstanceType(actionOpenType);              // Action<...>
            actionGeneric.GenericArguments.Add(msgTypeRef);

            var actionCtorDef   = actionOpenType.Resolve()
                .Methods.First(m => m.IsConstructor && m.Parameters.Count == 2);

            // 建一個「關閉後的」Action<TMsg>.ctor method reference
            var actionCtorRef = new MethodReference(".ctor", module.TypeSystem.Void, actionGeneric)
            {
                HasThis             = true,
                ExplicitThis        = false,
                CallingConvention   = actionCtorDef.CallingConvention
            };
            foreach (var p in actionCtorDef.Parameters)
            {
                actionCtorRef.Parameters.Add(new ParameterDefinition(p.ParameterType));
            }

            // 4) 準備 RegisterNotify<TMsg> 的 generic instance method
            var registerNotifyRef       = module.ImportReference(registerNotifyDef);
            var registerNotifyGeneric   = new GenericInstanceMethod(registerNotifyRef);
            registerNotifyGeneric.GenericArguments.Add(msgTypeRef);

            // 5) 對所有「實例建構子」插入：
            // this.RegisterNotify<TMsg>(new Action<TMsg>(this, &Handler));
            foreach (var ctor in declaringType.Methods.Where(m => m.IsConstructor && !m.IsStatic))
            {
                if (!ctor.HasBody)
                    continue;

                var il  = ctor.Body.GetILProcessor();
                var ret = ctor.Body.Instructions.Last(i => i.OpCode == OpCodes.Ret);

                // this
                il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
                // delegate target: this
                il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
                // &Method
                il.InsertBefore(ret, il.Create(OpCodes.Ldftn, targetMethod));
                // new Action<TMsg>(this, &Method)
                il.InsertBefore(ret, il.Create(OpCodes.Newobj, actionCtorRef));
                // this.RegisterNotify<TMsg>(...)
                il.InsertBefore(ret, il.Create(OpCodes.Call, registerNotifyGeneric));
            }

            Debug.Log($"[NotifyHandlerWeaver] NotifyHandler 注入完成：{declaringType.FullName}.{targetMethod.Name}");
        }
    }
}

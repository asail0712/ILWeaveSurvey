#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace XPlan.ILWeave
{
    /// <summary>
    /// Unity 內部 IL Weaving：
    /// 1) Tools/IL Weave/Weave Editor DLL：修改 Library/ScriptAssemblies/Assembly-CSharp.dll（Editor/Play 模式可測）
    /// 2) Build：自動針對 Player 修改（Mono：Build 後；IL2CPP：Build 前先改 ScriptAssemblies，讓 il2cpp 吃到已織入的 IL）
    /// </summary>
    public sealed class UnityILWeaver : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        // ★★★ 你要織入的 Attribute 完整名稱（Namespace + Type）
        private const string kLogAttributeFullName = "XPlan.ILWeave.LogExecutionAttribute";

        // 重要：確保 Editor 可用
        public int callbackOrder => 0;

        #region 手動織入（Editor 下）
        [MenuItem("Tools/IL Weave/Weave Editor DLL")]
        private static void WeaveEditorDLL()
        {
            var dllPath         = Path.Combine(EditorApplication.applicationContentsPath, "../");
            // Editor/Play 時的腳本輸出路徑
            var scriptAsmDir    = Path.GetFullPath("Library/ScriptAssemblies");
            var asmCSharp       = Path.Combine(scriptAsmDir, "Assembly-CSharp.dll");
            var asmFirstpass    = Path.Combine(scriptAsmDir, "Assembly-CSharp-firstpass.dll");

            int total = 0;
            if (File.Exists(asmCSharp)) total += WeaveOne(asmCSharp);
            if (File.Exists(asmFirstpass)) total += WeaveOne(asmFirstpass);

            UnityEngine.Debug.Log($"[ILWeaver] Editor DLL weaved. Patched methods: {total}");
            AssetDatabase.Refresh();
        }
        #endregion

        #region Build 前（供 IL2CPP）
        public void OnPreprocessBuild(BuildReport report)
        {
            // 若是 IL2CPP：要在轉 C++ 之前就把 ScriptAssemblies 改好
            if (PlayerSettings.GetScriptingBackend(report.summary.platformGroup) == ScriptingImplementation.IL2CPP)
            {
                var scriptAsmDir    = Path.GetFullPath("Library/ScriptAssemblies");
                var asmCSharp       = Path.Combine(scriptAsmDir, "Assembly-CSharp.dll");
                var asmFirstpass    = Path.Combine(scriptAsmDir, "Assembly-CSharp-firstpass.dll");

                int total = 0;
                if (File.Exists(asmCSharp)) total += WeaveOne(asmCSharp);
                if (File.Exists(asmFirstpass)) total += WeaveOne(asmFirstpass);

                UnityEngine.Debug.Log($"[ILWeaver][Preprocess IL2CPP] Patched methods: {total}");
            }
        }
        #endregion

        #region Build 後（供 Mono）
        public void OnPostprocessBuild(BuildReport report)
        {
            // 若是 Mono：Build 後直接改 Player 目錄中的 Assembly-CSharp.dll
            if (PlayerSettings.GetScriptingBackend(report.summary.platformGroup) == ScriptingImplementation.Mono2x)
            {
                var managedDir      = GetManagedDir(report);
                if (managedDir == null)
                {
                    UnityEngine.Debug.LogWarning("[ILWeaver] Cannot locate Managed folder. Skip.");
                    return;
                }

                var asmCSharp       = Path.Combine(managedDir, "Assembly-CSharp.dll");
                var asmFirstpass    = Path.Combine(managedDir, "Assembly-CSharp-firstpass.dll");

                int total = 0;
                if (File.Exists(asmCSharp)) total += WeaveOne(asmCSharp);
                if (File.Exists(asmFirstpass)) total += WeaveOne(asmFirstpass);

                UnityEngine.Debug.Log($"[ILWeaver][Postprocess Mono] Patched methods: {total}");
            }
        }

        private static string GetManagedDir(BuildReport report)
        {
            // 主要平台範例（可依需要擴充）
            var outPath = report.summary.outputPath;
#if UNITY_STANDALONE
            if (report.summary.platform == BuildTarget.StandaloneWindows ||
                report.summary.platform == BuildTarget.StandaloneWindows64)
            {
                // Windows Standalone：<exeName>_Data/Managed
                var data    = Path.ChangeExtension(outPath, null) + "_Data";
                var managed = Path.Combine(data, "Managed");
                return Directory.Exists(managed) ? managed : null;
            }
            if (report.summary.platform == BuildTarget.StandaloneOSX)
            {
                // macOS：<App>.app/Contents/Resources/Data/Managed
                var managed = Path.Combine(outPath, "Contents/Resources/Data/Managed");
                return Directory.Exists(managed) ? managed : null;
            }
#endif
            // Android/iOS（IL2CPP）通常走 Preprocess，不在這裡處理
            return null;
        }
        #endregion

        #region 主要織入邏輯
        private static int WeaveOne(string assemblyPath)
        {
            var resolver    = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath));

            var readSymbols = File.Exists(Path.ChangeExtension(assemblyPath, "pdb"));
            var rp          = new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = readSymbols };
            var wp          = new WriterParameters { WriteSymbols = readSymbols };

            var module      = ModuleDefinition.ReadModule(assemblyPath, rp);

            // 引用必要方法/型別
            var consoleWriteLine    = module.ImportReference(typeof(System.Console).GetMethod(nameof(System.Console.WriteLine), new[] { typeof(string) }));
            var stopwatchType       = module.ImportReference(typeof(System.Diagnostics.Stopwatch));
            var swStartNew          = module.ImportReference(typeof(System.Diagnostics.Stopwatch).GetMethod("StartNew", Type.EmptyTypes));
            var swStop              = module.ImportReference(typeof(System.Diagnostics.Stopwatch).GetMethod("Stop"));
            var swElapsedMsGet      = module.ImportReference(typeof(System.Diagnostics.Stopwatch).GetProperty("ElapsedMilliseconds").GetGetMethod());
            var stringConcatObj     = module.ImportReference(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(object) }));
            var stringConcatStr     = module.ImportReference(typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) }));
            var boxInt64            = module.ImportReference(typeof(long));

            int patched = 0;

            foreach (var type in module.Types)
            {
                foreach (var method in type.Methods.Where(m => m.HasBody && m.CustomAttributes.Any(a => a.AttributeType.FullName == kLogAttributeFullName)))
                {
                    PatchMethod(method);
                    patched++;
                }
            }

            module.Write(assemblyPath, wp);
            return patched;

            void PatchMethod(MethodDefinition method)
            {
                var il = method.Body.GetILProcessor();
                //method.Body.SimplifyMacros();

                // 建立 local: Stopwatch sw
                var swVar           = new VariableDefinition(stopwatchType);
                method.Body.Variables.Add(swVar);

                string enterMsg     = $"[Enter] {method.DeclaringType.FullName}.{method.Name}";
                string exitPrefix   = $"[Exit]  {method.DeclaringType.FullName}.{method.Name} (ms=";

                var first = method.Body.Instructions.First();

                // 入口插入：Console.WriteLine(enterMsg); sw = Stopwatch.StartNew();
                il.InsertBefore(first, il.Create(OpCodes.Ldstr, enterMsg));
                il.InsertBefore(first, il.Create(OpCodes.Call, consoleWriteLine));
                il.InsertBefore(first, il.Create(OpCodes.Call, swStartNew));
                il.InsertBefore(first, il.Create(OpCodes.Stloc, swVar));

                // finally 區塊
                var finallyStart    = il.Create(OpCodes.Nop);
                var finallyEnd      = il.Create(OpCodes.Endfinally);

                il.Append(finallyStart);
                {
                    il.Append(il.Create(OpCodes.Ldloc, swVar));
                    il.Append(il.Create(OpCodes.Call, swStop));

                    il.Append(il.Create(OpCodes.Ldstr, exitPrefix));
                    il.Append(il.Create(OpCodes.Ldloc, swVar));
                    il.Append(il.Create(OpCodes.Call, swElapsedMsGet));   // long
                    il.Append(il.Create(OpCodes.Box, boxInt64));
                    il.Append(il.Create(OpCodes.Call, stringConcatObj));  // prefix + ms
                    il.Append(il.Create(OpCodes.Ldstr, ")"));
                    il.Append(il.Create(OpCodes.Call, stringConcatStr));  // ... + ")"
                    il.Append(il.Create(OpCodes.Call, consoleWriteLine));
                }
                il.Append(finallyEnd);

                // 把整個方法包成 try/finally
                var handler         = new ExceptionHandler(ExceptionHandlerType.Finally)
                {
                    TryStart        = first,         // 注意：first 之前我們已插入進入碼，這裡從 first 開始
                    TryEnd          = finallyStart,  // 到 finallyStart 前為止
                    HandlerStart    = finallyStart,
                    HandlerEnd      = finallyEnd
                };
                method.Body.ExceptionHandlers.Add(handler);

                method.Body.InitLocals = true;
                //method.Body.OptimizeMacros();
            }
        }
        #endregion
    }
}
#endif

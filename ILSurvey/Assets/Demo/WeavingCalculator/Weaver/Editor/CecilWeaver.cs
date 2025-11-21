// Assets/WeavingDemo/Weaver/Editor/CecilWeaver.cs
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Mono.Cecil;
using Mono.Cecil.Cil;

[InitializeOnLoad]
public static class CecilWeaver
{
    // 避免同一回合重入
    private static int _weaving;

    static CecilWeaver()
    {
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
    }

    private static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] msgs)
    {
        // 1) 該組件編譯失敗就不要動
        if (msgs != null && msgs.Any(m => m.type == CompilerMessageType.Error)) return;

        // 2) 僅處理目標 dll（避免誤改其他組件）
        if (!assemblyPath.EndsWith("WeaveTarget.dll", StringComparison.OrdinalIgnoreCase)) return;

        // 3) 重入保護（本輪只處理一次）
        if (Interlocked.Exchange(ref _weaving, 1) == 1) return;

        try
        {
            var pdbPath     = Path.ChangeExtension(assemblyPath, "pdb");
            bool hasPdb     = File.Exists(pdbPath);
            string temp     = assemblyPath + ".weave.tmp";
            string backup   = assemblyPath + ".bak";

            // 4) 直接用「檔案路徑」讀取；Cecil 會在同路徑尋找 PDB
            var rp = new ReaderParameters { ReadSymbols = hasPdb };
            var wp = new WriterParameters { WriteSymbols = hasPdb };

            using (var module = ModuleDefinition.ReadModule(assemblyPath, rp))
            {
                var type        = module.GetType("WeaveTarget.Calculator") ?? throw new Exception("Type not found: WeaveTarget.Calculator");
                var method      = type.Methods.FirstOrDefault(m => m.Name == "Add") ?? throw new Exception("Method not found: Add");

                bool isInstance = !method.IsStatic;

                // 5) 清空既有 IL 與 EH
                method.Body.ExceptionHandlers.Clear();
                method.Body.Instructions.Clear();

                // 6) 先簡化巨集，最後再最佳化
                //method.Body.SimplifyMacros();

                var il = method.Body.GetILProcessor();

                // 目標：return (a + b) * 10;
                if (isInstance) { il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_2); }
                else { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); }

                il.Emit(OpCodes.Add);
                il.Emit(OpCodes.Ldc_I4, 10);
                il.Emit(OpCodes.Mul);
                il.Emit(OpCodes.Ret);

                //method.Body.OptimizeMacros();

                // 7) 寫到暫存檔（避免 in-place 鎖檔）
                module.Write(temp, wp);
            }

            // 8) 嘗試原子替換（多次重試）
            ReplaceWithRetry(temp, assemblyPath, backup);

            // 9) 同步 PDB（若有）
            if (hasPdb)
            {
                string tempPdb = Path.ChangeExtension(temp, "pdb");
                if (File.Exists(tempPdb))
                {
                    ReplaceWithRetry(tempPdb, pdbPath, Path.ChangeExtension(backup, "pdb"));
                }
            }

            Debug.Log("[Weaver] WeaveTarget.dll 替換完成");
        }
        catch (Exception ex)
        {
            Debug.LogError("[Weaver] " + ex);
        }
        finally
        {
            Interlocked.Exchange(ref _weaving, 0);
        }
    }

    private static void ReplaceWithRetry(string temp, string target, string backup, int attempts = 10, int delayMs = 50)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                // 確保目標可寫
                if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal);

                // 原子替換（Windows）
                if (File.Exists(target)) File.Replace(temp, target, backup, true);
                else File.Move(temp, target);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(delayMs);
            }
        }
        // 最後一次拋出錯誤以利偵錯
        if (File.Exists(target)) File.Replace(temp, target, backup, true);
        else File.Move(temp, target);
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

using Mono.Cecil;

namespace XPlan.Editors.Weaver
{
    [InitializeOnLoad]
    public static class CecilWeaver
    {
        private static int _weaving;

        // 想被 Weave 的 DLL 名稱列表（檔名字串）
        private static readonly string[] TargetAssemblyNames =
        {
            "Assembly-CSharp.dll",
            // 之後你可以繼續加新的Dll
        };

        // ★ 這裡註冊所有「方法切面」
        private static readonly IMethodAspectWeaver[] _methodAspects =
        {
            new LogAspectWeaver(),
            new NotifyHandlerWeaver(),
            // 之後你可以繼續加新的切面
            // new AnotherAspectWeaver(),
        };

        // 類別級切面
        private static readonly ITypeAspectWeaver[] _typeAspects =
        {
            // 例：某個 [NotifyHandler] 如果你想貼在 class 上    
            // new SomeTypeAspectWeaver(),
        };

        // 欄位級切面
        private static readonly IFieldAspectWeaver[] _fieldAspects =
        {
            // 例：I18N 用在 Text / Image 欄位上    
            new I18NViewWeaver(),
        };

        static CecilWeaver()
        {
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
        }

        private static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] msgs)
        {
            try
            {
                // 1) 編譯失敗就不處理
                if (msgs != null && msgs.Any(m => m.type == CompilerMessageType.Error))
                    return;

                // 2) 只處理我們關心的目標 DLL
                if (!IsTargetAssembly(assemblyPath))
                    return;

                // 3) 避免同一回合重入
                if (Interlocked.Exchange(ref _weaving, 1) == 1)
                    return;

                DoWeave(assemblyPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Weaver] 例外：" + ex);
            }
            finally
            {
                Interlocked.Exchange(ref _weaving, 0);
            }
        }

        /// <summary>
        /// 判斷這次編好的 assemblyPath 是否在我們的目標名單裡
        /// </summary>
        private static bool IsTargetAssembly(string assemblyPath)
        {
            var fileName = Path.GetFileName(assemblyPath);
            return TargetAssemblyNames.Any(x =>
                fileName.Equals(x, StringComparison.OrdinalIgnoreCase));
        }

        private static void DoWeave(string assemblyPath)
        {
            var pdbPath     = Path.ChangeExtension(assemblyPath, "pdb");
            bool hasPdb     = File.Exists(pdbPath);
            string temp     = assemblyPath + ".weave.tmp";
            string backup   = assemblyPath + ".bak";

            /****************************************
             * ★ 重點：設定 AssemblyResolver
             ****************************************/
            var resolver    = new DefaultAssemblyResolver();

            // 目標 DLL 所在資料夾（通常就是 Library/ScriptAssemblies）
            var asmDir = Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrEmpty(asmDir) && Directory.Exists(asmDir))
            {
                resolver.AddSearchDirectory(asmDir);
            }

            // 專案根目錄：Assets/..
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            // Library/ScriptAssemblies 底下放著：
            // - Assembly-CSharp.dll
            // - Assembly-CSharp-firstpass.dll
            // - 各種 asmdef 對應的 dll
            var scriptAssembliesDir = Path.Combine(projectRoot, "Library", "ScriptAssemblies");
            if (Directory.Exists(scriptAssembliesDir))
            {
                resolver.AddSearchDirectory(scriptAssembliesDir);
            }

            // Unity 安裝路徑：EditorApplication.applicationPath → .../Editor/Unity.exe
            var editorDir   = Path.GetDirectoryName(EditorApplication.applicationPath);
            var dataDir     = Path.Combine(editorDir, "Data");

            // 1) NetStandard BCL（shim）
            var netStandardDir = Path.Combine(dataDir, "NetStandard", "compat");
            if (Directory.Exists(netStandardDir))
            {
                resolver.AddSearchDirectory(netStandardDir);
            }

            // 2) MonoBleedingEdge BCL（System / mscorlib / netstandard 等）
            // 版本號可能不一樣，這裡用 4.7.1-api 是 2021LTS 常見配置
            var monoApiDir = Path.Combine(dataDir, "MonoBleedingEdge", "lib", "mono", "4.7.1-api");
            if (Directory.Exists(monoApiDir))
            {
                resolver.AddSearchDirectory(monoApiDir);
            }

            var rp = new ReaderParameters
            {
                ReadSymbols         = hasPdb,
                AssemblyResolver    = resolver,
            };

            var wp = new WriterParameters { WriteSymbols = hasPdb };

            using (var module = ModuleDefinition.ReadModule(assemblyPath, rp))
            {
                // ★ 統一收集「要被織入的項目」
                var methodTargets   = new List<(IMethodAspectWeaver aspect, MethodDefinition method, CustomAttribute attr)>();
                var typeTargets     = new List<(ITypeAspectWeaver aspect, TypeDefinition type, CustomAttribute attr)>();
                var fieldTargets    = new List<(IFieldAspectWeaver aspect, FieldDefinition field, CustomAttribute attr)>();

                foreach (var type in module.Types)
                {
                    foreach (var nestedType in GetAllNestedTypes(type))
                    {
                        // ① 類別屬性
                        if (nestedType.HasCustomAttributes)
                        {
                            foreach (var attr in nestedType.CustomAttributes)
                            {
                                foreach (var aspect in _typeAspects)
                                {
                                    if (attr.AttributeType.FullName == aspect.AttributeFullName)
                                    {
                                        typeTargets.Add((aspect, nestedType, attr));
                                    }
                                }
                            }
                        }

                        // ② 欄位屬性
                        foreach (var field in nestedType.Fields)
                        {
                            if (!field.HasCustomAttributes)
                                continue;

                            foreach (var attr in field.CustomAttributes)
                            {
                                foreach (var aspect in _fieldAspects)
                                {
                                    if (attr.AttributeType.FullName == aspect.AttributeFullName)
                                    {
                                        fieldTargets.Add((aspect, field, attr));
                                    }
                                }
                            }
                        }

                        // ③ 方法屬性（你原本的邏輯）
                        foreach (var method in nestedType.Methods)
                        {
                            if (!method.HasBody || !method.HasCustomAttributes)
                                continue;

                            foreach (var attr in method.CustomAttributes)
                            {
                                foreach (var aspect in _methodAspects)
                                {
                                    if (attr.AttributeType.FullName == aspect.AttributeFullName)
                                    {
                                        methodTargets.Add((aspect, method, attr));
                                    }
                                }
                            }
                        }
                    }
                }

                if (typeTargets.Count == 0 && fieldTargets.Count == 0 && methodTargets.Count == 0)
                {
                    Debug.Log("[Weaver] 找不到任何可處理的切面標記，略過");
                }
                else
                {
                    foreach (var (aspect, t, attr) in typeTargets)
                    {
                        try
                        {
                            aspect.Apply(module, t, attr);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[Weaver] TypeAspect {aspect.GetType().Name} 織入 {t.FullName} 失敗：{ex}");
                        }
                    }

                    foreach (var (aspect, f, attr) in fieldTargets)
                    {
                        try
                        {
                            aspect.Apply(module, f, attr);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[Weaver] FieldAspect {aspect.GetType().Name} 織入 {f.FullName} 失敗：{ex}");
                        }
                    }

                    foreach (var (aspect, m, attr) in methodTargets)
                    {
                        try
                        {
                            aspect.Apply(module, m, attr);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[Weaver] MethodAspect {aspect.GetType().Name} 織入 {m.FullName} 失敗：{ex}");
                        }
                    }
                }

                module.Write(temp, wp);
            }

            ReplaceWithRetry(temp, assemblyPath, backup);

            if (hasPdb)
            {
                string tempPdb = Path.ChangeExtension(temp, "pdb");
                if (File.Exists(tempPdb))
                {
                    ReplaceWithRetry(tempPdb, pdbPath, Path.ChangeExtension(backup, "pdb"));
                }
            }

            var asmName = Path.GetFileName(assemblyPath);
            Debug.Log("[Weaver] " + asmName + " weaving 完成");
        }

        private static IEnumerable<TypeDefinition> GetAllNestedTypes(TypeDefinition type)
        {
            yield return type;

            if (type.HasNestedTypes)
            {
                foreach (var nt in type.NestedTypes)
                {
                    foreach (var t in GetAllNestedTypes(nt))
                        yield return t;
                }
            }
        }

        private static void ReplaceWithRetry(string temp, string target, string backup, int attempts = 10, int delayMs = 50)
        {
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    if (File.Exists(target))
                        File.SetAttributes(target, FileAttributes.Normal);

                    if (File.Exists(target))
                        File.Replace(temp, target, backup, true);
                    else
                        File.Move(temp, target);

                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMs);
                }
            }

            if (File.Exists(target))
                File.Replace(temp, target, backup, true);
            else
                File.Move(temp, target);
        }

        /****************************************
         * 切面介面：所有級別的 Aspect 都實作它們
         ****************************************/
        public interface IMethodAspectWeaver
        {
            string AttributeFullName { get; }

            void Apply(ModuleDefinition module, MethodDefinition targetMethod, CustomAttribute attr);
        }

        public interface ITypeAspectWeaver
        {
            string AttributeFullName { get; }
            void Apply(ModuleDefinition module, TypeDefinition targetType, CustomAttribute attr);
        }

        public interface IFieldAspectWeaver
        {
            string AttributeFullName { get; }
            void Apply(ModuleDefinition module, FieldDefinition targetField, CustomAttribute attr);
        }
    }
}
#endif

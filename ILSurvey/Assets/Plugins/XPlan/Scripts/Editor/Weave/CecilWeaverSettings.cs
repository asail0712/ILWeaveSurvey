using UnityEngine;
using UnityEditor;

namespace XPlan.Editors.Weaver
{
    public static class CecilWeaverSettings
    {
        private const string Key = "XPlan.CecilWeaver.Enabled";

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(Key, true);
            set => EditorPrefs.SetBool(Key, value);
        }

        [MenuItem("XPlanTools/Weaver/Toggle Enabled")]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Debug.Log($"[CecilWeaver] Enabled = {Enabled}");
        }
    }
}

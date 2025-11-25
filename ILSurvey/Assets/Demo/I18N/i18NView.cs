using UnityEngine;
using UnityEngine.UI;

using TMPro;

using XPlan;
using XPlan.UI;
using XPlan.Utility;

namespace WeaveTarget
{
    public class i18NView : ViewBase<i18NViewModel>
    {
        [I18NView("DemoStr1")]
        [SerializeField]private Text demoTxt;

        [I18NView("DemoStr2")]
        [SerializeField] private TextMeshProUGUI demoTMP;

        [I18NView("DemoImg")]
        [SerializeField] private Image demoImg;

        [ContextMenu("換成中文")]
        private void ChangeCHT()
        {
            StringTable.Instance.CurrLanguage = 1;
        }

        [ContextMenu("換成英文")]
        private void ChangeEng()
        {
            StringTable.Instance.CurrLanguage = 0;
        }
    }
}

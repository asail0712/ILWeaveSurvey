using UnityEngine;
using UnityEngine.UI;

using XPlan;
using XPlan.UI;

namespace WeaveTarget
{
    [ViewBinding]
    public class ButtonTriggerView : ViewBase<TriggerViewModel>
    {        
        [SerializeField] private Button demoTriggerBtn;
    }
}

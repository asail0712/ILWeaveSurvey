using UnityEngine;
using XPlan;

namespace WeaveTarget
{
    public class MVVMWeaveDemo : SystemBase
    {
        protected override void OnInitialLogic()
        {
            RegisterLogic(new TriggerViewModel());
        }
    }
}

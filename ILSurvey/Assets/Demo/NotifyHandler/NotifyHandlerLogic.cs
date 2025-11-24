using UnityEngine;

using XPlan;
using XPlan.Observe;

namespace WeaveTarget
{
    public class DemoMessage : MessageBase
    {
        public string demoStr;

        public DemoMessage(string demoStr)
        {
            this.demoStr = demoStr;
        }
    }

    public class NotifyHandlerLogic : LogicComponent
    {
        [NotifyHandler(typeof(DemoMessage))]
        private void ShowMessage(DemoMessage msg)
        {
            Debug.Log(msg.demoStr);
        }
    }
}
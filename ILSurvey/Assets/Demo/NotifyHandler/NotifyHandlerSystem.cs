using UnityEngine;

using XPlan;

namespace WeaveTarget
{
    public class NotifyHandlerSystem : SystemBase
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected override void OnInitialLogic()
        {
            // for override
            RegisterLogic(new NotifyHandlerLogic());
        }

        [ContextMenu("SendMsg XDDDDDDD")]
        private void SendMsg()
        {
            DemoMessage msg = new DemoMessage("XDDDDDDD");
            msg.Send();
        }
    }
}
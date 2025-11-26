using UnityEngine;
using XPlan;
using XPlan.UI;

namespace WeaveTarget
{
    public class TriggerViewModel : ViewModelBase
    {
        public TriggerViewModel()
        {
            
        }

        [ButtonBinding]
        private void OnDemoTriggerClick()
        {
            Debug.Log("Binding Success");
        }
    }
}
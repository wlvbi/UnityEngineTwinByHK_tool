using HKTools;
using QFramework;
using System.Collections.Generic;

namespace HKTools
{
    public class CMD_TIP_HideTips : AbstractCommand
    {
        List<HK_TipsCtrl> tips;

        public CMD_TIP_HideTips(List<HK_TipsCtrl> _tips)
        {
            tips = _tips;
        }

        protected override void OnExecute()
        {
            this.GetSystem<IHK_TipSystem>().HideTips(tips);
        }
    }
}

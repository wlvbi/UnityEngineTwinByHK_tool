using HKTools;
using QFramework;
using System.Collections.Generic;

namespace HKTools
{
    public class CMD_TIP_ShowTips : AbstractCommand
    {
        List<HK_TipsCtrl> tips;

        public CMD_TIP_ShowTips(List<HK_TipsCtrl> _tips)
        {
            tips = _tips;
        }

        protected override void OnExecute()
        {
            this.GetSystem<IHK_TipSystem>().ShowTips(tips);
        }
    }
}

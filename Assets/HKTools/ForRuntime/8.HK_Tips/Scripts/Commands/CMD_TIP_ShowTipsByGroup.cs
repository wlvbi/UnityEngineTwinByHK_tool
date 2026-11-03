using QFramework;

namespace HKTools
{
    public class CMD_TIP_ShowTipsByGroup : AbstractCommand
    {
        HK_TipGroup tipGroup;

        public CMD_TIP_ShowTipsByGroup(HK_TipGroup _tipGroup)
        {
            tipGroup = _tipGroup;
        }

        protected override void OnExecute()
        {
            this.GetSystem<IHK_TipSystem>().ShowTipsByGroup(tipGroup);
        }
    }
}
using HKTools;
using QFramework;

namespace HKTools
{
    public class CMD_TIP_HideAllHKTips : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.GetSystem<IHK_TipSystem>().HideAllTips();
        }
    }
}

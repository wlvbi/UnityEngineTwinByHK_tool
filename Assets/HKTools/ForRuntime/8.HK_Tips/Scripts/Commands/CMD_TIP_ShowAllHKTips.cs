using HKTools;
using QFramework;

namespace HKTools
{
    public class CMD_TIP_ShowAllHKTips : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.GetSystem<IHK_TipSystem>().ShowAllTips();
        }
    }
}

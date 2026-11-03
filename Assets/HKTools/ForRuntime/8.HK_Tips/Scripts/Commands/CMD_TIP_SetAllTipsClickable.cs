﻿using HKTools;
using QFramework;

namespace HKTools
{
    /// <summary>批量设置所有 Tip 的可点击状态（显隐请用 Show/Hide 系列指令）</summary>
    public class CMD_TIP_SetAllTipsClickable : AbstractCommand
    {
        bool isClickable;

        public CMD_TIP_SetAllTipsClickable(bool isClickable)
        {
            this.isClickable = isClickable;
        }

        protected override void OnExecute()
        {
            this.GetSystem<IHK_TipSystem>().SetAllTipsClickable(isClickable);
        }
    }
}

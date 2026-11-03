using HKTools;
using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace EngineTwin
{
    /// <summary>
    /// 鼠标拾取（新输入系统）：悬停高亮、点击选中部件。
    /// 点到 HK_Tip 标签时调用框架自己的点击回调（HK_TipTitle.OnClickCallback），
    /// 由框架派发 Event_TIP_OnClick —— HK_TipTitle 原生依赖 OnMouse 消息，在仅启用新输入系统的项目里需要这层桥接。
    /// </summary>
    public class EnginePicker : EngineTwinController
    {
        const float ClickMaxMovePixels = 6f;

        Camera viewCamera;
        IEnginePartModel partModel;
        Vector2 pressPosition;
        bool pressStartedOnScene;

        public void Init(Camera camera)
        {
            viewCamera = camera;
            partModel = this.GetModel<IEnginePartModel>();
        }

        void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || viewCamera == null || partModel == null) return;

            Vector2 pos = mouse.position.ReadValue();
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            EnginePart hitPart = null;
            HK_TipTitle hitTip = null;
            if (!overUI && Physics.Raycast(viewCamera.ScreenPointToRay(pos), out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Collide))
            {
                hitTip = hit.collider.GetComponent<HK_TipTitle>();
                if (hitTip == null) hitPart = partModel.FindByCollider(hit.collider);
            }

            partModel.Hovered.Value = hitTip != null ? partModel.FindByTip(hitTip.GetComponentInParent<HK_TipsCtrl>()) : hitPart;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressPosition = pos;
                pressStartedOnScene = !overUI;
            }

            bool isClick = mouse.leftButton.wasReleasedThisFrame && pressStartedOnScene
                           && (pos - pressPosition).sqrMagnitude <= ClickMaxMovePixels * ClickMaxMovePixels;
            if (!isClick) return;

            if (hitTip != null)
                hitTip.OnClickCallback?.Invoke();               // → HK_TipsCtrl → Event_TIP_OnClick
            else
                this.SendCommand(new CMD_SelectPart(hitPart));  // 点空白处 = 取消选中
        }
    }
}

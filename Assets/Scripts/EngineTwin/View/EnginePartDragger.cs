using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace EngineTwin
{
    /// <summary>
    /// 部件拖拽系统：允许拖拽已拆卸的部件到新位置。
    /// 左键拖拽移动（在相机平行平面），Ctrl + 左键前后移动。
    /// 拖拽时相机自动锁定。
    /// </summary>
    public class EnginePartDragger : EngineTwinController
    {
        public float dragSensitivity = 0.8f;          // 拖拽灵敏度
        public float depthSensitivity = 0.01f;        // 深度拖拽灵敏度
        public float snapDistance = 0.05f;            // 吸附距离阈值
        public bool enableSnapping = true;            // 是否启用吸附

        Camera viewCamera;
        IEnginePartModel partModel;
        EngineAssembly assembly;
        Transform engineRoot;
        EngineOrbitCamera orbitCamera;
        List<EnginePart> allParts; // 用于持久化保存

        EnginePart draggingPart;
        Vector3 dragStartPartPos;
        Vector3 dragStartMouseWorld;
        float dragDistanceMultiplier = 1f;
        bool isDragging;

        public void Init(Camera camera, EngineAssembly engineAssembly, Transform root)
        {
            viewCamera = camera;
            partModel = this.GetModel<IEnginePartModel>();
            assembly = engineAssembly;
            engineRoot = root;
            orbitCamera = camera.GetComponent<EngineOrbitCamera>();
            allParts = new List<EnginePart>(partModel.Parts);
        }

        void Update()
        {
            if (viewCamera == null || partModel == null || assembly == null) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (overUI) return;

            HandleDragStart(mouse);
            HandleDragging(mouse);
            HandleDragEnd(mouse);
        }

        void HandleDragStart(Mouse mouse)
        {
            if (isDragging) return;

            // 左键按下开始拖拽（不需要 Shift，因为相机已经锁定）
            if (mouse.leftButton.wasPressedThisFrame)
            {
                EnginePart selected = partModel.Selected.Value;
                if (selected != null && assembly.IsDetached(selected))
                {
                    Vector2 screenPos = mouse.position.ReadValue();
                    Ray ray = viewCamera.ScreenPointToRay(screenPos);

                    // 检查是否点击到选中的部件
                    if (Physics.Raycast(ray, out RaycastHit hit, 50f))
                    {
                        EnginePart hitPart = partModel.FindByCollider(hit.collider);
                        if (hitPart == selected)
                        {
                            StartDragging(selected, hit.point);
                        }
                    }
                }
            }
        }

        void StartDragging(EnginePart part, Vector3 hitPoint)
        {
            draggingPart = part;
            dragStartPartPos = part.Group.position;
            dragStartMouseWorld = hitPoint;
            isDragging = true;
            dragDistanceMultiplier = 1f;

            // 锁定相机
            if (orbitCamera != null)
            {
                orbitCamera.Lock();
            }

            // 通知 Assembly 停止更新此部件位置
            assembly.SetDragging(part, true);

            Debug.Log($"[拖拽] 开始拖拽部件: {part.DisplayName}，相机已锁定");
        }

        void HandleDragging(Mouse mouse)
        {
            if (!isDragging || draggingPart == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude < 0.01f) return;

            bool ctrlPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;

            // Shift + Ctrl + 左键：沿相机深度方向移动
            if (ctrlPressed && mouse.leftButton.isPressed)
            {
                DragDepth(delta.y);
            }
            // Shift + 左键：在相机平行平面移动
            else if (mouse.leftButton.isPressed)
            {
                DragInCameraPlane(delta);
            }
        }

        void DragInCameraPlane(Vector2 delta)
        {
            // 计算相机空间的移动向量
            Vector3 right = viewCamera.transform.right;
            Vector3 up = viewCamera.transform.up;

            // 根据距离自动调整灵敏度
            float distanceToCamera = Vector3.Distance(viewCamera.transform.position, draggingPart.Group.position);
            float sensitivity = dragSensitivity * distanceToCamera * 0.001f;

            Vector3 worldDelta = (right * delta.x + up * delta.y) * sensitivity;
            draggingPart.Group.position += worldDelta;
        }

        void DragDepth(float deltaY)
        {
            Vector3 forward = viewCamera.transform.forward;

            // 根据距离自动调整灵敏度
            float distanceToCamera = Vector3.Distance(viewCamera.transform.position, draggingPart.Group.position);
            float sensitivity = depthSensitivity * distanceToCamera;

            Vector3 worldDelta = forward * deltaY * sensitivity;
            draggingPart.Group.position += worldDelta;
        }

        void HandleDragEnd(Mouse mouse)
        {
            if (!isDragging) return;

            // 左键释放结束拖拽
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                EndDragging();
            }

            // ESC 取消拖拽，恢复原位
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelDragging();
            }
        }

        void EndDragging()
        {
            if (draggingPart == null) return;

            Vector3 currentPos = draggingPart.Group.position;
            Vector3 defaultDetachPos = assembly.GetDefaultDetachWorldPosition(draggingPart);

            // 通知 Assembly 恢复更新此部件位置
            assembly.SetDragging(draggingPart, false);

            // 尝试吸附到标准拆卸结束位置
            if (enableSnapping)
            {
                float distance = Vector3.Distance(currentPos, defaultDetachPos);
                if (distance < snapDistance)
                {
                    // 吸附到标准拆卸结束位置（清除自定义位置）
                    assembly.ClearCustomPosition(draggingPart);
                    Debug.Log($"[拖拽] 部件 {draggingPart.DisplayName} 吸附到默认拆卸位置");
                }
                else
                {
                    // 保存自定义拆卸结束位置
                    assembly.SetCustomPosition(draggingPart, currentPos);
                    Debug.Log($"[拖拽] 部件 {draggingPart.DisplayName} 设置自定义拆卸位置");
                }
            }
            else
            {
                // 不启用吸附时直接保存位置
                assembly.SetCustomPosition(draggingPart, currentPos);
            }

            // 解锁相机
            if (orbitCamera != null)
            {
                orbitCamera.Unlock();
            }

            // 保存位置到本地
            PartPositionPersistence.Save(assembly, allParts);

            draggingPart = null;
            isDragging = false;
            Debug.Log("[拖拽] 拖拽结束，相机已解锁，位置已保存");
        }

        void CancelDragging()
        {
            if (draggingPart == null) return;

            // 通知 Assembly 恢复更新此部件位置
            assembly.SetDragging(draggingPart, false);

            draggingPart.Group.position = dragStartPartPos;
            Debug.Log($"[拖拽] 取消拖拽，恢复部件 {draggingPart.DisplayName} 到原位");

            // 解锁相机
            if (orbitCamera != null)
            {
                orbitCamera.Unlock();
            }

            draggingPart = null;
            isDragging = false;
            Debug.Log("[拖拽] 拖拽取消，相机已解锁");
        }

        void OnGUI()
        {
            if (!isDragging) return;

            // 显示拖拽提示
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 16;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;

            string hint = $"拖拽中: {draggingPart?.DisplayName}\n" +
                         $"拖动鼠标: 平面移动 | Ctrl+拖动: 深度移动\n" +
                         $"释放左键: 完成 | ESC: 取消\n" +
                         $"(相机已锁定)";

            GUILayout.BeginArea(new Rect(Screen.width / 2 - 200, 20, 400, 80));
            GUILayout.Box(hint, style);
            GUILayout.EndArea();
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace EngineTwin
{
    /// <summary>环绕相机（新输入系统）：左键旋转、右键/中键平移、滚轮缩放、R 复位，可平滑聚焦到部件</summary>
    [RequireComponent(typeof(Camera))]
    public class EngineOrbitCamera : MonoBehaviour
    {
        public float rotateSpeed = 0.25f;
        public float panSpeed = 0.0012f;
        public float minDistance = 0.5f;
        public float maxDistance = 6f;
        public float smoothing = 10f;

        Vector3 pivot, targetPivot, homePivot;
        float yaw, pitch, distance;
        float targetYaw, targetPitch, targetDistance;
        float homeYaw, homePitch, homeDistance;
        bool initialized, dragging;
        int dragButton;
        bool locked; // 外部锁定标志，用于拖拽部件时禁用相机

        /// <summary>锁定相机（拖拽部件时调用）</summary>
        public void Lock() => locked = true;

        /// <summary>解锁相机</summary>
        public void Unlock() => locked = false;

        public void Init(Vector3 pivotPoint)
        {
            pivot = targetPivot = homePivot = pivotPoint;
            Vector3 offset = transform.position - pivotPoint;
            distance = targetDistance = homeDistance = Mathf.Max(offset.magnitude, minDistance);
            Vector3 e = Quaternion.LookRotation(-offset.normalized, Vector3.up).eulerAngles;
            pitch = targetPitch = homePitch = e.x > 180f ? e.x - 360f : e.x;
            yaw = targetYaw = homeYaw = e.y;
            initialized = true;
        }

        public void Focus(Bounds bounds)
        {
            targetPivot = bounds.center;
            // 小部件也保持一定距离：既能看清周边结构，也让 HK_Tip 标签处于合适的缩放区间（camRange 1.2~4）
            targetDistance = Mathf.Clamp(bounds.extents.magnitude * 4f + 0.9f, 1.3f, 3f);
        }

        public void ResetView()
        {
            targetPivot = homePivot;
            targetYaw = homeYaw;
            targetPitch = homePitch;
            targetDistance = homeDistance;
        }

        void LateUpdate()
        {
            if (!initialized) return;

            if (Mouse.current != null) HandleMouse(Mouse.current);
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetView();

            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            yaw = Mathf.LerpAngle(yaw, targetYaw, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            distance = Mathf.Lerp(distance, targetDistance, k);
            pivot = Vector3.Lerp(pivot, targetPivot, k);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(pivot - rot * Vector3.forward * distance, rot);
        }

        void HandleMouse(Mouse mouse)
        {
            // 如果相机被锁定，跳过所有输入处理
            if (locked)
            {
                dragging = false;
                return;
            }

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (!dragging && !overUI)
            {
                if (mouse.leftButton.wasPressedThisFrame) { dragging = true; dragButton = 0; }
                else if (mouse.rightButton.wasPressedThisFrame) { dragging = true; dragButton = 1; }
                else if (mouse.middleButton.wasPressedThisFrame) { dragging = true; dragButton = 2; }
            }

            if (dragging)
            {
                ButtonControl button = dragButton == 0 ? mouse.leftButton : dragButton == 1 ? mouse.rightButton : mouse.middleButton;
                if (!button.isPressed)
                {
                    dragging = false;
                }
                else
                {
                    Vector2 d = mouse.delta.ReadValue();
                    if (dragButton == 0)
                    {
                        targetYaw += d.x * rotateSpeed;
                        targetPitch = Mathf.Clamp(targetPitch - d.y * rotateSpeed, -15f, 85f);
                    }
                    else
                    {
                        targetPivot -= (transform.right * d.x + transform.up * d.y) * panSpeed * targetDistance;
                    }
                }
            }

            if (!overUI)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    // 不同平台/版本的滚轮单位不同（1 或 120 每格），统一换算成「格」
                    float notches = Mathf.Abs(scroll) >= 10f ? scroll / 120f : scroll;
                    targetDistance = Mathf.Clamp(targetDistance * Mathf.Pow(0.88f, notches), minDistance, maxDistance);
                }
            }
        }
    }
}

# URP 配置指南

## 🚀 快速配置（推荐）

### 方法 1：使用菜单工具（最简单）

1. **Unity 菜单栏** → **EngineTwin** → **Setup URP Pipeline**
2. 等待配置完成
3. 看到提示框"URP 配置完成"
4. ✅ 完成！

---

## 📝 手动配置步骤

如果自动工具不工作，请手动配置：

### 步骤 1：创建 URP 资源

1. **Project 窗口** 右键 → **Create** → **Rendering** → **URP Asset (with Universal Renderer)**
2. 命名为：`EngineTwin_URP`
3. 保存位置：`Assets/EngineTwin/Settings/`

### 步骤 2：设置渲染管线

1. **Edit** → **Project Settings** → **Graphics**
2. **Scriptable Render Pipeline Settings** → 拖入刚创建的 `EngineTwin_URP`
3. **Edit** → **Project Settings** → **Quality**
4. 每个质量级别的 **Rendering** → 拖入 `EngineTwin_URP`

### 步骤 3：创建全息材质

1. **Project 窗口** → `Assets/EngineTwin/Shaders/HolographicEngine.shader`
2. 右键 Shader → **Create** → **Material**
3. 命名为：`HolographicEngine`
4. 保存位置：`Assets/EngineTwin/Materials/`

### 步骤 4：设置材质参数

在 Inspector 中设置：
```
Base Color: (0.2, 0.6, 1, 1)  RGB: 51, 153, 255
Edge Color: (0, 1, 1, 1)      RGB: 0, 255, 255
Fresnel Power: 3
Alpha: 0.5
Scan Speed: 1
Scan Width: 0.1
Scan Color: (0, 1, 1, 1)      RGB: 0, 255, 255
Flow Speed: 1
Flow Intensity: 0.5
Grid Size: 0.1
Grid Thickness: 0.02
Grid Color: (0.2, 0.8, 1, 0.3) RGB: 51, 204, 255, Alpha: 76
```

### 步骤 5：配置 Bootstrap

1. 打开场景中的 **EngineTwin** 对象
2. 找到 **HolographicDisplay** 组件
3. **Holographic Material** → 拖入刚创建的材质

---

## ✅ 验证配置

### 检查 1：渲染管线
```
Edit → Project Settings → Graphics
查看：Scriptable Render Pipeline Settings 不为 None
```

### 检查 2：Shader 编译
```
打开 Console 窗口
确保没有 Shader 编译错误
```

### 检查 3：材质加载
```
选择 HolographicEngine.mat
Inspector 显示 Shader: EngineTwin/HolographicEngine
```

### 检查 4：运行测试
```
1. 运行场景
2. 控制面板 → 全息效果 → 全息模式 ✓
3. 发动机应该变为半透明青色，带流光效果
```

---

## 🔧 常见问题

### 问题 1：Shader 编译错误
**症状：** Console 显示 Shader error

**解决：**
```
1. Window → Package Manager
2. 搜索 "Universal RP"
3. 确保已安装（Install 或 Update）
4. 重启 Unity
```

### 问题 2：材质显示粉红色
**症状：** 模型变成粉红色

**解决：**
```
1. 检查 Graphics Settings 是否设置 URP
2. 检查 Shader 是否编译成功
3. 重新导入 Shader（右键 → Reimport）
```

### 问题 3：全息模式无效果
**症状：** 点击开关没有变化

**解决：**
```
1. 检查 HolographicDisplay 组件的 holographicMaterial 是否赋值
2. 检查 Console 是否有错误
3. 确认场景中有 EngineTwin 对象和发动机模型
```

### 问题 4：半透明不显示
**症状：** 全息模式下完全不透明

**解决：**
```
1. 检查材质的 Alpha 参数（应该 < 1）
2. 检查 Shader 的 Blend 模式
3. 确认 URP 支持透明渲染
```

---

## 📊 URP 设置推荐

### Renderer 设置
```
Rendering Path: Forward
Depth Priming Mode: Auto
Opaque Texture: On (如果需要折射效果)
```

### Quality 设置
```
Anti Aliasing: SMAA (推荐) 或 TAA
HDR: On
MSAA: 4x (性能允许的话)
```

### Camera 设置
```
Rendering Path: Use Pipeline Settings
Anti-aliasing: Use Pipeline Settings
```

---

## 🎨 全息效果调优

### 科技感强（推荐）
```
Alpha: 0.4
Edge Color: (0, 1, 1) 青色
Scan Speed: 1.2
Flow Intensity: 0.6
Grid Color: (0.2, 0.8, 1, 0.4)
```

### 柔和透明
```
Alpha: 0.6
Edge Color: (0.5, 0.8, 1) 淡蓝
Scan Speed: 0.8
Flow Intensity: 0.3
Grid Color: (0.3, 0.7, 1, 0.2)
```

### 炫酷高亮
```
Alpha: 0.3
Edge Color: (0, 1, 1) 亮青
Scan Speed: 1.5
Flow Intensity: 0.8
Grid Color: (0, 1, 1, 0.5)
```

---

## 📦 所需文件检查清单

确保以下文件存在：

```
✓ Assets/EngineTwin/Shaders/HolographicEngine.shader
✓ Assets/EngineTwin/Materials/HolographicEngine.mat
✓ Assets/EngineTwin/Settings/EngineTwin_URP.asset
✓ Assets/Scripts/EngineTwin/View/Effects/HolographicDisplay.cs
✓ Assets/Scripts/EngineTwin/View/Effects/TechTextureGenerator.cs
✓ Assets/Scripts/EngineTwin/Editor/URPSetupTool.cs
```

---

## 🆘 需要帮助？

如果配置遇到问题：

1. **检查 Unity 版本**：建议 2021.3 LTS 或更高
2. **检查 URP 包**：Package Manager 中确认已安装
3. **查看 Console**：记录所有错误信息
4. **重启 Unity**：有时候简单重启就能解决问题

---

## ✅ 配置成功标志

当您看到以下效果时，说明配置成功：

1. ✓ 场景正常运行，没有错误
2. ✓ 点击"全息模式"，发动机变为半透明
3. ✓ 可以看到青色边缘光
4. ✓ 有扫描线上下移动
5. ✓ 表面有流光闪烁
6. ✓ 可以透过外壳看到内部结构

恭喜！您已成功配置 URP 全息效果！🎉

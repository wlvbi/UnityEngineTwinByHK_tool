# V8 发动机数字孪生 - URP 全息科技效果

## 🎨 新增视觉效果

### 1. 全息显示模式
半透明的发动机模型，带有科技感的流光效果，适合数字孪生展示。

**主要特性：**
- ✨ **Fresnel 边缘光**：边缘发光效果，突出轮廓
- 🌊 **流光动画**：表面流动的光线纹理
- 📡 **扫描线**：垂直扫描光束，持续移动
- 🔲 **网格叠加**：科技感网格纹理
- 🔵 **半透明**：可透视发动机内部结构

---

## 📁 新增文件

### Shader
```
Assets/EngineTwin/Shaders/
└── HolographicEngine.shader       # URP 全息效果 Shader
```

### 脚本
```
Assets/Scripts/EngineTwin/View/Effects/
├── HolographicDisplay.cs          # 全息显示控制器
└── TechTextureGenerator.cs        # 科技感纹理生成器
```

---

## 🎮 使用方式

### 控制面板操作
在底部控制栏找到"全息效果"区域：

| 控件 | 功能 |
|------|------|
| **全息模式** 开关 | 切换全息显示模式 |

### 效果参数（Inspector 调整）

**HolographicDisplay 组件：**
- `Intensity` (0-1) - 整体效果强度
- `Alpha` (0-1) - 半透明度
- `Enable Scan Line` - 启用扫描线
- `Scan Speed` - 扫描线速度
- `Scan Color` - 扫描线颜色
- `Edge Color` - 边缘光颜色
- `Fresnel Power` - 边缘光强度
- `Enable Flow` - 启用流光
- `Flow Speed` - 流光速度
- `Flow Intensity` - 流光强度
- `Enable Grid` - 启用网格
- `Grid Size` - 网格大小
- `Grid Color` - 网格颜色

---

## 🎨 Shader 特性

### 1. Fresnel 边缘光
```hlsl
float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
```
基于视角和法线的边缘发光，角度越大越亮。

### 2. 扫描线效果
```hlsl
float scanPos = frac(_Time.y * _ScanSpeed);
float scanLine = smoothstep(_ScanWidth, 0, abs(input.positionWS.y - scanPos * 2 - 1));
```
垂直移动的扫描光束，自动循环。

### 3. 网格叠加
```hlsl
float2 grid = abs(frac(input.positionWS.xz / _GridSize) - 0.5);
float gridPattern = step(_GridThickness, min(grid.x, grid.y));
```
基于世界空间坐标的网格纹理。

### 4. 流光动画
```hlsl
float2 flowUV = input.positionWS.xz * 0.5 + _Time.y * _FlowSpeed * 0.1;
half4 flow = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, flowUV);
```
使用纹理采样实现流动光线。

---

## 🔧 技术实现

### 材质替换系统
```csharp
// 保存原始材质
originalMaterials[renderer] = renderer.sharedMaterials;

// 应用全息材质
renderer.sharedMaterials = new Material[] { holographicInstance };

// 恢复原始材质
renderer.sharedMaterials = originalMaterials[renderer];
```

### 纹理生成
动态生成科技感纹理，无需外部资源：
- **流光纹理**：对角线 + 噪声
- **六边形网格**：程序化六边形图案
- **数字雨**：黑客帝国风格
- **扫描线**：水平线条

---

## 🎯 应用场景

### 1. 展示模式
开启全息模式，展示发动机的科技感：
- 半透明可以看到内部结构
- 流光效果突出运动感
- 扫描线增加科技氛围

### 2. 教学演示
配合拆装功能：
- 全息模式下拆卸部件
- 清晰看到内部连接
- 科技感增强吸引力

### 3. 故障诊断
配合传感器数据：
- 全息模式突出问题部件
- 半透明看到内部状态
- 扫描线模拟检测过程

---

## 🎨 视觉效果预览

### 正常模式
```
┌─────────────────────────┐
│   ███████████████████   │  实心发动机模型
│   ███████████████████   │  标准金属材质
│   ███████████████████   │  
└─────────────────────────┘
```

### 全息模式
```
┌─────────────────────────┐
│   ╔═══════════════╗     │  半透明轮廓
│   ║ ░░░░░░░░░░░░░ ║ ╍╍  │  边缘发光
│ ╍╍║ ▒▒▒▒▒▒▒▒▒▒▒▒▒ ║     │  扫描线 ↓
│   ╚═══════════════╝     │  流光动画 ~
└─────────────────────────┘
```

---

## ⚙️ 性能优化

### 1. 材质实例
使用单一材质实例，所有部件共享：
```csharp
holographicInstance = new Material(holographicMaterial);
```

### 2. 条件渲染
只在全息模式启用时更新材质属性：
```csharp
if (isHolographicMode) {
    UpdateMaterialProperties();
}
```

### 3. 纹理生成
只在初始化时生成一次：
```csharp
Texture2D flowTexture = TechTextureGenerator.GenerateFlowTexture(256);
```

---

## 🚀 未来扩展

### 可能的增强：
- [ ] 多种全息配色方案（蓝色、绿色、红色）
- [ ] 部件单独控制全息强度
- [ ] 数据流可视化（传感器数据流动）
- [ ] 全息投影动画（开启/关闭过渡）
- [ ] 粒子束连接部件（显示数据流）
- [ ] X射线模式（只显示内部结构）
- [ ] 热力图叠加（温度可视化）

---

## 📊 Shader 属性完整列表

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| _BaseColor | Color | (0.2, 0.6, 1, 1) | 基础颜色 |
| _EdgeColor | Color | (0, 1, 1, 1) | 边缘光颜色 |
| _FresnelPower | Float | 3 | Fresnel 指数 |
| _Alpha | Float | 0.5 | 透明度 |
| _ScanSpeed | Float | 1 | 扫描速度 |
| _ScanWidth | Float | 0.1 | 扫描线宽度 |
| _ScanColor | Color | (0, 1, 1, 1) | 扫描线颜色 |
| _FlowMap | Texture2D | - | 流光贴图 |
| _FlowSpeed | Float | 1 | 流光速度 |
| _FlowIntensity | Float | 0.5 | 流光强度 |
| _GridSize | Float | 0.1 | 网格大小 |
| _GridThickness | Float | 0.02 | 网格线粗细 |
| _GridColor | Color | (0.2, 0.8, 1, 0.3) | 网格颜色 |

---

## ✅ 集成检查清单

- [x] URP Shader 创建
- [x] HolographicDisplay 控制器
- [x] 纹理生成器
- [x] Bootstrap 集成
- [x] Dashboard UI 集成
- [x] 材质替换系统
- [x] 原始材质恢复
- [ ] 默认材质资源（可选）
- [ ] 配置预设（可选）

---

## 🎉 完成状态

全息科技效果已完全集成到数字孪生系统！

**测试步骤：**
1. 确保项目使用 URP 渲染管线
2. 运行场景
3. 点击控制面板的"全息模式"开关
4. 观察发动机变为半透明科技感显示
5. 调整 Inspector 中的参数查看效果

**建议配置：**
- Alpha: 0.5（适中透明度）
- Scan Speed: 1.0（舒适的扫描速度）
- Edge Color: 青色 (0, 1, 1)（经典科技感）
- Grid Size: 0.1（合适的网格密度）

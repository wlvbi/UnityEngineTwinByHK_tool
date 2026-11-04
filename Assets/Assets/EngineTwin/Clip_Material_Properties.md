# 剖切材质属性复制说明

## ✅ 完整属性保留

剖切功能现在会完整保留原始材质的所有属性：

### 基础属性
- ✅ 基础颜色 (_BaseColor / _Color)
- ✅ 基础纹理 (_BaseMap / _MainTex)
- ✅ 纹理缩放和偏移 (Tiling & Offset)

### PBR 属性
- ✅ 金属度 (_Metallic)
- ✅ 光滑度 (_Smoothness / _Glossiness)
- ✅ 金属度/光滑度贴图 (_MetallicGlossMap)

### 高级贴图
- ✅ 法线贴图 (_BumpMap)
- ✅ 法线强度 (_BumpScale)
- ✅ 遮蔽贴图 (_OcclusionMap)
- ✅ 遮蔽强度 (_OcclusionStrength)
- ✅ 自发光颜色 (_EmissionColor)
- ✅ 自发光贴图 (_EmissionMap)

### 高光属性
- ✅ 高光颜色 (_SpecColor)
- ✅ 高光贴图 (_SpecGlossMap)

### 关键字和模式
- ✅ 法线贴图启用 (_NORMALMAP)
- ✅ 金属贴图启用 (_METALLICSPECGLOSSMAP)
- ✅ 遮蔽贴图启用 (_OCCLUSIONMAP)
- ✅ 自发光启用 (_EMISSION)
- ✅ 高光高光开关 (_SPECULARHIGHLIGHTS_OFF)
- ✅ 环境反射开关 (_ENVIRONMENTREFLECTIONS_OFF)
- ✅ 高光工作流 (_SPECULAR_SETUP)

### 渲染设置
- ✅ 渲染队列 (Render Queue)
- ✅ 所有浮点和向量参数

---

## 🔄 工作原理

### 原始材质 (URP Lit)
```
颜色: (0.7, 0.7, 0.75)
金属度: 0.9
光滑度: 0.6
法线贴图: Normal_01
纹理缩放: (2, 2)
```

### 转换为剖切材质 (URPLitWithClip)
```
Shader: URPLitWithClip
颜色: (0.7, 0.7, 0.75)      ✅ 保留
金属度: 0.9                  ✅ 保留
光滑度: 0.6                  ✅ 保留
法线贴图: Normal_01          ✅ 保留
纹理缩放: (2, 2)             ✅ 保留
+ 剪裁平面功能               ✅ 新增
```

---

## 🎯 使用方法

### 开启剖切
```
控制面板 → 剖切 → 开启
↓
自动转换材质
所有属性保留
外观完全一致
只增加剖切功能
```

### 关闭剖切
```
控制面板 → 剖切 → 关闭
↓
恢复原始材质
所有属性恢复
```

---

## 📋 兼容性

### 支持的源材质
- ✅ URP/Lit
- ✅ URP/Simple Lit
- ✅ 自定义 PBR 材质
- ✅ 任何包含标准 PBR 属性的材质

### 转换目标
- ✅ EngineTwin/URPLitWithClip

---

## 🔍 属性复制优先级

1. **显式复制**（优先）
   - 基础颜色、纹理
   - 金属度、光滑度
   - 法线、遮蔽、自发光

2. **关键字复制**
   - 功能开关
   - 渲染模式

3. **通用复制**（兜底）
   - 所有浮点参数
   - 所有向量参数
   - 跳过 _Engine 开头的参数（剪裁平面专用）

---

## ⚠️ 注意事项

### 不会复制的内容
- ❌ Shader 关键字（自动由材质属性决定）
- ❌ _EngineClipPlane（剪裁平面专用）
- ❌ _EngineClipEnabled（剪裁平面专用）

### 特殊处理
- _Color → _BaseColor（自动转换）
- _MainTex → _BaseMap（自动转换）
- _Glossiness → _Smoothness（自动转换）

---

## 🎨 效果对比

### 剖切前
```
使用: URP/Lit
外观: 完整的 PBR 效果
功能: 无剪裁
```

### 剖切后
```
使用: EngineTwin/URPLitWithClip
外观: 完全相同的 PBR 效果 ✅
功能: + 剪裁平面 ✅
```

---

## 🎉 完成

现在开启剖切功能时：
- ✅ 外观完全保持不变
- ✅ 所有材质属性保留
- ✅ 所有贴图保留
- ✅ 只增加剪裁功能

测试方法：
1. 运行场景
2. 观察发动机外观
3. 开启剖切
4. 外观应该完全一致，只是可以看到内部了

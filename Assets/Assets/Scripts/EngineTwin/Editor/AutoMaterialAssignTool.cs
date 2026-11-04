using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace EngineTwin
{
    /// <summary>
    /// 自动材质分配工具：智能为发动机部件分配专业材质
    /// </summary>
    public static class AutoMaterialAssignTool
    {
        [MenuItem("EngineTwin/Auto Assign Professional Materials", false, 301)]
        public static void AutoAssignProfessionalMaterials()
        {
            if (!EditorUtility.DisplayDialog("自动分配专业材质",
                "将根据部件名称智能分配专业 PBR 材质\n\n" +
                "匹配规则：\n" +
                "- 包含 'block/cylinder/crank' → 铸铁\n" +
                "- 包含 'head/cover' → 铝合金\n" +
                "- 包含 'exhaust/manifold' → 不锈钢\n" +
                "- 包含 'intake/plastic' → 黑色塑料\n" +
                "- 包含 'oil/filter' → 蓝色涂漆\n" +
                "- 等等...\n\n" +
                "是否继续？",
                "继续", "取消"))
            {
                return;
            }

            Debug.Log("[Auto Assign] 开始自动分配专业材质...");

            // 加载所有专业材质
            Material matCastIron = LoadMaterial("Pro_CastIron");
            Material matAluminum = LoadMaterial("Pro_PolishedAluminum");
            Material matSteel = LoadMaterial("Pro_StainlessSteel");
            Material matPlasticBlack = LoadMaterial("Pro_PlasticBlack");
            Material matRubber = LoadMaterial("Pro_Rubber");
            Material matBrass = LoadMaterial("Pro_Brass");
            Material matCopper = LoadMaterial("Pro_Copper");
            Material matPaintBlack = LoadMaterial("Pro_PaintBlack");
            Material matPaintBlue = LoadMaterial("Pro_PaintBlue");

            // 检查材质是否加载成功
            if (matCastIron == null)
            {
                EditorUtility.DisplayDialog("错误",
                    "未找到专业材质！\n\n" +
                    "请先执行: EngineTwin → Apply Professional Rendering",
                    "确定");
                return;
            }

            // 默认材质
            Material defaultMat = matCastIron;

            // 遍历场景中所有渲染器
            Renderer[] renderers = Object.FindObjectsOfType<Renderer>(true);
            int assignedCount = 0;
            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("=== 材质分配报告 ===\n");

            foreach (var renderer in renderers)
            {
                string objName = renderer.name.ToLower();
                Material selectedMat = SelectMaterial(objName,
                    matCastIron, matAluminum, matSteel, matPlasticBlack,
                    matRubber, matBrass, matCopper, matPaintBlack, matPaintBlue,
                    defaultMat);

                // 应用材质
                Material[] mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = selectedMat;
                }
                renderer.sharedMaterials = mats;

                assignedCount++;
                report.AppendLine($"{renderer.name} → {selectedMat.name}");
            }

            report.AppendLine($"\n共分配 {assignedCount} 个对象");
            Debug.Log(report.ToString());

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("分配完成",
                $"已为 {assignedCount} 个对象分配专业材质！\n\n" +
                "查看 Console 了解详细分配情况。",
                "确定");
        }

        static Material LoadMaterial(string name)
        {
            string path = $"Assets/EngineTwin/Materials/Professional/{name}.mat";
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static Material SelectMaterial(string objName,
            Material matCastIron, Material matAluminum, Material matSteel,
            Material matPlasticBlack, Material matRubber, Material matBrass,
            Material matCopper, Material matPaintBlack, Material matPaintBlue,
            Material defaultMat)
        {
            // 缸体、曲轴箱 - 铸铁
            if (objName.Contains("block") || objName.Contains("cylinder") ||
                objName.Contains("crank") || objName.Contains("case"))
            {
                return matCastIron;
            }

            // 缸盖、罩盖 - 抛光铝合金
            if (objName.Contains("head") || objName.Contains("cover") ||
                objName.Contains("lid") || objName.Contains("cap"))
            {
                return matAluminum;
            }

            // 排气管、歧管 - 不锈钢
            if (objName.Contains("exhaust") || objName.Contains("manifold") ||
                objName.Contains("pipe") || objName.Contains("header"))
            {
                return matSteel;
            }

            // 进气、塑料件 - 黑色塑料
            if (objName.Contains("intake") || objName.Contains("plastic") ||
                objName.Contains("duct") || objName.Contains("hose"))
            {
                return matPlasticBlack;
            }

            // 密封件、垫片 - 橡胶
            if (objName.Contains("gasket") || objName.Contains("seal") ||
                objName.Contains("rubber") || objName.Contains("o-ring"))
            {
                return matRubber;
            }

            // 螺栓、连接件 - 黄铜
            if (objName.Contains("bolt") || objName.Contains("screw") ||
                objName.Contains("nut") || objName.Contains("fitting"))
            {
                return matBrass;
            }

            // 铜管、散热器 - 铜
            if (objName.Contains("copper") || objName.Contains("radiator") ||
                objName.Contains("cooler"))
            {
                return matCopper;
            }

            // 机油滤清器、滤芯 - 蓝色涂漆
            if (objName.Contains("oil") || objName.Contains("filter"))
            {
                return matPaintBlue;
            }

            // 皮带轮、轮子 - 黑色涂漆
            if (objName.Contains("pulley") || objName.Contains("wheel") ||
                objName.Contains("belt"))
            {
                return matPaintBlack;
            }

            // 发电机、泵 - 黑色塑料
            if (objName.Contains("alternator") || objName.Contains("pump") ||
                objName.Contains("generator"))
            {
                return matPlasticBlack;
            }

            // 火花塞 - 黄铜
            if (objName.Contains("spark") || objName.Contains("plug"))
            {
                return matBrass;
            }

            // 活塞 - 铝合金
            if (objName.Contains("piston"))
            {
                return matAluminum;
            }

            // 连杆 - 不锈钢
            if (objName.Contains("rod") || objName.Contains("connecting"))
            {
                return matSteel;
            }

            // 齿轮、链条 - 不锈钢
            if (objName.Contains("gear") || objName.Contains("chain") ||
                objName.Contains("sprocket"))
            {
                return matSteel;
            }

            // 阀门 - 不锈钢
            if (objName.Contains("valve"))
            {
                return matSteel;
            }

            // 凸轮轴 - 铸铁
            if (objName.Contains("cam") || objName.Contains("shaft"))
            {
                return matCastIron;
            }

            // 默认：铸铁
            return defaultMat;
        }

        [MenuItem("EngineTwin/Show Material Assignment Rules", false, 302)]
        public static void ShowMaterialRules()
        {
            string rules = @"=== 自动材质分配规则 ===

【铸铁 - Pro_CastIron】
深灰色，高金属度，粗糙表面
关键词: block, cylinder, crank, case, cam, shaft

【抛光铝合金 - Pro_PolishedAluminum】
浅灰色，高金属度，光滑表面
关键词: head, cover, lid, cap, piston

【不锈钢 - Pro_StainlessSteel】
中灰色，高金属度，高光泽
关键词: exhaust, manifold, pipe, rod, gear, chain, valve

【黑色塑料 - Pro_PlasticBlack】
黑色，低金属度，半光泽
关键词: intake, plastic, duct, hose, alternator, pump

【橡胶 - Pro_Rubber】
深黑色，无金属度，哑光
关键词: gasket, seal, rubber, o-ring

【黄铜 - Pro_Brass】
金色，高金属度，光亮
关键词: bolt, screw, nut, fitting, spark, plug

【铜 - Pro_Copper】
橙铜色，高金属度，中等光泽
关键词: copper, radiator, cooler

【黑色涂漆 - Pro_PaintBlack】
黑色，低金属度，光泽
关键词: pulley, wheel, belt

【蓝色涂漆 - Pro_PaintBlue】
深蓝色，低金属度，光泽
关键词: oil, filter

=== 使用方法 ===

1. 确保部件命名包含关键词（英文）
2. 执行: EngineTwin → Auto Assign Professional Materials
3. 查看 Console 了解分配结果
4. 手动调整不满意的部件

=== 手动调整 ===

如果自动分配不准确：
1. 选中部件
2. Inspector → Materials
3. 从 Assets/EngineTwin/Materials/Professional/ 拖入正确材质
";

            Debug.Log(rules);
            EditorUtility.DisplayDialog("材质分配规则", rules, "确定");
        }
    }
}
#endif

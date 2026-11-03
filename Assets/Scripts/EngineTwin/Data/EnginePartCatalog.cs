using System;
using System.Collections.Generic;
using UnityEngine;

namespace EngineTwin
{
    [Serializable]
    public class EnginePartInfo
    {
        [Tooltip("唯一标识")] public string id;
        [Tooltip("显示名称")] public string displayName;
        [TextArea(2, 4)] public string description;

        [Tooltip("按网格物体名前缀匹配（列表靠前的部件优先匹配）")]
        public string[] meshPrefixes;

        [Tooltip("该部件绑定的传感器（第一个会显示在部件提示标签上）")]
        public SensorId[] sensors;

        [Tooltip("关键部件：标签默认显示（HK_TipGroup01）；非关键部件标签默认隐藏（HK_TipGroup02）")]
        public bool keyPart = true;

        [Tooltip("标签引出方向（发动机根节点局部坐标，零向量 = 自动）")]
        public Vector3 tipDirection;

        public EnginePartInfo(string id, string displayName, string description, string[] prefixes,
            SensorId[] sensors, bool keyPart, Vector3 tipDirection)
        {
            this.id = id; this.displayName = displayName; this.description = description;
            meshPrefixes = prefixes; this.sensors = sensors; this.keyPart = keyPart; this.tipDirection = tipDirection;
        }
    }

    /// <summary>部件目录：把 FBX 里的 300 多个网格归并为逻辑部件，并绑定传感器</summary>
    [CreateAssetMenu(menuName = "EngineTwin/Part Catalog", fileName = "EnginePartCatalog")]
    public class EnginePartCatalog : ScriptableObject
    {
        public List<EnginePartInfo> parts = new List<EnginePartInfo>();

        public static EnginePartCatalog CreateDefault()
        {
            var c = CreateInstance<EnginePartCatalog>();
            c.name = "EnginePartCatalog (Default)";
            c.parts = DefaultParts();
            return c;
        }

        // Unity 坐标：+Z = 发动机前端（皮带轮），-X = 右列缸（排气侧），+Y = 上
        public static List<EnginePartInfo> DefaultParts()
        {
            SensorId[] none = Array.Empty<SensorId>();
            return new List<EnginePartInfo>
            {
                new EnginePartInfo("throttle", "节气门体", "电子节气门，控制进入进气歧管的空气量，开度由油门指令驱动。",
                    new[] { "Int_TB", "Int_ETC", "Int_TPS" }, new[] { SensorId.Throttle }, true, new Vector3(0.35f, 0.8f, 1f)),
                new EnginePartInfo("intake", "进气歧管", "8 道进气流道，将空气均匀分配到各缸，内置进气压力（MAP）传感器。",
                    new[] { "Int_" }, new[] { SensorId.IntakePressure }, true, new Vector3(0.1f, 1f, -0.45f)),
                new EnginePartInfo("coils", "点火线圈", "每缸独立点火线圈（COP），点火频率与转速同步。",
                    new[] { "Coil_" }, new[] { SensorId.Rpm }, true, new Vector3(-1f, 1f, -0.35f)),
                new EnginePartInfo("valvecover", "气门室盖", "密封缸盖顶部的配气机构，防止机油外泄。",
                    new[] { "VC_" }, none, false, new Vector3(1f, 0.9f, -0.2f)),
                new EnginePartInfo("fuelrail", "燃油导轨", "向 8 个喷油器供给高压燃油，监测燃油压力。",
                    new[] { "Misc_FuelRail", "Misc_InjPlug", "Misc_InjWire" }, new[] { SensorId.FuelPressure }, true, new Vector3(0.8f, 1f, 0.25f)),
                new EnginePartInfo("harness", "线束与管路", "点火/喷油线束、PCV 曲轴箱通风管与后部软管。",
                    new[] { "Misc_Harness", "Misc_CoilLead", "Misc_RearHose", "Misc_PCV" }, none, false, new Vector3(0f, 1f, -1f)),
                new EnginePartInfo("dipstick", "机油尺", "人工检查机油液位的量尺。",
                    new[] { "Misc_Dip" }, none, false, new Vector3(-1f, 0.5f, 1f)),
                new EnginePartInfo("oilfilter", "机油滤清器", "过滤机油中的杂质，滤座处安装机油压力传感器。",
                    new[] { "Misc_OilFilter", "Misc_FilterBoss", "Misc_FilterFlute" }, new[] { SensorId.OilPressure }, true, new Vector3(-1f, -0.6f, 0.2f)),
                new EnginePartInfo("timingcover", "正时链盖", "保护正时链条与链轮的铸铝前盖。",
                    new[] { "Blk_TimingCover", "Misc_TCBolt" }, none, false, new Vector3(0.6f, 0.05f, 1f)),
                new EnginePartInfo("oilpan", "油底壳", "储存机油，内置机油温度传感器。",
                    new[] { "Blk_Pan", "Misc_PanBolt" }, new[] { SensorId.OilTemp }, true, new Vector3(0.8f, -1f, 0.2f)),
                new EnginePartInfo("block", "缸体 / 缸盖", "铸铝 90° V8 缸体与缸盖，水套温度代表整机热状态。",
                    new[] { "Blk_", "Misc_BlkBolt" }, new[] { SensorId.CoolantTemp }, true, new Vector3(-1f, 0.1f, -0.9f)),
                new EnginePartInfo("alternator", "发电机", "由皮带驱动，为电气系统供电并给蓄电池充电。",
                    new[] { "Frt_Alt", "Frt_PulleyAlt" }, new[] { SensorId.BatteryVoltage }, true, new Vector3(1f, 0.7f, 0.6f)),
                new EnginePartInfo("waterpump", "水泵", "驱动冷却液在缸体与散热器之间循环。",
                    new[] { "Frt_PulleyWP", "Frt_WP", "Frt_WaterNeck" }, new[] { SensorId.CoolantTemp }, true, new Vector3(0.15f, 0.9f, 1f)),
                new EnginePartInfo("crank", "曲轴减震轮", "曲轴前端扭振减震器，转速传感器的信号来源。",
                    new[] { "Frt_Crank" }, new[] { SensorId.Rpm }, true, new Vector3(0.25f, -0.55f, 1f)),
                new EnginePartInfo("beltdrive", "皮带与张紧轮", "多楔皮带驱动水泵与发电机，张紧轮保持皮带张力。",
                    new[] { "Frt_Belt", "Frt_Tens", "Frt_PulleyTens", "Frt_PulleyIdler" }, none, false, new Vector3(-0.9f, 0.1f, 1f)),
                new EnginePartInfo("exhaust", "排气歧管", "不锈钢 4 合 1 排气头段，安装排气温度传感器。",
                    new[] { "Exh_" }, new[] { SensorId.ExhaustTemp }, true, new Vector3(-1f, -0.15f, -0.5f)),
            };
        }
    }
}

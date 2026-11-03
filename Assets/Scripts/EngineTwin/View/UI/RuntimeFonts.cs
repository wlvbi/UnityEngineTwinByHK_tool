using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace EngineTwin
{
    /// <summary>
    /// 运行时中文字体：基于模板字体的源字体文件，在内存中创建一个「多图集」的动态字体。
    /// 不直接往工程里的字体资产（msyh SDF）追加字形——在编辑器中给持久化字体资产追加图集时，
    /// TMP 会在场景视图渲染回调里重新导入该资产，D3D12 下可能导致编辑器崩溃；单张图集也容纳不下常用汉字。
    /// </summary>
    public static class RuntimeFonts
    {
        static TMP_FontAsset cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => cached = null;

        public static TMP_FontAsset CreateFrom(TMP_FontAsset template)
        {
            if (cached != null) return cached;
            if (template == null) return null;

            Font source = template.sourceFontFile;
            if (source == null)
            {
                Debug.LogWarning($"[EngineTwin] {template.name} 没有源字体文件，继续使用原字体资产");
                return template;
            }

            TMP_FontAsset runtime = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            if (runtime == null)
            {
                Debug.LogWarning("[EngineTwin] 运行时字体创建失败，继续使用原字体资产");
                return template;
            }

            runtime.name = template.name + " (Runtime)";
            cached = runtime;
            return runtime;
        }
    }
}

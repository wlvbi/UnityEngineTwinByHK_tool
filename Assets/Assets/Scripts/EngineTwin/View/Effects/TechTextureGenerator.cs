using UnityEngine;

namespace EngineTwin
{
    /// <summary>
    /// 科技感纹理生成器：为全息效果生成流光和噪声纹理
    /// </summary>
    public static class TechTextureGenerator
    {
        /// <summary>生成流光纹理（用于全息流光效果）</summary>
        public static Texture2D GenerateFlowTexture(int size = 256)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.name = "FlowTexture";
            texture.wrapMode = TextureWrapMode.Repeat;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // 创建对角线流动纹理
                    float diagonal = (u + v) * 2f;
                    float flow = Mathf.Sin(diagonal * Mathf.PI * 4f) * 0.5f + 0.5f;

                    // 添加噪声
                    float noise = Mathf.PerlinNoise(u * 4f, v * 4f);

                    // 组合
                    float intensity = flow * 0.7f + noise * 0.3f;

                    pixels[y * size + x] = new Color(intensity, intensity, intensity, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true);

            return texture;
        }

        /// <summary>生成六边形网格纹理</summary>
        public static Texture2D GenerateHexGrid(int size = 256, float cellSize = 0.1f)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.name = "HexGridTexture";
            texture.wrapMode = TextureWrapMode.Repeat;

            Color[] pixels = new Color[size * size];
            Color lineColor = new Color(0.2f, 0.8f, 1f, 0.5f);
            Color bgColor = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size / cellSize;
                    float v = (float)y / size / cellSize;

                    // 简化的六边形检测（近似）
                    float gridU = Mathf.Abs(Mathf.Repeat(u, 1f) - 0.5f);
                    float gridV = Mathf.Abs(Mathf.Repeat(v, 1f) - 0.5f);
                    float dist = Mathf.Max(gridU, gridV);

                    // 边缘检测
                    bool isEdge = dist > 0.45f;

                    pixels[y * size + x] = isEdge ? lineColor : bgColor;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true);

            return texture;
        }

        /// <summary>生成数字雨纹理（黑客帝国风格）</summary>
        public static Texture2D GenerateDigitalRain(int width = 256, int height = 512)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true);
            texture.name = "DigitalRainTexture";
            texture.wrapMode = TextureWrapMode.Repeat;

            Color[] pixels = new Color[width * height];
            Color rainColor = new Color(0f, 1f, 0.5f, 1f);

            // 随机列
            System.Random random = new System.Random(12345);

            for (int x = 0; x < width; x++)
            {
                if (random.NextDouble() > 0.7f) // 30% 的列有数字雨
                {
                    int rainLength = random.Next(10, 50);
                    int startY = random.Next(0, height);

                    for (int i = 0; i < rainLength && startY + i < height; i++)
                    {
                        int y = (startY + i) % height;
                        float alpha = 1f - (float)i / rainLength; // 头部亮，尾部暗
                        pixels[y * width + x] = rainColor * alpha;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true);

            return texture;
        }

        /// <summary>生成扫描线纹理</summary>
        public static Texture2D GenerateScanLines(int size = 256, int lineCount = 32)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "ScanLinesTexture";
            texture.wrapMode = TextureWrapMode.Repeat;

            Color[] pixels = new Color[size * size];
            Color lineColor = new Color(0.3f, 0.9f, 1f, 0.3f);
            Color bgColor = new Color(0f, 0f, 0f, 0f);

            int lineSpacing = size / lineCount;

            for (int y = 0; y < size; y++)
            {
                bool isLine = (y % lineSpacing) < 2;
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = isLine ? lineColor : bgColor;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false);

            return texture;
        }
    }
}

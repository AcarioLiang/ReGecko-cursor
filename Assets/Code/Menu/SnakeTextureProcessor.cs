using UnityEngine;


public class SnakeTextureProcessor : MonoBehaviour
{
    [Header("原始纹理")]
    public Texture2D originalTexture;

    [Header("九宫格参数 - 垂直布局")]
    public int leftBorderWidth = 16;    // 左边框宽度
    public int rightBorderWidth = 16;   // 右边框宽度
    public int headHeight = 16;         // 头部高度
    public int tailHeight = 16;         // 尾部高度
    public int bodyStartY = 20;         // 身体纹理采样起始Y位置
    public int bodyHeight = 10;         // 身体重复区域高度

    [ContextMenu("生成垂直九宫格纹理")]
    public void GenerateVerticalNineSliceTexture()
    {
        if (!ValidateInputs()) return;

        int width = originalTexture.width;
        int height = originalTexture.height;

        Texture2D newTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixelColor = GetPixelForVerticalLayout(x, y, width, height);
                pixels[y * width + x] = pixelColor;
            }
        }

        newTexture.SetPixels(pixels);
        newTexture.Apply();

        // 保存纹理
        byte[] bytes = newTexture.EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + "/SnakeNineSlice.png", bytes);

        Debug.Log("九宫格纹理已生成：" + Application.dataPath + "/SnakeNineSlice.png");
    }

    private Color GetPixelForVerticalLayout(int x, int y, int width, int height)
    {
        // Y方向：头部在上，尾部在下，中间是身体
        if (y >= height - headHeight)
        {
            // 头部区域（纹理上方）：直接复制
            return originalTexture.GetPixel(x, y);
        }
        else if (y < tailHeight)
        {
            // 尾部区域（纹理下方）：从原图尾部复制
            int originalY = tailHeight - 1 - y;
            return originalTexture.GetPixel(x, originalY);
        }
        else
        {
            // 身体区域（中间）：循环采样
            int offsetInBody = (y - tailHeight) % bodyHeight;
            int sampleY = bodyStartY + offsetInBody;
            return originalTexture.GetPixel(x, sampleY);
        }
    }

    private bool ValidateInputs()
    {
        if (originalTexture == null)
        {
            Debug.LogError("请设置原始纹理！");
            return false;
        }

        if (headHeight + tailHeight >= originalTexture.height)
        {
            Debug.LogError($"头部高度({headHeight}) + 尾部高度({tailHeight}) 不能大于等于纹理高度({originalTexture.height})！");
            return false;
        }

        return true;
    }
}
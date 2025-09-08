using UnityEngine;


public class SnakeTextureProcessor : MonoBehaviour
{
    [Header("ԭʼ����")]
    public Texture2D originalTexture;

    [Header("�Ź������ - ��ֱ����")]
    public int leftBorderWidth = 16;    // ��߿���
    public int rightBorderWidth = 16;   // �ұ߿���
    public int headHeight = 16;         // ͷ���߶�
    public int tailHeight = 16;         // β���߶�
    public int bodyStartY = 20;         // �������������ʼYλ��
    public int bodyHeight = 10;         // �����ظ�����߶�

    [ContextMenu("���ɴ�ֱ�Ź�������")]
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

        // ��������
        byte[] bytes = newTexture.EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + "/SnakeNineSlice.png", bytes);

        Debug.Log("�Ź������������ɣ�" + Application.dataPath + "/SnakeNineSlice.png");
    }

    private Color GetPixelForVerticalLayout(int x, int y, int width, int height)
    {
        // Y����ͷ�����ϣ�β�����£��м�������
        if (y >= height - headHeight)
        {
            // ͷ�����������Ϸ�����ֱ�Ӹ���
            return originalTexture.GetPixel(x, y);
        }
        else if (y < tailHeight)
        {
            // β�����������·�������ԭͼβ������
            int originalY = tailHeight - 1 - y;
            return originalTexture.GetPixel(x, originalY);
        }
        else
        {
            // ���������м䣩��ѭ������
            int offsetInBody = (y - tailHeight) % bodyHeight;
            int sampleY = bodyStartY + offsetInBody;
            return originalTexture.GetPixel(x, sampleY);
        }
    }

    private bool ValidateInputs()
    {
        if (originalTexture == null)
        {
            Debug.LogError("������ԭʼ����");
            return false;
        }

        if (headHeight + tailHeight >= originalTexture.height)
        {
            Debug.LogError($"ͷ���߶�({headHeight}) + β���߶�({tailHeight}) ���ܴ��ڵ�������߶�({originalTexture.height})��");
            return false;
        }

        return true;
    }
}
using UnityEngine;
using Unity.Barracuda;
using UnityEngine.UI;

public class DepthSensingFromChatgpt : MonoBehaviour
{
    public NNModel modelAsset;
    public RawImage outputDisplay;

    private Model runtimeModel;
    private IWorker worker;
    private WebCamTexture webcam;

    void Start()
    {
        // モデルのロード
        runtimeModel = ModelLoader.Load(modelAsset);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, runtimeModel);

        // Webカメラ起動
        webcam = new WebCamTexture(320, 240);
        webcam.Play();
    }

    void Update()
    {
        if (!webcam.didUpdateThisFrame) return;

        Texture2D inputTex = new Texture2D(webcam.width, webcam.height);
        inputTex.SetPixels(webcam.GetPixels());
        inputTex.Apply();

        Texture2D resized = ResizeTexture(inputTex, 256, 256);

        using (Tensor inputTensor = TextureToTensor(resized))
        {
            worker.Execute(inputTensor);
            using (Tensor output = worker.PeekOutput())
            {
                Debug.Log($"Output shape: {output.shape}");
                float[] data = output.ToReadOnlyArray();

                float min = float.MaxValue;
                float max = float.MinValue;

                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] < min) min = data[i];
                    if (data[i] > max) max = data[i];
                }

                Debug.Log($"Output min: {min}, max: {max}");

                Texture2D depthTex = new Texture2D(output.width, output.height);

                for (int y = 0; y < output.height; y++)
                {
                    for (int x = 0; x < output.width; x++)
                    {
                        float val = (data[y * output.width + x] - min) / (max - min);
                        depthTex.SetPixel(x, y, new Color(val, val, val));
                    }
                }
                depthTex.Apply();
                outputDisplay.texture = depthTex;
            }
        }
    }
    // 前処理：Texture2DのRGBをfloat[3][H][W]に変換し、0-1に正規化
    Tensor TextureToTensor(Texture2D tex)
    {
        int width = tex.width;
        int height = tex.height;
        Color[] pixels = tex.GetPixels();
        float[,,,] tensorData = new float[1, 3, height, width]; // NCHW

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c = pixels[y * width + x];
                // 0-1に正規化済み（Colorはもともと0-1）
                tensorData[0, 0, y, x] = c.r; // R
                tensorData[0, 1, y, x] = c.g; // G
                tensorData[0, 2, y, x] = c.b; // B
            }
        }

        return new Tensor(1, height, width, 3, tensorData);
    }

    Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        Texture2D result = new Texture2D(width, height);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

    void OnDestroy()
    {
        webcam.Stop();
        worker.Dispose();
    }
}
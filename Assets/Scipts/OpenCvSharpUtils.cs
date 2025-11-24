// OpenCvSharp を使った顔検出＋AgeEstimator 連携サンプル
// 必要DLL：OpenCvSharp4.dll, OpenCvSharp4.runtime.win.dll などを
// Assets/Plugins に配置し、StreamingAssets/haarcascade_frontalface_alt.xml を用意してください。

using System.Runtime.InteropServices;
using System;
using OpenCvSharp;
using UnityEngine;

/// <summary>
/// Texture2D と Mat の相互変換ユーティリティ
/// </summary>
public static class OpenCvSharpUtils
{
    /// <summary>
    /// RGB24 テクスチャの生データを Mat(CV_8UC3) にコピー
    /// </summary>
    public static Mat Texture2DToMat(Texture2D tex)
    {
        // RawTextureData で生バイト列として取得
        byte[] raw = tex.GetRawTextureData();
        int width = tex.width;
        int height = tex.height;

        // Mat を生成（3チャンネル、1ピクセル = 3バイト）
        Mat mat = new Mat(height, width, MatType.CV_8UC3);

        // ネイティブメモリに直接コピー
        IntPtr dstPtr = mat.Data;
        Marshal.Copy(raw, 0, dstPtr, raw.Length);

        // Unity の Texture2D は RGB、OpenCV は BGR 前提なので変換
        Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2BGR);
        return mat;
    }

    /// <summary>
    /// Mat(CV_8UC3) を RGB24 テクスチャにコピー
    /// </summary>
    public static Texture2D MatToTexture2D(Mat mat)
    {
        // BGR → RGB
        Cv2.CvtColor(mat, mat, ColorConversionCodes.BGR2RGB);
        Texture2D tex = new Texture2D(mat.Width, mat.Height, TextureFormat.RGB24, false);
        byte[] data = mat.ToBytes();
        tex.LoadRawTextureData(data);
        tex.Apply();
        return tex;
    }
}
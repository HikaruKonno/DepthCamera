using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Barracuda;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FaceDetector:
///  - WebCamTexture から毎フレームキャプチャ
///  - AgeEstimator で推論し、UI Text や Debug.Log で表示
/// </summary>
public class FaceDetectorONNX : MonoBehaviour
{
    [Header("Estimator Reference")]
    [Tooltip("Inspector で設定した AgeEstimator コンポーネント")]
    public AgeEstimator ageEstimator;

    [Header("UI Elements")]
    [Tooltip("推論結果を表示する UI Text (Canvas 上に配置)")]
    public Text resultText;

    private WebCamTexture camTexture;
    private Texture2D snap;

    void Start()
    {
        // カメラ初期化
        camTexture = new WebCamTexture();
        camTexture.Play();

        // スナップ用 Texture2D
        snap = new Texture2D(camTexture.width, camTexture.height);
        // UI 初期化
        if (resultText != null)
        {
            resultText.text = "Estimating...";
        }
    }

    void Update()
    {
        // 毎フレーム、カメラ映像をキャプチャ
        // didUpdateThisFrame 「今フレームで映像が更新された」フラグ。
        if (!camTexture.didUpdateThisFrame)
        {
            return;
        }

        // 1. WebCamTexture からピクセルを Texture2D にコピー
        snap.SetPixels(camTexture.GetPixels());
        snap.Apply();

        // 2. 簡易的に全画面を顔領域とみなし推論
        // 実運用では顔検出ライブラリでバウンディングボックスを取得
        // snap を 224×224 にリサイズ
        // Tensor に変換 → モデルに流し込み → 出力を取得
        // float[] の確率配列を返す
        float[] probs = ageEstimator.PredictAge(snap);

        // 3. 最も確率が高いクラスを探す
        // probs.Max()
        // 配列中の最大値（最も高い確率）を取得
        // ToList().IndexOf(...)
        // その最大値が何番目（どのクラス）かを調べる
        // 結果：最も確率の高い“年齢クラスインデックス”を得られる
        int classIndex = probs.ToList().IndexOf(probs.Max());

        float prob = probs[classIndex];
        string message = $"Age class: {classIndex} Prob : {prob:F2}";

        // 4. 結果をコンソール表示
        Debug.Log(message);

        // UI
        if (resultText != null)
        {
            resultText.text = message;
        }
    }

    void OnDestroy()
    {
        camTexture?.Stop();
    }
}

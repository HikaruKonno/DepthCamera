using System.IO;
using TensorFlowLite;
using Unity.Barracuda;
using UnityEngine;

/// <summary>
/// AgeEstimator:
///  - ONNX Model Zoo の "age_googlenet" モデル (GoogLeNet ベース、0～100 歳の 101 クラス分類) を使用
///  - 入力画像を前処理し、年齢クラスの確率配列を返します
///  - モデルは IMDB-WIKI データセット等で学習されており、顔画像から年齢推定を行います
/// </summary>
public class AgeEstimator : MonoBehaviour
{
    // ONNXモデル
    [Header("Model Settings")]
    [Tooltip("Imported NNModel asset (e.g. age_googlenet.onnx)")]
    public NNModel modelAsset;
    // Key
    [Tooltip("Name of the output layer (Softmax)")]
    public string outputName = "softmaxout_1";

    private Model runtimeModel;
    private IWorker worker;

    void Awake()
    {
        // モデルのロードと推論エンジンワーカー作成
        runtimeModel = ModelLoader.Load(modelAsset);
        // 利用可能な出力レイヤー名をログに表示
        Debug.Log($"[AgeEstimator] Model outputs: {string.Join(", ", runtimeModel.outputs)}");
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, runtimeModel);
    }

    /// <summary>
    /// 推論を実行し、Softmax 確率を返す
    /// </summary>
    public float[] PredictAge(Texture2D inputTex)
    {
        // 1. 前処理: サイズを224x224にリサイズ
        const int size = 224;

        // 2.テクスチャの作成
        // TextureFormat.RGB24 は「8 ビットずつの赤・緑・青チャンネルを持つフォーマット」
        // アルファチャンネルは含まれない。
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);

        // 3.一時的に GPU 上で描画ターゲットとして使う RenderTexture を取得
        RenderTexture rt = RenderTexture.GetTemporary(size, size);

        // 4.inputTex（元の入力画像）を GPU 上で rt（224×224 のレンダーターゲット）にコピー
        Graphics.Blit(inputTex, rt);

        // 5.Unity の内部状態として、
        //「これから ReadPixels 等で読み出すレンダーターゲットはこの rt ですよ」という指定を行う
        RenderTexture.active = rt;

        // 6.GPU 上にある rt のピクセルを、先ほど作成した Texture2D tex に読み込む
        // new Rect(0,0,size,size) で「左下 (0,0) から幅 size、高さ size の矩形領域」を指定。
        // 後ろの 0,0 は、読み込んだピクセルを tex のどの座標から貼り付けるか（オフセット）。
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);

        // 7.ReadPixels で生のピクセルデータは CPU 上のバッファに入っているだけなので、
        // GPU に反映し描画可能なテクスチャとして確定させるために Apply() を呼び出す
        tex.Apply();

        // 8.プールから借りた RenderTexture を返却する。
        // これを忘れるとメモリ（特に GPU メモリ）が無駄に消費され続ける
        RenderTexture.ReleaseTemporary(rt);

        // 9. テンソル変換 (NCHW)
        // 第一引数に Texture2D tex を渡すと、内部でピクセルデータを読み出し、
        // 第二引数の 3 はチャンネル数を表す（RGBなので3チャンネル）。

        // Barracuda の標準は “NCHW” という並び順で、
        // N: バッチサイズ(= 1)
        // C: チャンネル（ここでは 3 = RGB → チャンネルが先）
        // H: 高さ（height = 224）
        // W: 幅（width = 224）
        // Tensor はネイティブリソース（GPU／CPU メモリ）を確保します。
        // using を使うことでブロックを抜けたタイミングで
        // input.Dispose() が自動的に呼ばれ、メモリリークを防ぐ。
        using (Tensor input = new Tensor(tex, 3))
        {
            // 10. 推論実行
            // 引数の input をモデルに流し込み、一連の計算を実行します。
            // これにより内部グラフ（ONNX モデル）が順次評価され、最終的に出力レイヤーに結果が格納される。
            worker.Execute(input);

            // 11. 出力取得
            // 1 )PeekOutput(string)
            // outputName で指定したレイヤー（Softmax のあとなど）の Tensor を取得します。
            // Peek なので、worker 側のデータはそのまま残しつつ、新しい Tensor オブジェクトを返す。
            Tensor output = worker.PeekOutput(outputName);
            // 2 )ToReadOnlyArray()
            // Tensor の生データ（float 配列）を取り出す。
            float[] predictions = output.ToReadOnlyArray();
            // 3 )これで C# の配列として確率やスコアを扱えるようになります。
            // output.Dispose()
            // 取得した output もネイティブリソースを持つため、自分で破棄（Dispose）。
            output.Dispose();

            // 12. クリーンアップ
            // 手動で生成した Texture2D tex は Unity のオブジェクトなので、
            // 使い終わったら Destroy してメモリから削除。
            Destroy(tex);
            // 最終的に “モデルが予測した各クラスの確率”を float 配列として呼び出し元に返す。
            return predictions;
        }
    }

    void OnDestroy()
    {
        // ワーカーを破棄
        worker?.Dispose();
    }
}

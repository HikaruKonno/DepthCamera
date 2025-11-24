using System;
using System.IO;
using System.Linq;
using TensorFlowLite;
using Unity.Barracuda;
using UnityEngine;
using UnityEngine.UI;

public class DepthSensor : MonoBehaviour
{
    // 使用するONNX形式の深度推定モデル
    [SerializeField] private NNModel _monoDepthONNX;

    // Webカメラ入力と出力の表示用RawImage
    [SerializeField] private RawImage _sourceImageView;
    [SerializeField] private RawImage _destinationImageView;

    // クリックした座標に配置するプレハブ
    [SerializeField] private GameObject _boxPrefab;

    // クラス冒頭に追加
    [SerializeField] private Color _nearColor = Color.red;      // カメラに近い点の色
    [SerializeField] private Color _midColor = Color.yellow;   // 中間距離の点の色
    [SerializeField] private Color _farColor = Color.blue;     // カメラから遠い点の色

    [SerializeField,Range(0,1920)] private int _width;
    [SerializeField,Range(0,1080)] private int _hight;
    private Model m_RuntimeModel;
    private IWorker worker;

    // クラスフィールドに追加（DepthSensor クラスの先頭）
    [SerializeField] private Material _particleMaterial; // Inspector でマテリアルを割り当てられるようにする（推奨）

    // Webカメラテクスチャ
    private WebCamTexture _webCamTexture;

    // 出力先のテクスチャ
    private RenderTexture outputRenderTexture;


    // リサイズ関連
    private TextureResizer.ResizeOptions _options;
    private TextureResizer _resizer;
    private TextureToNativeTensor _resizerr;

    // フレームと入力画像
    private RenderTexture frame;
    private Texture2D inputTexture;     // モデル入力用
    private Texture2D depthTexture;     // 推論結果の深度マップテクスチャ
    private Rect region;                // ReadPixels用の領域

    // モデルに合わせた画像サイズ
    private const int MODEL_WIDTH = 224;
    private const int MODEL_HEIGHT = 224;

    // メッシュ用の配列
    private Vector3[] vertices;         // メッシュ頂点座標配列
    private int[] triangles;            // 三角形インデックス配列
    private Mesh mesh;                  // メッシュオブジェクト
    private Color[] colors;             // 頂点カラー配列

    // 深度の配列
    private float[] depthArray;

    // ★ 新規：ParticleSystem 用配列
    private ParticleSystem _particleSystem;
    private ParticleSystem.Particle[] _particles;

    private void Start()
    {
        InitBarracuda();
        InitWebCamFeed();
        InitPointCloudMesh();
        InitResizerAndTextures();

        // ★ ここで ParticleSystem を初期化
        InitParticleSystem();
    }

    /// <summary>
    /// Webカメラの初期化
    /// </summary>
    private void InitBarracuda()
    {
        m_RuntimeModel = ModelLoader.Load(_monoDepthONNX);
        worker = WorkerFactory.CreateComputeWorker(m_RuntimeModel);
    }

    /// <summary>
    /// モデルと推論ワーカーの初期化
    /// </summary>
    private void InitWebCamFeed()
    {
        // new WebCamTexture（Width,Height,fps）
        _webCamTexture = new WebCamTexture(_width, _hight, 30);
        _sourceImageView.texture = _webCamTexture;          // UIに表示
        _webCamTexture.Play();
    }

    /// <summary>
    /// 入力/出力用テクスチャの初期化
    /// </summary>
    private void InitPointCloudMesh()
    {
        vertices = new Vector3[MODEL_WIDTH * MODEL_HEIGHT];
        triangles = MakeMeshTriangles();                    // 三角形の頂点インデックスを生成
        mesh = new Mesh();
        colors = new Color[MODEL_WIDTH * MODEL_HEIGHT];
    }

    /// <summary>
    /// ポイントクラウド表示用のメッシュ初期化
    /// </summary>
    private void InitResizerAndTextures()
    {
        _resizer = new TextureResizer();
        _options = new TextureResizer.ResizeOptions();

        // モデルが想定するサイズに設定
        _options.width = MODEL_WIDTH;
        _options.height = MODEL_HEIGHT;

        // モデル入力用のテクスチャ（RGB24形式）
        inputTexture = new Texture2D(MODEL_WIDTH, MODEL_HEIGHT, TextureFormat.RGB24, false);
        // 深度推論結果表示用のテクスチャ（RGB24形式）
        depthTexture = new Texture2D(MODEL_WIDTH, MODEL_HEIGHT, TextureFormat.RGB24, false);
        // ReadPixels用に短形領域を用意
        region = new Rect(0, 0, MODEL_WIDTH, MODEL_HEIGHT);
    }

    private void Update()
    {
        if (!_webCamTexture && !_webCamTexture.isPlaying && !_webCamTexture.didUpdateThisFrame)
        {
            Debug.Log("カメラ無し");
            return;
        }

        if (_webCamTexture.width < 16 || _webCamTexture.height < 16)
        {
            // 解像度が確定していない → 起動待ち
            return;
        }

        Color[] pixels = _webCamTexture.GetPixels();

        // Webカメラ映像が取得できていれば処理続行
        if (pixels.Length >= (MODEL_WIDTH * MODEL_HEIGHT))
        {
            // Webカメラ映像をモデル入力サイズにリサイズ
            ResizeWebCamFeedToInputTexture();

            // 深度配列に左上から入れたいため上下を変更する
            // Texture2Dは左下から見るため

            // テクスチャをTensorに変換
            using (var tensor = new Tensor(inputTexture))
            {

                // モデルに入力し推論を実行
                using (var output = worker.Execute(tensor).PeekOutput())
                {
                    // 出力Tensorをfloat配列に変換（深度値）
                    float[] depth = output.AsFloats();
                    depthArray = depth;

                    //CheckDepth(depth);

                    // OutputDepth(depth);

                    // 深度値をテクスチャとメッシュ頂点に変換
                    PrepareDepthTextureFromFloats(depth);
                }
            }
            // 深度マップをUI表示
            _destinationImageView.texture = depthTexture;

            // メッシュ更新
            UpdatePointCloudMeshFilter();
        }
        else
        {
            Debug.LogError("unti");
        }

        // --- 既存のカメラ→深度処理略 ---

        if (depthArray != null)
        {
            // 深度テクスチャ作成後に点群更新
            UpdatePointCloudParticles();
        }

    }

    /// <summary>
    /// Webカメラ映像をモデル入力サイズにリサイズし、inputTextureに転送する
    /// </summary>
    private void ResizeWebCamFeedToInputTexture()
    {
        // Webカメラテクスチャを指定サイズにリサイズ
        RenderTexture tex = _resizer.Resize(_webCamTexture, _options);

        // リサイズ結果のRenderTextureをアクティブにして読み込み
        RenderTexture.active = tex;
        inputTexture.ReadPixels(region, 0, 0);
        RenderTexture.active = null;

        // テクスチャ反映
        inputTexture.Apply();
        // 入力用テクスチャをUI表示
        _sourceImageView.texture = inputTexture;
    }

    private void OnDestroy()
    {
        // WebCam停止
        _webCamTexture?.Stop();

        // Barracuda ワーカー破棄
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }

        // 使っている RenderTexture を Release するなら
        if (outputRenderTexture != null)
        {
            outputRenderTexture.Release();
            outputRenderTexture = null;
        }
    }

    /// <summary>
    /// 推論結果の深度データをテクスチャとメッシュ頂点に変換
    /// </summary>
    /// <param name="depth">flaot[]型：推論結果の深度値配列</param>
    private void PrepareDepthTextureFromFloats(float[] depth)
    {
        var min = depth.Min();
        var max = depth.Max();

        // 各画素ごとに深度を正規化し色に変換・頂点と色配列に設定
        for (int i = 0; i < depth.Length; i++)
        {
            int x = i % MODEL_WIDTH;
            int y = i / MODEL_WIDTH;
            float val = (depth[i] - min) / (max - min);

            Color c;
            if (val < 0.5f)
            {
                c = Color.Lerp(_nearColor, _midColor, val / 0.5f);
            }
            else
            {
                c = Color.Lerp(_midColor, _farColor, (val - 0.5f) / 0.5f);
            }

            depthTexture.SetPixel(x, y, c);
            // 以降の頂点・色配列設定はそのまま

            // ★ここから復活：頂点と頂点色の設定
            // テクスチャの上下反転補正
            int invY = MODEL_HEIGHT - y - 1;

            // ３Ｄ空間上の座標設定（中心化も同時に）
            float cx = MODEL_WIDTH / 2f;
            float cy = MODEL_HEIGHT / 2f;
            float worldX = (x - cx) / (MODEL_WIDTH / 0.9f);
            float worldY = (y - cy) / (MODEL_HEIGHT / 0.9f);
            float worldZ = val;

            vertices[i] = new Vector3(worldX, worldY, worldZ);
            colors[i] = inputTexture.GetPixel(x, invY);
        }
        depthTexture.Apply();
    }

    /// <summary>
    /// メッシュに頂点、色、インデックスを設定し、ポイントクラウドとして描画
    /// </summary>
    private void UpdatePointCloudMeshFilter()
    {
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.SetIndices(mesh.GetIndices(0), MeshTopology.Points, 0);
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    /// <summary>
    /// 三角形インデックス生成（未使用だがMesh構造用）
    /// </summary>
    /// <returns>int型：三角形のインデックスの配列</returns>
    public int[] MakeMeshTriangles()
    {
        var triangles = new int[(MODEL_WIDTH - 1) * (MODEL_HEIGHT - 1) * 6];
        for (int y = 0; y < MODEL_HEIGHT - 1; ++y)
        {
            for (int x = 0; x < MODEL_WIDTH - 1; ++x)
            {
                int ul = y * MODEL_WIDTH + x;            // 左上
                int ur = y * MODEL_WIDTH + x + 1;        // 右上
                int ll = (y + 1) * MODEL_WIDTH + x;      // 左下
                int lr = (y + 1) * MODEL_WIDTH + x + 1;  // 右下

                int offset = (y * (MODEL_WIDTH - 1) + x) * 6;

                // 2つの三角形で四角形を構成
                triangles[offset + 0] = ll;
                triangles[offset + 1] = ul;
                triangles[offset + 2] = ur;
                triangles[offset + 3] = ll;
                triangles[offset + 4] = ur;
                triangles[offset + 5] = lr;

            }
        }

        return triangles;
    }

    /// <summary>
    /// 深度を出力する
    /// </summary>
    /// <param name="depthArray">float[]型：深度の配列</param>
    private void OutputDepth(float[] depthArray)
    {

        StreamWriter sw = new StreamWriter("./Assets/TextData.txt", false); // TextData.txtというファイルを新規で用意
        int num = 0;

        foreach (int i in depthArray)
        {
            sw.Write($"{i} ");// ファイルに書き出したあと改行
            ++num;

            if (num >= 224)
            {
                sw.Write("\n"); num = 0;
            }
        }

        sw.WriteLine("おわり");

        sw.Flush();
        sw.Close();
    }

    void CheckDepth(float[] depth)
    {
        for (int i = 0; i < depth.Length; ++i)
        {
            Debug.LogError($"{i}番目:{Math.Round(depth[i])}");
        }

        // 止める
        Debug.Break();
    }

    private void InitParticleSystem()
    {
        // 1) ParticleSystem コンポーネント取得 or 追加
        _particleSystem = GetComponent<ParticleSystem>();
        if (_particleSystem == null)
        {
            _particleSystem = gameObject.AddComponent<ParticleSystem>();
        }

        // Renderer を取得
        var psr = _particleSystem.GetComponent<ParticleSystemRenderer>();

        // 2) マテリアル設定（Inspector 割当があれば優先）
        if (_particleMaterial != null)
        {
            psr.material = _particleMaterial;
        }
        else
        {
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                var mat = new Material(shader);
                // ブレンドや ZWrite 等の調整が必要ならここで行う（必要ならコメント解除して調整）
                // mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                // mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                // mat.SetInt("_ZWrite", 1);
                psr.material = mat;
            }
            else
            {
                Debug.LogWarning("[InitParticleSystem] Shader 'Particles/Standard Unlit' not found. Please assign a particle material in inspector or add shader to Always Included Shaders.");
            }
        }

        // 3) renderMode を先に決定しておく
        psr.renderMode = ParticleSystemRenderMode.Mesh;

        // 4) 頂点だけを持つ Mesh を用意（Points）
        var pointMesh = new Mesh();
        pointMesh.name = "PointMesh_SingleVertex";
        pointMesh.vertices = new Vector3[] { Vector3.zero };
        pointMesh.SetIndices(new int[] { 0 }, MeshTopology.Points, 0);

        // bounds と GPU アップロード
        pointMesh.RecalculateBounds();
        try
        {
            // false にして読み取り可能なまま GPU に送る（必要なら true にしてから再生成）
            pointMesh.UploadMeshData(false);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[InitParticleSystem] UploadMeshData failed: {ex.Message}");
        }

        // ログ（デバッグ用）
        Debug.Log($"[InitParticleSystem] pointMesh.vertexCount={pointMesh.vertexCount}, indexCount={pointMesh.GetIndexCount(0)}, bounds={pointMesh.bounds}");

        // 5) mesh をレンダラーに割当
        psr.mesh = pointMesh;

        // 割当られたか確認
        if (psr.mesh == null || psr.mesh.vertexCount == 0)
        {
            Debug.LogWarning("[InitParticleSystem] Assigned point mesh was null/empty. Falling back to small quad mesh for compatibility.");

            // 代替：小さな Quad を作る（互換性重視）
            var quad = new Mesh();
            quad.name = "ParticleQuad";
            quad.vertices = new Vector3[] {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
            quad.uv = new Vector2[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            quad.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            quad.RecalculateBounds();
            try
            {
                quad.UploadMeshData(false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InitParticleSystem] UploadMeshData for quad failed: {ex.Message}");
            }

            psr.mesh = quad;
            psr.renderMode = ParticleSystemRenderMode.Mesh;

            Debug.Log($"[InitParticleSystem] fallback quad.vertexCount={psr.mesh.vertexCount}, bounds={psr.mesh.bounds}");
        }
        else
        {
            Debug.Log($"[InitParticleSystem] assigned psr.mesh vertexCount={psr.mesh.vertexCount}");
        }

        // 6) まずシステムを停止し、全パーティクルをクリア
        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _particleSystem.Clear();

        // 7) main モジュール設定
        var main = _particleSystem.main;
        main.playOnAwake = false;                                  // Awakeで再生しない
        main.maxParticles = MODEL_WIDTH * MODEL_HEIGHT;             // 最大パーティクル数
        main.loop = false;                                         // ループしない
        main.duration = 1f;                                        // 1秒（使いどころないので適当でOK）
        main.simulationSpace = ParticleSystemSimulationSpace.Local;    // ローカル座標（必要なら World に変更）
        main.startLifetime = Mathf.Infinity;                         // 無限ライフタイム
        main.startSize = 0.04f;                                     // 小さめに変更（100f は大きすぎる）
        main.startColor = Color.white;                               // 色は個別にセット

        // 8) Emission は完全にオフ
        var emission = _particleSystem.emission;
        emission.enabled = false;

        // 9) パーティクル配列を確保して残ライフだけセット
        _particles = new ParticleSystem.Particle[MODEL_WIDTH * MODEL_HEIGHT];
        for (int i = 0; i < _particles.Length; i++)
        {
            _particles[i].remainingLifetime = Mathf.Infinity;
            // 初期ポジションを zero にしておく（UpdatePointCloudParticles で更新）
            _particles[i].position = Vector3.zero;
            _particles[i].startSize = 0.04f;
        }

        // 10) SetParticles -> Play
        _particleSystem.SetParticles(_particles, _particles.Length);
        _particleSystem.Play();

        // デバッグ出力
        Debug.Log($"[Init] isPlaying={_particleSystem.isPlaying}, particleCount={_particleSystem.particleCount}");
    }

    /// <summary>
    /// depthArray と vertices, colors 配列を使ってパーティクルを更新
    /// </summary>
    private void UpdatePointCloudParticles()
    {
        // 毎フレーム、パーティクル状態をログ
        float minDepth = depthArray.Min();
        float maxDepth = depthArray.Max();
        float depthRange = maxDepth - minDepth;


        for (int i = 0; i < vertices.Length; i++)
        {
            float depth = depthArray[i];
            float normalized = (depth - minDepth) / depthRange; // 0=近い, 1=遠い

            // ─── 手前／奥で色を段階的に変える ───
            Color c;
            if (normalized < 0.5f)
            {
                // 近い～中間： nearColor → midColor
                float t = normalized / 0.5f;
                c = Color.Lerp(_nearColor, _midColor, t);
            }
            else
            {
                // 中間～遠い： midColor → farColor
                float t = (normalized - 0.5f) / 0.5f;
                c = Color.Lerp(_midColor, _farColor, t);
            }

            // 点の大きさも深度で変えたいならここで調整（省略可）

            _particles[i].position = vertices[i];
            _particles[i].startColor = c;
            _particles[i].startSize = Mathf.Lerp(0.04f, 0.005f, normalized);
            _particles[i].remainingLifetime = Mathf.Infinity;
        }

        _particleSystem.SetParticles(_particles, _particles.Length);
    }
}
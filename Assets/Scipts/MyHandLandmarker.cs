//using System.Collections;
//using System.Collections.Generic;
//using Mediapipe;
//using Mediapipe.Unity;
//using Mediapipe.Unity.Sample.HandLandmarkDetection;
//using UnityEngine;

//[RequireComponent(typeof(Camera))]
//public class MyHandLandmarker : MonoBehaviour
//{
//    [Header("Depth Sensor (MonoDepth)")]
//    [Tooltip("DepthSensor コンポーネント (DepthArray プロパティ公開済み)")]
//    public DepthSensor depthSensor;

//    [Header("Touch Detection")]
//    [Tooltip("タッチ距離閾値 (ワールド単位)")]
//    public float touchDistanceThreshold = 0.05f;
//    [Tooltip("判定対象のレイヤー")]
//    public LayerMask touchLayers;

//    GraphBuilder

//    private  HandLandmarkerGraph handGraph;
//    private Camera mainCamera;
//    private int modelWidth = 224, modelHeight = 224;

//    void Start()
//    {
//        mainCamera = GetComponent<Camera>();

//        // 1) Graph を生成・初期化
//        handGraph = new HandLandmarkerGraph();
//        handGraph.OnHandLandmarksOutput += OnHandLandmarks;  // イベント登録
//        handGraph.Initialize();                             // graph のセットアップ
//        handGraph.StartRunAsync();                          // ライブカメラ入力を開始
//    }

//    void OnDestroy()
//    {
//        // 後片付け
//        handGraph.OnHandLandmarksOutput -= OnHandLandmarks;
//        handGraph.CloseInputStream();
//        handGraph.Shutdown();
//    }

//    private void OnHandLandmarks(object sender, OutputEventArgs<NormalizedLandmarkList> args)
//    {
//        var landmarkList = args.value;
//        if (landmarkList == null || landmarkList.Landmark.Count == 0) return;

//        // 深度配列を取得
//        var depthArray = depthSensor.DepthArray;
//        if (depthArray == null || depthArray.Length == 0) return;

//        // #8 が人差し指先
//        var tip = landmarkList.Landmark[8];

//        // 正規化→ピクセル座標
//        int px = Mathf.Clamp((int)(tip.X * modelWidth), 0, modelWidth - 1);
//        int py = Mathf.Clamp((int)((1f - tip.Y) * modelHeight), 0, modelHeight - 1);
//        int idx = py * modelWidth + px;

//        // 深度取得 (0〜1 正規化)
//        float z = depthArray[idx];

//        // DepthSensor と同じスケールでワールド座標化
//        var fingerWorld = new Vector3(
//            px / (modelWidth / 0.9f),
//            py / (modelHeight / 0.9f),
//            z
//        );

//        // OverlapSphere で“タッチ”判定
//        var hits = Physics.OverlapSphere(fingerWorld, touchDistanceThreshold, touchLayers);
//        if (hits.Length > 0)
//        {
//            foreach (var c in hits)
//            {
//                Debug.Log($"[PureGraph] Touch with {c.name}");
//                // ▼ ここにタッチ時の処理を追加
//            }
//        }
//    }

//    void Update()
//    {
//        // 毎フレーム、カメラ画像を Graph に流し込む
//        // handGraph.InputStreamName は “input_video” がデフォルト
//        handGraph.TryAddTextureFrameToInputStream(
//            mainCamera.targetTexture,  // あるいは WebCamTexture
//            TimeFrame.Timestamp
//        );
//    }
//}

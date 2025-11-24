//using System.Collections.Generic;
//using UnityEngine;
//using Mediapipe;
//using Mediapipe.Unity;
//using Mediapipe.Unity.Sample.HandLandmarkDetection;

//namespace ARSakakibara
//{
//    /// <summary>
//    /// MediaPipe と MonoDepth (DepthSensor) の深度情報を組み合わせ、
//    /// 指先がシーン内オブジェクトに"タッチ"したかを判定するコンポーネント
//    /// </summary>
//    [RequireComponent(typeof(Camera))]
//    public class CombinedTouchDetector : MonoBehaviour
//    {
//        [Header("MediaPipe Settings")]
//        [Tooltip("MediaPipe HandLandmarkerSolution プレハブ")]
//        public HandLandmarkerRunner handSolution;

//        [Header("Depth Settings")]
//        [Tooltip("DepthSensor コンポーネント (深度配列 depthArray を取得) ")]
//        public DepthSensor depthSensor;
//        public int modelWidth = 224;
//        public int modelHeight = 224;

//        [Header("Touch Detection")]
//        [Tooltip("距離ベースのタッチ閾値 (ワールド単位) ")]
//        public float touchDistanceThreshold = 0.05f;
//        [Tooltip("タッチ判定対象レイヤー")]
//        public LayerMask touchLayers;

//        private Camera mainCamera;
//        private float[] depthArray;

//        void Awake()
//        {
//            mainCamera = GetComponent<Camera>();
//            if (handSolution == null)
//                Debug.LogError("HandLandmarkerSolution がアサインされていません。");
//            if (depthSensor == null)
//                Debug.LogError("DepthSensor がアサインされていません。");
//        }

//        void OnEnable()
//        {
//            handSolution.OnHandLandmarksOutput.AddListener(OnHandLandmarks);
//        }

//        void OnDisable()
//        {
//            handSolution.OnHandLandmarksOutput.RemoveListener(OnHandLandmarks);
//        }

//        /// <summary>
//        /// HandLandmarker から呼ばれるコールバック
//        /// </summary>
//        void OnHandLandmarks(List<NormalizedLandmarkList> hands)
//        {
//            if (hands == null || hands.Count == 0)
//                return;

//            // 最新の深度配列を取得
//            depthArray = depthSensor.DepthArray;  // public プロパティとして depthArray を DepthSensor に用意しておく
//            if (depthArray == null || depthArray.Length == 0)
//                return;

//            // 最初の手のランドマークを使用
//            var landmarks = hands[0].Landmark;
//            var indexTip = landmarks[8]; // #8 = 人差し指先

//            // 正規化座標からピクセル座標へ変換
//            int px = Mathf.Clamp((int)(indexTip.X * modelWidth), 0, modelWidth - 1);
//            int py = Mathf.Clamp((int)((1f - indexTip.Y) * modelHeight), 0, modelHeight - 1);
//            int depthIndex = py * modelWidth + px;

//            // 深度取得 (0～1 正規化)
//            float depthZ = depthArray[depthIndex];

//            // ワールド座標に変換 (DepthSensor のスケーリングに合わせる)
//            Vector3 fingerWorld = new Vector3(
//                px / ((float)modelWidth / 0.9f),
//                py / ((float)modelHeight / 0.9f),
//                depthZ
//            );

//            // シーン内オブジェクトへ距離判定
//            Collider[] hits = Physics.OverlapSphere(fingerWorld, touchDistanceThreshold, touchLayers);
//            if (hits.Length > 0)
//            {
//                foreach (var col in hits)
//                {
//                    Debug.Log($"Touch detected with: {col.name}");
//                    // ここでタッチ時の処理を呼び出し
//                }
//            }
//        }
//    }
//}

using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Stage Config", fileName = "StageConfig")]
public class StageConfig : ScriptableObject
{
    [Header("Rail Budget")]
    [Min(0)] public int maxRails = 0; // 0이면 무제한

    [Header("Marble")]
    public GameObject marblePrefab;

    [Header("Build Palette (Stage)")]
    public PlacementData[] allowedPlacements;

    [Header("Placement Limits (per PO)")]
    public PlacementLimit[] placementLimits;

    [Serializable]
    public struct PlacementLimit
    {
        public PlacementData data;
        [Min(0)] public int maxCount; // 0이면 무제한
    }

    /// <summary>
    /// 해당 PlacementData의 최대 설치 수.
    /// - 설정이 없으면 무제한(0)로 취급하거나, 기본값을 정할 수 있음.
    /// </summary>
    public int GetMaxCount(PlacementData data)
    {
        if (data == null) return 0;
        if (placementLimits == null) return 0;

        for (int i = 0; i < placementLimits.Length; i++)
        {
            if (placementLimits[i].data == data)
                return Mathf.Max(0, placementLimits[i].maxCount);
        }
        return 0; // 미설정이면 무제한
    }

    public int defaultPlacementIndex = 0;

    // =========================================================
    // Camera Limits (Stage-specific)
    // =========================================================
    [Serializable]
    public struct CameraPose
    {
        public Vector2 pos;
        public float zoom; // orthographicSize
    }

    public CameraLimits cameraLimits = new CameraLimits
    {
        zoomInPos = new Vector2(0, 0),
        zoomInZoom = 8f,

        boundsCenter = new Vector2(0, 0),
        boundsHalfY = 10f,

        introStartPose = new CameraPose { pos = new Vector2(0, 0), zoom = 12f },
        zoomOutPose = new CameraPose { pos = new Vector2(0, 0), zoom = 10f }
    };

    [Serializable]
    public struct CameraLimits
    {
        [Header("Work View (Reset Pose)")]
        public Vector2 zoomInPos;
        public float zoomInZoom;

        [Header("Bounds Center")]
        public Vector2 boundsCenter;

        [Header("Bounds Half Height (Y only)")]
        [Tooltip("경계의 세로 반경(센터에서 위/아래). 가로는 16:9로 자동 계산")]
        [Min(0f)] public float boundsHalfY;

        // 16:9 고정
        const float DEFAULT_ASPECT_16_9 = 16f / 9f;

        public float BoundsHalfX_16_9 => boundsHalfY * DEFAULT_ASPECT_16_9;

        public Vector2 BoundsMin => boundsCenter - new Vector2(BoundsHalfX_16_9, boundsHalfY);
        public Vector2 BoundsMax => boundsCenter + new Vector2(BoundsHalfX_16_9, boundsHalfY);

        [Header("Intro")]
        public CameraPose introStartPose;

        [Header("Zoom Toggle")]
        public CameraPose zoomOutPose;

        /// <summary>
        /// 최대 축소 한계는 전체보기 줌과 동일하게 사용.
        /// </summary>
        public float MaxZoomCap => Mathf.Max(0f, zoomOutPose.zoom);
    }
}
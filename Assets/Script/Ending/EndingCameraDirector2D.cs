using System.Collections;
using UnityEngine;

public class EndingCameraDirector2D : MonoBehaviour
{
    [System.Serializable]
    public class CameraPoint
    {
        public Transform point;
        public float moveTime = 2f;
        public float waitTime = 0f;
        public float orthoSize = 5f;
    }

    [SerializeField] Camera cam;
    [SerializeField] string coreCameraTag = "MainCamera";
    [SerializeField] CameraPoint[] points;

    IEnumerator Start()
    {
        yield return WaitForCoreCamera();

        if (cam == null || points == null || points.Length == 0)
            yield break;

        cam.transform.position = ToCameraPos(points[0].point.position);
        cam.orthographicSize = points[0].orthoSize;

        for (int i = 1; i < points.Length; i++)
        {
            yield return MoveTo(points[i]);

            if (points[i].waitTime > 0f)
                yield return new WaitForSeconds(points[i].waitTime);
        }
    }

    IEnumerator WaitForCoreCamera()
    {
        while (cam == null)
        {
            cam = Camera.main;

            if (cam == null)
                yield return null;
        }
    }

    IEnumerator MoveTo(CameraPoint target)
    {
        if (target == null || target.point == null)
            yield break;

        Vector3 fromPos = cam.transform.position;
        Vector3 toPos = ToCameraPos(target.point.position);

        float fromSize = cam.orthographicSize;
        float toSize = target.orthoSize;

        float t = 0f;
        float duration = Mathf.Max(0.01f, target.moveTime);

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);

            cam.transform.position = Vector3.Lerp(fromPos, toPos, p);
            cam.orthographicSize = Mathf.Lerp(fromSize, toSize, p);

            yield return null;
        }

        cam.transform.position = toPos;
        cam.orthographicSize = toSize;
    }

    Vector3 ToCameraPos(Vector3 worldPos)
    {
        return new Vector3(worldPos.x, worldPos.y, cam.transform.position.z);
    }
}
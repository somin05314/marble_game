using UnityEngine;

public class SnapManager : MonoBehaviour
{
    public static bool TrySnap(PlacementObject obj)
    {
        Debug.Log($"TrySnap called on: {obj.name}");

        SnapTarget[] targets = FindObjectsOfType<SnapTarget>();
        Debug.Log($"SnapTargets found: {targets.Length}");
        Debug.Log($"SnapPoints count: {obj.snapPoints?.Length ?? 0}");

        float minDist = float.MaxValue;
        SnapTarget bestTarget = null;
        Transform bestSnapPoint = null;

        foreach (var point in obj.snapPoints)
        {
            foreach (var target in targets)
            {
                if (target.GetComponentInParent<PlacementObject>() == obj)
                    continue;

                float dist = Vector2.Distance(point.position, target.transform.position);
                Debug.Log(
                    $"Checking dist: {dist:F3} (radius: {target.snapRadius}) " +
                    $"point: {point.name}, target: {target.name}"
                );
                if (dist < target.snapRadius && dist < minDist)
                {
                    minDist = dist;
                    bestTarget = target;
                    bestSnapPoint = point;
                }
            }
        }
        Debug.Log(
    $"BestTarget: {(bestTarget ? bestTarget.name : "null")}, " +
    $"BestSnapPoint: {(bestSnapPoint ? bestSnapPoint.name : "null")}"
);
        if (bestTarget != null && bestSnapPoint != null)
        {
            Vector3 offset = obj.transform.position - bestSnapPoint.position;
            obj.transform.position = bestTarget.transform.position + offset;

            obj.snappedTo = bestTarget.GetComponentInParent<PlacementObject>();
            return true;
        }

        obj.snappedTo = null;
        return false;
    }

}

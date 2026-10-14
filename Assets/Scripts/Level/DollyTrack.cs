using System.Collections.Generic;
using UnityEngine;

namespace YourMovieSucks.Level
{
    /// <summary>
    /// 光枪推轨路径：一组机位节点（Transform）。
    /// 节点顺序即相机推进顺序；节点间连线在 Scene 视图用 Gizmos 显示。
    /// </summary>
    public class DollyTrack : MonoBehaviour
    {
        [SerializeField] private List<Transform> waypoints = new();

        public int Count => waypoints.Count;
        public IReadOnlyList<Transform> Waypoints => waypoints;

        public Transform GetWaypoint(int index)
        {
            if (index < 0 || index >= waypoints.Count) return null;
            return waypoints[index];
        }

        public void AddWaypoint(Transform t)
        {
            if (t != null && !waypoints.Contains(t))
                waypoints.Add(t);
        }

        public void RemoveWaypoint(Transform t)
        {
            waypoints.Remove(t);
        }

        public void ClearNulls()
        {
            waypoints.RemoveAll(w => w == null);
        }

        private void OnDrawGizmos()
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;

                Gizmos.color = i == 0 ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(waypoints[i].position, 0.25f);

                if (i > 0 && waypoints[i - 1] != null)
                {
                    Gizmos.color = new Color(1f, 1f, 0.5f, 0.7f);
                    Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
                }
            }
        }
    }
}

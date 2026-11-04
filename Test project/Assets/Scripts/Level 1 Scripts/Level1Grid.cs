using System.Collections.Generic;
using UnityEngine;

namespace ThatGameJam.LevelOne
{
    // Uses real tile positions, not the unreliable Ground(x,y) names.
    public sealed class Level1Grid : MonoBehaviour
    {
        public Transform groundRoot;
        public Transform tile44;
        public Transform tile54;
        public Transform tile46;
        public Vector2Int[] blockedCells = { new Vector2Int(6, 1) };
        readonly Dictionary<Vector2Int, Vector3> cells = new Dictionary<Vector2Int, Vector3>();
        Vector3 origin, xStep, yStep;

        public bool Build()
        {
            cells.Clear();
            if (!groundRoot || !tile44 || !tile54 || !tile46)
                return Error("Assign Ground Root, Tile44, Tile54 and Tile46.");
            origin = tile44.position;
            xStep = tile54.position - origin;
            yStep = (tile46.position - origin) / 2f;
            if (xStep.sqrMagnitude < .01f || yStep.sqrMagnitude < .01f ||
                Mathf.Abs(Vector3.Dot(xStep.normalized, yStep.normalized)) > .1f)
                return Error("Grid axes must be nonzero and perpendicular.");
            foreach (Transform t in groundRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!t.gameObject.activeInHierarchy || !t.name.Trim().StartsWith("Ground(")) continue;
                Vector3 delta = t.position - origin;
                float x = Vector3.Dot(delta, xStep) / xStep.sqrMagnitude;
                float y = Vector3.Dot(delta, yStep) / yStep.sqrMagnitude;
                var key = new Vector2Int(4 + Mathf.RoundToInt(x), 4 + Mathf.RoundToInt(y));
                if (Mathf.Abs(x - Mathf.Round(x)) > .15f || Mathf.Abs(y - Mathf.Round(y)) > .15f)
                    return Error("Off-grid tile: " + t.name);
                if (cells.ContainsKey(key)) return Error("Two tiles occupy cell " + key);
                cells.Add(key, t.position);
            }
            foreach (var key in blockedCells) cells.Remove(key);
            return cells.Count > 0 || Error("No Ground(...) tiles found.");
        }
        bool Error(string message) { Debug.LogError("[Level1Grid] " + message, this); return false; }
        public bool Has(Vector2Int key) => cells.ContainsKey(key);
        public Vector3 Position(Vector2Int key) => cells[key];
        public float CellSize => Mathf.Min(xStep.magnitude, yStep.magnitude);
        public static Vector2Int JumpTarget(Vector2Int cell, Vector2Int direction) => cell + direction * 2;
    }
}

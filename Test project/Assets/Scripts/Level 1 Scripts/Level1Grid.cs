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
        [Header("Player Tile Highlight")]
        public bool highlightPlayerTile = true;
        [Tooltip("Optional material used on the occupied tile. If empty, use the tint below.")]
        public Material playerTileMaterial;
        public Color playerTileColor = new Color(.28f, .92f, .82f, 1f);
        [Range(0f, 1f)] public float playerTileTintStrength = .8f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");
        readonly Dictionary<Vector2Int, Transform> tileObjects = new Dictionary<Vector2Int, Transform>();
        readonly Dictionary<Renderer, Material[]> originalTileMaterials = new Dictionary<Renderer, Material[]>();
        readonly List<TileTint> highlightedSlots = new List<TileTint>();
        readonly struct TileTint
        {
            public readonly Renderer Renderer;
            public readonly int Slot;
            public readonly MaterialPropertyBlock Original;
            public TileTint(Renderer renderer, int slot, MaterialPropertyBlock original)
            { Renderer = renderer; Slot = slot; Original = original; }
        }

        Vector3 origin, xStep, yStep;

        public bool Build()
        {
            ClearPlayerTileHighlight();
            cells.Clear();
            tileObjects.Clear();
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
                tileObjects.Add(key, t);
            }
            foreach (var key in blockedCells) { cells.Remove(key); tileObjects.Remove(key); }
            return cells.Count > 0 || Error("No Ground(...) tiles found.");
        }
        // Tint only the renderer instance for the tile the player occupies.
        public void HighlightPlayerTile(Vector2Int cell)
        {
            ClearPlayerTileHighlight();
            if (!highlightPlayerTile || !isActiveAndEnabled || !tileObjects.TryGetValue(cell, out var tile) || !tile)
                return;

            var rootRenderer = tile.GetComponent<Renderer>();
            var renderers = rootRenderer ? new[] { rootRenderer } : tile.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled) continue;
                var materials = renderer.sharedMaterials;
                if (playerTileMaterial && materials.Length > 0)
                {
                    var replacement = new Material[materials.Length];
                    for (int slot = 0; slot < replacement.Length; slot++)
                        replacement[slot] = playerTileMaterial;
                    originalTileMaterials.Add(renderer, materials);
                    renderer.sharedMaterials = replacement;
                    continue;
                }
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    if (!material) continue;
                    int property = material.HasProperty(BaseColor) ? BaseColor :
                        material.HasProperty(LegacyColor) ? LegacyColor : -1;
                    if (property == -1) continue;

                    var original = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(original, slot);
                    var tinted = new MaterialPropertyBlock();
                    if (original.isEmpty)
                        renderer.GetPropertyBlock(tinted);
                    else
                        renderer.GetPropertyBlock(tinted, slot);
                    Color baseColor = tinted.HasColor(property) ? tinted.GetColor(property) : material.GetColor(property);
                    Color color = Color.Lerp(baseColor, playerTileColor, playerTileTintStrength);
                    color.a = baseColor.a;
                    tinted.SetColor(property, color);
                    highlightedSlots.Add(new TileTint(renderer, slot, original.isEmpty ? null : original));
                    renderer.SetPropertyBlock(tinted, slot);
                }
            }
        }

        public void ClearPlayerTileHighlight()
        {
            foreach (var entry in originalTileMaterials)
                if (entry.Key) entry.Key.sharedMaterials = entry.Value;
            originalTileMaterials.Clear();
            foreach (var entry in highlightedSlots)
                if (entry.Renderer) entry.Renderer.SetPropertyBlock(entry.Original, entry.Slot);
            highlightedSlots.Clear();
        }

        void OnDisable() => ClearPlayerTileHighlight();


        bool Error(string message) { Debug.LogError("[Level1Grid] " + message, this); return false; }
        public bool Has(Vector2Int key) => cells.ContainsKey(key);
        public Vector3 Position(Vector2Int key) => cells[key];
        public float CellSize => Mathf.Min(xStep.magnitude, yStep.magnitude);
        public static Vector2Int JumpTarget(Vector2Int cell, Vector2Int direction) => cell + direction * 2;
    }
}


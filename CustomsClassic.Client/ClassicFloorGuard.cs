using System.Collections.Generic;
using Comfort.Common;
using CustomsClassic.Shared;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace CustomsClassic.Client
{
    /// <summary>
    /// Keeps the ground under every character in the physics world. The classic map's static colliders can drop out of
    /// world queries for seconds while still enabled; toggling a collider re-adds it. A character already fallen
    /// through is put back on the ground above it.
    /// </summary>
    internal class ClassicFloorGuard : MonoBehaviour
    {
        const int WalkMask = (1 << 0) | (1 << 11) | (1 << 18);   // Default, Terrain, LowPolyCollider: what a character stands on
        const float Cell = 8f;
        const float Below = 1.2f;       // further than a step under the feet and the character is in the air anyway
        const float Recheck = 0.1f;     // a cell just looked at is left alone this long: losses come in waves a second apart
        const float Depth = 8f;         // nothing this far under a falling character: it is through the map, not off a ledge
        const float Reach = 15f;        // how far above it the ground it fell through is looked for

        readonly Dictionary<long, List<Collider>> _cells = new Dictionary<long, List<Collider>>();
        readonly Dictionary<long, float> _repaired = new Dictionary<long, float>();
        readonly RaycastHit[] _hits = new RaycastHit[48];
        int _watched, _restored, _events, _rescued;
        float _nextReport, _spentMs, _worstMs;
        int _asked;
        readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();

        internal static void Install() => SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (!CustomsScenes.IsClassicScene(scene.name)) return;
            var holder = new GameObject("CustomsClassic.FloorGuard");
            SceneManager.MoveGameObjectToScene(holder, scene);   // goes with the map
            holder.AddComponent<ClassicFloorGuard>();
        };

        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Collider>(false))
                {
                    if (!c.enabled || c.isTrigger || c.attachedRigidbody != null || ((1 << c.gameObject.layer) & WalkMask) == 0) continue;
                    var b = c.bounds;
                    int x0 = Mathf.FloorToInt(b.min.x / Cell), x1 = Mathf.FloorToInt(b.max.x / Cell), z0 = Mathf.FloorToInt(b.min.z / Cell), z1 = Mathf.FloorToInt(b.max.z / Cell);
                    for (var x = x0; x <= x1; x++)
                        for (var z = z0; z <= z1; z++)
                        {
                            if (!_cells.TryGetValue(Key(x, z), out var list)) _cells[Key(x, z)] = list = new List<Collider>();
                            list.Add(c);
                        }
                    _watched++;
                }
            Plugin.Log.LogInfo($"[CC] floor guard: {_watched} walking collider(s) in {_cells.Count} cell(s)");
        }

        // Drops a ray through the collider's point nearest to `near`. A full hit buffer counts as found.
        bool WorldFinds(Collider c, Vector3 near)
        {
            var b = c.bounds;
            var x = Mathf.Clamp(near.x, b.min.x + 0.02f, b.max.x - 0.02f);
            var z = Mathf.Clamp(near.z, b.min.z + 0.02f, b.max.z - 0.02f);
            var ray = new Ray(new Vector3(x, b.max.y + 1f, z), Vector3.down);
            if (!c.Raycast(ray, out _, b.size.y + 2f)) return true;   // nothing of it under that point: not a judgement
            var n = Physics.RaycastNonAlloc(ray, _hits, b.size.y + 2f, 1 << c.gameObject.layer, QueryTriggerInteraction.Ignore);
            if (n == _hits.Length) return true;
            for (var i = 0; i < n; i++) if (_hits[i].collider == c) return true;
            return false;
        }

        // A character falling through the map goes back onto the lowest navmesh ground above. Teleport resets fall damage.
        bool Rescue(Player p, Vector3 at)
        {
            var move = p.MovementContext;
            if (move == null || move.IsGrounded || move.FreefallTime < 0.25f) return false;
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, Depth + 0.5f, WalkMask, QueryTriggerInteraction.Ignore)) return false;
            var n = Physics.RaycastNonAlloc(at + Vector3.up * Reach, Vector3.down, _hits, Reach - 0.6f, WalkMask, QueryTriggerInteraction.Ignore);
            var best = Vector3.zero;
            var found = false;
            for (var i = 0; i < n; i++)
            {
                if (found && _hits[i].point.y >= best.y) continue;
                if (!NavMesh.SamplePosition(_hits[i].point, out var nav, 1f, NavMesh.AllAreas)) continue;
                best = nav.position;
                found = true;
            }
            if (!found) return false;
            p.Teleport(best + Vector3.up * 0.05f);
            return true;
        }

        void Update()
        {
            var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            var players = world?.AllAlivePlayersList;
            if (players == null || _cells.Count == 0) return;
            var now = Time.unscaledTime;
            _clock.Restart();
            for (var i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null) continue;
                var at = p.Position;
                if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, Below + 0.5f, WalkMask, QueryTriggerInteraction.Ignore)) continue;
                int cx = Mathf.FloorToInt(at.x / Cell), cz = Mathf.FloorToInt(at.z / Cell);
                var back = 0;
                for (var x = cx - 1; x <= cx + 1; x++)
                    for (var z = cz - 1; z <= cz + 1; z++)
                    {
                        var key = Key(x, z);
                        if (_repaired.TryGetValue(key, out var until) && now < until) continue;
                        _repaired[key] = now + Recheck;
                        if (!_cells.TryGetValue(key, out var list)) continue;
                        foreach (var c in list)
                        {
                            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                            _asked++;
                            if (WorldFinds(c, at)) continue;
                            c.enabled = false;
                            c.enabled = true;
                            back++;
                        }
                    }
                if (back > 0) { _restored += back; _events++; }
                if (Rescue(p, at)) _rescued++;
            }
            var ms = (float)_clock.Elapsed.TotalMilliseconds;
            _spentMs += ms;
            if (ms > _worstMs) _worstMs = ms;
            if (now < _nextReport) return;
            if (_events == 0 && _rescued == 0 && _worstMs < 5f) { _spentMs = _worstMs = 0f; _asked = 0; _nextReport = now + 5f; return; }
            Plugin.Log.LogWarning($"[CC] floor guard: {_restored} collider(s) under {_events} character check(s) had left the physics world and were put back; " +
                                  $"{_rescued} character(s) lifted back onto the ground; {_asked} collider(s) asked for, {_spentMs:F0} ms in all, worst frame {_worstMs:F1} ms");
            _restored = _events = _rescued = _asked = 0;
            _spentMs = _worstMs = 0f;
            _nextReport = now + 5f;
        }
    }
}

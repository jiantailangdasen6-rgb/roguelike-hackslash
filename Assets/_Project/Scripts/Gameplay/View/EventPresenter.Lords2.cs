using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// The second lords' trials on screen: the reaper's rattling coffin, the choir's three sweeping beams, the void's
    /// gaze following its target, the wolf's mark over its prey, and the bursts that tell a trial was overcome.
    /// </summary>
    public sealed partial class EventPresenter
    {
        private sealed class Beams
        {
            public Fx[] Lines;
            public Vector2 Centre;
            public float Start, Duration, Turn, Angle;
        }

        private readonly List<Beams> _beams = new();
        private (Fx Line, ulong Target, Vector2 Eye, float Until)? _gaze;
        private (Fx Mark, ulong Target, float Until)? _huntMark;

        private void Lords2Trial(in SimEvent e)
        {
            switch ((TrialMoment)e.Int)
            {
                case TrialMoment.CoffinsRise:
                    CameraShake.Add(0.4f);
                    ShowCallout(e.Pos + Vector2.up * 2.6f, "棺の中", new Color(0.85f, 0.8f, 0.7f), 46);
                    break;
                case TrialMoment.CoffinRattle:
                    Burst("fx_puff", e.Pos + Vector2.up * 0.2f, 12f, 1.1f, new Color(0.6f, 0.55f, 0.5f, 0.8f));
                    for (int i = 0; i < 3; i++) Shard(e.Pos + Vector2.up * 0.8f, 60f + i * 30f, new Color(0.7f, 0.65f, 0.55f));
                    break;
                case TrialMoment.CoffinWrong:
                    ShowCallout(e.Pos + Vector2.up * 1.8f, "空の棺", new Color(0.8f, 0.7f, 0.6f), 34);
                    break;
                case TrialMoment.ReaperBursts:
                    ShowCallout(e.Pos + Vector2.up * 3f, "死神の帰還", new Color(0.75f, 0.6f, 1f), 48);
                    break;
                case TrialMoment.BeamsStart:
                {
                    var b = new Beams { Centre = e.Pos, Start = Time.time, Duration = e.Value, Turn = e.Id, Angle = Mathf.Atan2(e.Vec.y, e.Vec.x) * Mathf.Rad2Deg, Lines = new Fx[3] };
                    for (int k = 0; k < 3; k++)
                    {
                        var line = Spawn("fx_line", e.Pos, 0f, e.Value, 0f, 0f, new Color(1f, 0.92f, 0.6f, 0.75f), order: -290);
                        line.Go.transform.localScale = new Vector3(RunSimulation.BeamLength / 2f, RunSimulation.BeamWidth / 0.25f, 1f);
                        AddLight(line, new Color(1f, 0.9f, 0.6f), 1.4f, 4f);
                        line.Light.transform.localPosition = new Vector3(1f, 0f);
                        b.Lines[k] = line;
                    }
                    _beams.Add(b);
                    CameraShake.Add(0.4f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "聖光の回廊", new Color(1f, 0.9f, 0.6f), 46);
                    break;
                }
                case TrialMoment.GazeAim:
                {
                    var line = Spawn("fx_line", e.Pos, 0f, e.Value + 1f, 0f, 0f, new Color(0.75f, 0.35f, 1f, 0.6f), order: -290);
                    _gaze = (line, e.ClientId, e.Pos, Time.time + e.Value);
                    if (IsLocal(e.ClientId)) ShowCallout(e.Pos + Vector2.up * 2.6f, "見つめられている！ 柱の陰へ", new Color(0.85f, 0.6f, 1f), 40);
                    break;
                }
                case TrialMoment.GazeFire:
                {
                    if (_gaze.HasValue) Expire(_gaze.Value.Line);
                    _gaze = null;
                    bool blocked = e.Id == 1;
                    var ring = Spawn("fx_nova", e.Pos + Vector2.up * 0.4f, 1f, 0.4f, 0.4f, blocked ? 1.6f : 3f, blocked ? new Color(0.7f, 0.7f, 0.8f, 0.7f) : new Color(0.75f, 0.35f, 1f, 0.9f));
                    ring.FadeOut = true;
                    if (!blocked) CameraShake.Add(0.5f);
                    ShowCallout(e.Pos + Vector2.up * 1.9f, blocked ? "防いだ" : "凝視", blocked ? new Color(0.85f, 0.85f, 0.9f) : new Color(0.85f, 0.5f, 1f), 34);
                    break;
                }
                case TrialMoment.ShellUp:
                    Burst("fx_puff", e.Pos + Vector2.up * 0.4f, 12f, 2.4f, new Color(0.6f, 0.8f, 0.8f, 0.8f));
                    ShowCallout(e.Pos + Vector2.up * 3f, "殻ごもり", new Color(0.6f, 0.95f, 0.9f), 46);
                    break;
                case TrialMoment.ShellOpen:
                case TrialMoment.HuntOver:
                    ShowCallout(e.Pos + Vector2.up * 3f, "試練の終わり", new Color(0.8f, 0.8f, 0.8f), 36);
                    break;
                case TrialMoment.HuntMark:
                {
                    var mark = Spawn("fx_mark", e.Pos + Vector2.up * 1.6f, 0f, e.Value + 0.6f, 1.6f, 1.6f, new Color(1f, 0.3f, 0.25f), order: 3200);
                    AddLight(mark, new Color(1f, 0.3f, 0.2f), 1.2f, 2.5f);
                    _huntMark = (mark, e.ClientId, Time.time + e.Value + 0.6f);
                    if (IsLocal(e.ClientId)) ShowCallout(e.Pos + Vector2.up * 2.6f, "狙われている！ 柱の前へ", new Color(1f, 0.5f, 0.4f), 40);
                    break;
                }
                case TrialMoment.RowsStart:
                    CameraShake.Add(0.4f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "処刑の列", new Color(1f, 0.6f, 0.3f), 46);
                    break;
                case TrialMoment.RowsSlam:
                    ShowCallout(e.Pos + Vector2.up * 3f, "断頭の一撃", new Color(1f, 0.4f, 0.3f), 48);
                    break;
                case TrialMoment.ReaperFound:
                case TrialMoment.BeamsEnd:
                case TrialMoment.GazeEnd:
                case TrialMoment.ShellCracked:
                case TrialMoment.HuntCrash:
                case TrialMoment.RowsBroken:
                {
                    foreach (var b in _beams) foreach (var l in b.Lines) Expire(l);
                    _beams.Clear();
                    var flash = Spawn("fx_nova", e.Pos + Vector2.up * 0.8f, 1f, 0.7f, 0.5f, 8f, new Color(1f, 0.95f, 0.8f, 0.9f));
                    flash.FadeOut = true;
                    AddLight(flash, new Color(1f, 0.9f, 0.6f), 4f, 10f);
                    for (int i = 0; i < 12; i++) Shard(e.Pos + Vector2.up * 1f, i * 30f + Random.Range(0f, 20f), new Color(1f, 0.9f, 0.65f));
                    HitStop.Freeze(0.12f, -1, major: true);
                    CameraShake.Add(0.7f);
                    ShowCallout(e.Pos + Vector2.up * 3f, (TrialMoment)e.Int == TrialMoment.HuntCrash ? "激突！ 隙あり！" : "隙あり！", new Color(1f, 0.85f, 0.4f), 52);
                    break;
                }
            }
        }

        /// <summary>Ends an effect early (it fades out on its next update), unless it is already gone.</summary>
        private void Expire(Fx fx)
        {
            if (_effects.Contains(fx)) fx.Life = Mathf.Min(fx.Life, fx.Age);
        }

        /// <summary>Turns the beams, keeps the gaze on its target and the mark over the prey.</summary>
        private void UpdateLords2()
        {
            for (int i = _beams.Count - 1; i >= 0; i--)
            {
                var b = _beams[i];
                float t = Time.time - b.Start;
                if (t >= b.Duration)
                {
                    _beams.RemoveAt(i);
                    continue;
                }
                for (int k = 0; k < 3; k++)
                {
                    var tr = b.Lines[k].Go.transform;
                    tr.position = b.Centre;
                    tr.rotation = Quaternion.Euler(0f, 0f, b.Angle + b.Turn * t + k * 120f);
                    tr.localScale = new Vector3(RunSimulation.BeamLength / 2f, RunSimulation.BeamWidth / 0.25f * (0.85f + 0.15f * Mathf.Sin(Time.time * 18f + k)), 1f);
                }
            }
            if (_gaze.HasValue)
            {
                var (line, target, eye, until) = _gaze.Value;
                var player = FindPlayer(target);
                if (Time.time > until + 0.5f || player == null)
                {
                    Expire(line);
                    _gaze = null;
                }
                else
                {
                    Vector2 to = (Vector2)player.transform.position + Vector2.up * 0.5f - eye;
                    var tr = line.Go.transform;
                    tr.position = eye;
                    tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
                    float k = 1f - Mathf.Clamp01((until - Time.time) / RunSimulation.GazeAim);
                    tr.localScale = new Vector3(to.magnitude / 2f, (0.3f + 0.9f * k) / 0.25f, 1f);
                }
            }
            if (_huntMark.HasValue)
            {
                var (mark, target, until) = _huntMark.Value;
                var player = FindPlayer(target);
                if (Time.time > until || player == null) _huntMark = null;
                else mark.Go.transform.position = player.transform.position + Vector3.up * 1.8f;
            }
        }
    }
}

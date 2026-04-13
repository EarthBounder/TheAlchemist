using System;
using System.Collections.Generic;

namespace TheAlchemist.Core;

/// <summary>
/// One segment of the authored campaign: intro once, then each mission is always followed by an interlude,
/// then outro once. Sequence: Intro, (Mission, Interlude)*, Outro.
/// </summary>
public enum GameFlowBeat : byte
{
    Intro = 0,
    Mission = 1,
    Interlude = 2,
    Outro = 3
}

/// <summary>Maps linear beat indices to <see cref="GameFlowBeat"/> and mission/interlude ordinals.</summary>
public static class GameFlowSchedule
{
    /// <summary>Number of beats for <paramref name="missionCount"/> missions (intro + pairs + outro).</summary>
    public static int TotalBeats(int missionCount)
    {
        if (missionCount < 0)
            throw new ArgumentOutOfRangeException(nameof(missionCount));
        // Intro, (Mission, Interlude) * N, Outro
        return 2 + 2 * missionCount;
    }

    public static GameFlowBeat GetBeat(int beatIndex, int missionCount)
    {
        int total = TotalBeats(missionCount);
        if ((uint)beatIndex >= (uint)total)
            throw new ArgumentOutOfRangeException(nameof(beatIndex));

        if (beatIndex == 0)
            return GameFlowBeat.Intro;
        if (beatIndex == total - 1)
            return GameFlowBeat.Outro;

        // Beats 1..total-2 alternate Mission (odd) / Interlude (even).
        return (beatIndex & 1) == 1 ? GameFlowBeat.Mission : GameFlowBeat.Interlude;
    }

    /// <summary>Mission index in [0, <paramref name="missionCount"/>) when beat is a mission.</summary>
    public static int MissionOrdinal(int beatIndex, int missionCount)
    {
        if (GetBeat(beatIndex, missionCount) != GameFlowBeat.Mission)
            throw new InvalidOperationException("Current beat is not a mission.");
        return (beatIndex - 1) / 2;
    }

    /// <summary>Interlude index in [0, <paramref name="missionCount"/>) when beat is an interlude.</summary>
    public static int InterludeOrdinal(int beatIndex, int missionCount)
    {
        if (GetBeat(beatIndex, missionCount) != GameFlowBeat.Interlude)
            throw new InvalidOperationException("Current beat is not an interlude.");
        return beatIndex / 2 - 1;
    }
}

/// <summary>Runtime position in a <see cref="GameFlowSchedule"/> for one campaign run.</summary>
public sealed class GameFlowProgress
{
    private int _beatIndex;

    public GameFlowProgress(int missionCount, IReadOnlyList<string> missionIds = null)
    {
        if (missionCount < 0)
            throw new ArgumentOutOfRangeException(nameof(missionCount));
        if (missionIds != null && missionIds.Count != missionCount)
            throw new ArgumentException("missionIds.Count must equal missionCount.", nameof(missionIds));

        MissionCount = missionCount;
        MissionIds = missionIds ?? Array.Empty<string>();
        _beatIndex = 0;
    }

    public int MissionCount { get; }

    /// <summary>Optional stable ids for content (maps, dialogue keys). Same length as <see cref="MissionCount"/>.</summary>
    public IReadOnlyList<string> MissionIds { get; }

    public int CurrentBeatIndex => _beatIndex;

    public int TotalBeats => GameFlowSchedule.TotalBeats(MissionCount);

    public GameFlowBeat CurrentBeat => GameFlowSchedule.GetBeat(_beatIndex, MissionCount);

    public bool IsBeforeOutro => _beatIndex < TotalBeats - 1;

    public bool IsComplete => _beatIndex >= TotalBeats;

    /// <summary>Content id for the current mission beat, or null if not on a mission or no ids were supplied.</summary>
    public string CurrentMissionContentId
    {
        get
        {
            if (CurrentBeat != GameFlowBeat.Mission)
                return null;
            int o = GameFlowSchedule.MissionOrdinal(_beatIndex, MissionCount);
            if ((uint)o >= (uint)MissionIds.Count || string.IsNullOrEmpty(MissionIds[o]))
                return null;
            return MissionIds[o];
        }
    }

    /// <summary>Advance to the next beat after the current segment ends. Returns false if already past outro.</summary>
    public bool TryAdvance()
    {
        if (_beatIndex >= TotalBeats)
            return false;
        _beatIndex++;
        return _beatIndex <= TotalBeats;
    }

    /// <summary>Jump for debug or saves; clamped to [0, <see cref="TotalBeats"/>].</summary>
    public void SetBeatIndex(int beatIndex)
    {
        _beatIndex = Math.Clamp(beatIndex, 0, TotalBeats);
    }
}

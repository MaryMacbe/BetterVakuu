using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Events;

namespace ModTest.Scripts.Planning;

public sealed class HeuristicEventScorer
{
    private static readonly IReadOnlyDictionary<string, float> KeywordWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        ["relic"] = 4f,
        ["upgrade"] = 2.75f,
        ["remove"] = 2.5f,
        ["transform"] = 2.25f,
        ["gold"] = 2f,
        ["potion"] = 1.75f,
        ["card"] = 1.25f,
        ["damage"] = -1.5f,
        ["lose hp"] = -2.5f,
        ["hp loss"] = -2.5f,
        ["curse"] = -3f,
        ["wound"] = -1.5f,
        ["injury"] = -1.5f,
        ["pay"] = -1.25f,
        ["leave"] = -0.25f,
        ["ignore"] = -0.5f
    };

    public float ScoreOption(PlanningSnapshot snapshot, EventOption option)
    {
        if (option.IsLocked)
        {
            return float.NegativeInfinity;
        }

        if (option.WillKillPlayer?.Invoke(snapshot.Player) == true)
        {
            return float.NegativeInfinity;
        }

        float score = option.IsProceed ? 0.5f : 2f;
        if (option.Relic != null)
        {
            score += 4f;
        }

        string text = $"{option.TextKey} {option.Title.GetRawText()} {option.Description.GetRawText()}".ToLowerInvariant();
        foreach ((string keyword, float weight) in KeywordWeights)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += weight;
            }
        }

        float playerHealthRatio = (float)snapshot.Player.Creature.CurrentHp / Math.Max(1, snapshot.Player.Creature.MaxHp);
        if (playerHealthRatio <= 0.4f && (text.Contains("damage", StringComparison.OrdinalIgnoreCase) || text.Contains("lose hp", StringComparison.OrdinalIgnoreCase)))
        {
            score -= 2.5f;
        }

        return score;
    }
}

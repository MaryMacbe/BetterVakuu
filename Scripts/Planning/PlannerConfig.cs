using Godot;

namespace ModTest.Scripts.Planning;

public static class PlannerConfig
{
    public const Key ToggleKey = Key.F7;
    public const float UiPollIntervalSeconds = 0.1f;
    public const float RewardScreenStartDelaySeconds = 0.1f;
    public const float MapScreenStartDelaySeconds = 0.15f;
    public const float CardRewardStartDelaySeconds = 0.45f;
    public const int UiClickDelayMs = 150;
    public const int MapLookaheadDepth = 4;
    public const float MapLookaheadDiscount = 0.8f;
}

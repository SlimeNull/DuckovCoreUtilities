using SlimeNull.DuckovCustomDeath.Configuration;

namespace SlimeNull.DuckovCustomDeath.Gameplay
{
    internal static class DeathDropPolicy
    {
        public const int LowQualityMaximum = 2;

        public static bool ShouldDropBackpackItem(DeathDropMode mode, int quality)
        {
            switch (mode)
            {
                case DeathDropMode.LowQualityBackpackOnly:
                    return quality <= LowQualityMaximum;
                case DeathDropMode.BackpackOnly:
                    return true;
                case DeathDropMode.None:
                    return false;
                default:
                    return true;
            }
        }
    }
}

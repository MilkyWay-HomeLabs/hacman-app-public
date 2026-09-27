namespace hacman_app_back.Modules.Achievements.Domain;

/// <summary>
/// The 13 fixed achievement categories, matching the Postgres enum
/// <c>achievement_category_enum</c> (repo/docs/hacman/infra/001_achievements_schema.sql,
/// extended by repo/docs/hacman/infra/003_achievements_telemetry_schema.sql with the
/// M7 telemetry tier: COMBAT, POWER_UPS, SURVIVAL).
/// </summary>
public enum AchievementCategory
{
    COLLECTION,
    MASTERY_EASY,
    MASTERY_MEDIUM,
    MASTERY_HARD,
    COMPLETION,
    VOLUME,
    VARIETY,
    SPEED,
    RANKING,
    LONGEVITY,
    COMBAT,
    POWER_UPS,
    SURVIVAL
}

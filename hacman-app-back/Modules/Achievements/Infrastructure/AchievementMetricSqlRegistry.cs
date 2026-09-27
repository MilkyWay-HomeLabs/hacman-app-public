namespace hacman_app_back.Modules.Achievements.Infrastructure;

/// <summary>
/// SQL text for every achievement metric this module currently knows how to compute, keyed
/// by <c>achievement_def.metric</c> exactly as spelled in
/// <c>repo/docs/hacman/infra/002_achievements_seed.sql</c>. Each query takes exactly one
/// parameter, <c>@userId</c>, and returns a single scalar — the raw counter
/// (<see cref="hacman_app_back.Modules.Achievements.Domain.AchievementLevelCalculator"/>
/// turns that into a level; nothing here computes a level).
///
/// Every <c>_pct</c>-suffixed metric computes its denominator live, with a
/// <c>COUNT(DISTINCT ...)</c> against this environment's own <c>galleries</c> table, inside
/// the same query — never a hardcoded constant. This is load-bearing (CLAUDE.md §3): prod
/// and test_hacman do not share the same content volume, so a query sized against one
/// environment's gallery/type/theme count would make achievements permanently unreachable
/// in the other.
///
/// All 100 of the v1 catalogue's metrics are implemented here (ROADMAP.md M3-2 complete):
/// COLLECTION, MASTERY_EASY/MEDIUM/HARD, COMPLETION, VOLUME, VARIETY, SPEED, RANKING and
/// LONGEVITY, 10 categories x 10 achievements each — plus the 30-metric M7 telemetry tier
/// (COMBAT, POWER_UPS, SURVIVAL; ROADMAP.md M7-2a, DECISIONS.md D-011), reading the
/// enemies_defeated/buffs_collected/hits_taken columns added by
/// infra/003_achievements_telemetry_schema.sql. 130 entries total. Adding a new one (e.g.
/// after a future catalogue expansion) is: pick a metric name that already exists in the
/// seed file, add one entry below. An achievement whose metric has no entry here simply
/// never accrues progress yet — same as any achievement before its counter first moves
/// (CLAUDE.md §3), not an error.
/// </summary>
public static class AchievementMetricSqlRegistry
{
    public static readonly IReadOnlyDictionary<string, string> Sql = new Dictionary<string, string>
    {
        // COL-01 — % of the archive unlocked.
        ["galleries_unlocked_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (SELECT COUNT(*) FROM user_galleries WHERE user_id = @userId AND is_unlocked)
                / NULLIF((SELECT COUNT(*) FROM galleries), 0)
            ), 0)::bigint;",

        // COL-02 — % of available gallery types touched by at least one unlock.
        ["gallery_type_coverage_unlocked_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.type)
                    FROM user_galleries ug
                    INNER JOIN galleries g ON g.id = ug.gallery_id
                    WHERE ug.user_id = @userId AND ug.is_unlocked
                )
                / NULLIF((SELECT COUNT(DISTINCT type) FROM galleries), 0)
            ), 0)::bigint;",

        // COL-03 — same as COL-02, for theme.
        ["gallery_theme_coverage_unlocked_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.theme)
                    FROM user_galleries ug
                    INNER JOIN galleries g ON g.id = ug.gallery_id
                    WHERE ug.user_id = @userId AND ug.is_unlocked
                )
                / NULLIF((SELECT COUNT(DISTINCT theme) FROM galleries), 0)
            ), 0)::bigint;",

        // COL-04 — same as COL-02, for subtype.
        ["gallery_subtype_coverage_unlocked_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.subtype)
                    FROM user_galleries ug
                    INNER JOIN galleries g ON g.id = ug.gallery_id
                    WHERE ug.user_id = @userId AND ug.is_unlocked
                )
                / NULLIF((SELECT COUNT(DISTINCT subtype) FROM galleries), 0)
            ), 0)::bigint;",

        // COL-05 — % of every image in the archive reachable through an unlocked gallery
        // (galleries.image_count summed, not a row count).
        ["images_collected_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COALESCE(SUM(g.image_count), 0)
                    FROM user_galleries ug
                    INNER JOIN galleries g ON g.id = ug.gallery_id
                    WHERE ug.user_id = @userId AND ug.is_unlocked
                )
                / NULLIF((SELECT SUM(image_count) FROM galleries), 0)
            ), 0)::bigint;",

        // COL-06 — most unlocks the user has ever landed within one calendar day.
        ["unlocks_in_a_day"] = @"
            SELECT COALESCE(MAX(daily_count), 0) FROM (
                SELECT COUNT(*) AS daily_count
                FROM user_galleries
                WHERE user_id = @userId AND is_unlocked AND unlocked_at IS NOT NULL
                GROUP BY date_trunc('day', unlocked_at)
            ) by_day;",

        // COL-07 — same as COL-06, over a calendar week.
        ["unlocks_in_a_week"] = @"
            SELECT COALESCE(MAX(weekly_count), 0) FROM (
                SELECT COUNT(*) AS weekly_count
                FROM user_galleries
                WHERE user_id = @userId AND is_unlocked AND unlocked_at IS NOT NULL
                GROUP BY date_trunc('week', unlocked_at)
            ) by_week;",

        // COL-08 — of the 5 earliest-added galleries in the archive, how many this user has
        // unlocked (0-5, an absolute count on the explicit >=5-galleries assumption flagged
        // in infra/002_achievements_seed.sql, not a live-denominator _pct metric).
        ["first_galleries_unlocked"] = @"
            WITH first_five AS (
                SELECT id FROM galleries ORDER BY created_at ASC, gallery_id ASC LIMIT 5
            )
            SELECT COUNT(*)
            FROM user_galleries ug
            INNER JOIN first_five f ON f.id = ug.gallery_id
            WHERE ug.user_id = @userId AND ug.is_unlocked;",

        // COL-09 — % of the archive's oldest quartile (""legacy"") galleries this user has
        // unlocked. The legacy cutoff is the archive's own 25th-percentile created_at, computed
        // live — it is always ~25% of the current catalogue, never a fixed count, so it holds
        // whether the archive has 10 galleries or 1000 (CLAUDE.md §3). The other
        // *_legacy_galleries_*_pct metrics (EZY-10/MED-10/HRD-10) reuse this same cutoff.
        ["legacy_galleries_unlocked_pct"] = @"
            WITH legacy AS (
                SELECT id FROM galleries
                WHERE created_at <= to_timestamp((
                    SELECT percentile_cont(0.25) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM created_at))
                    FROM galleries
                ))
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(*) FROM user_galleries ug
                    INNER JOIN legacy l ON l.id = ug.gallery_id
                    WHERE ug.user_id = @userId AND ug.is_unlocked
                )
                / NULLIF((SELECT COUNT(*) FROM legacy), 0)
            ), 0)::bigint;",

        // COL-10 — % of available gallery types where this user holds every gallery of that
        // type unlocked (a fully-owned ""set""), out of however many types currently exist.
        ["type_sets_fully_unlocked_pct"] = @"
            WITH type_totals AS (
                SELECT type, COUNT(*) AS total FROM galleries GROUP BY type
            ),
            type_unlocked AS (
                SELECT g.type, COUNT(*) AS unlocked
                FROM user_galleries ug
                INNER JOIN galleries g ON g.id = ug.gallery_id
                WHERE ug.user_id = @userId AND ug.is_unlocked
                GROUP BY g.type
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(*) FROM type_totals tt
                    INNER JOIN type_unlocked tu ON tu.type = tt.type AND tu.unlocked = tt.total
                )
                / NULLIF((SELECT COUNT(*) FROM type_totals), 0)
            ), 0)::bigint;",

        // EZY-01 — clears on EASY.
        ["easy_clears_total"] = @"
            SELECT COUNT(*) FROM level_scores WHERE user_id = @userId AND difficulty = 'EASY'::difficulty;",

        // MED-01 — clears on MEDIUM.
        ["medium_clears_total"] = @"
            SELECT COUNT(*) FROM level_scores WHERE user_id = @userId AND difficulty = 'MEDIUM'::difficulty;",

        // HRD-01 — clears on HARD.
        ["hard_clears_total"] = @"
            SELECT COUNT(*) FROM level_scores WHERE user_id = @userId AND difficulty = 'HARD'::difficulty;",

        // EZY-02 — % of the archive with at least one EASY clear.
        ["easy_gallery_coverage_cleared_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT gallery_id) FROM level_scores
                    WHERE user_id = @userId AND difficulty = 'EASY'::difficulty
                )
                / NULLIF((SELECT COUNT(*) FROM galleries), 0)
            ), 0)::bigint;",

        // EZY-03 — EASY clears faster than the 5-minute half-time bar.
        ["easy_sub_half_time_clears"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'EASY'::difficulty
              AND time_ms IS NOT NULL AND time_ms < 300000;",

        // EZY-04 — EASY clears faster than the 2.5-minute quarter-time bar.
        ["easy_sub_quarter_time_clears"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'EASY'::difficulty
              AND time_ms IS NOT NULL AND time_ms < 150000;",

        // EZY-05 — total clear time accumulated on EASY, in ms.
        ["easy_marathon_time_ms"] = @"
            SELECT COALESCE(SUM(time_ms), 0) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'EASY'::difficulty AND time_ms IS NOT NULL;",

        // EZY-06 — distinct calendar days with at least one EASY clear.
        ["easy_daily_grind_days"] = @"
            SELECT COUNT(DISTINCT date_trunc('day', created_at)) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'EASY'::difficulty;",

        // EZY-07 — % of available gallery types cleared on EASY.
        ["easy_type_coverage_cleared_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.type)
                    FROM level_scores ls
                    INNER JOIN galleries g ON g.id = ls.gallery_id
                    WHERE ls.user_id = @userId AND ls.difficulty = 'EASY'::difficulty
                )
                / NULLIF((SELECT COUNT(DISTINCT type) FROM galleries), 0)
            ), 0)::bigint;",

        // EZY-08 — % of available gallery themes cleared on EASY.
        ["easy_theme_coverage_cleared_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.theme)
                    FROM level_scores ls
                    INNER JOIN galleries g ON g.id = ls.gallery_id
                    WHERE ls.user_id = @userId AND ls.difficulty = 'EASY'::difficulty
                )
                / NULLIF((SELECT COUNT(DISTINCT theme) FROM galleries), 0)
            ), 0)::bigint;",

        // EZY-09 — EASY clears faster than the 7.5-minute consistency bar.
        ["easy_speed_consistency_clears"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'EASY'::difficulty
              AND time_ms IS NOT NULL AND time_ms < 450000;",

        // EZY-10 — % of the legacy (oldest-quartile) galleries cleared on EASY. Reuses the
        // same live 25th-percentile cutoff as COL-09's legacy cohort.
        ["easy_legacy_galleries_cleared_pct"] = @"
            WITH legacy AS (
                SELECT id FROM galleries
                WHERE created_at <= to_timestamp((
                    SELECT percentile_cont(0.25) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM created_at))
                    FROM galleries
                ))
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT ls.gallery_id) FROM level_scores ls
                    INNER JOIN legacy l ON l.id = ls.gallery_id
                    WHERE ls.user_id = @userId AND ls.difficulty = 'EASY'::difficulty
                )
                / NULLIF((SELECT COUNT(*) FROM legacy), 0)
            ), 0)::bigint;",

        // MED-02 — % of the archive with at least one MEDIUM clear.
        ["medium_gallery_coverage_cleared_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT gallery_id) FROM level_scores
                    WHERE user_id = @userId AND difficulty = 'MEDIUM'::difficulty
                )
                / NULLIF((SELECT COUNT(*) FROM galleries), 0)
            ), 0)::bigint;",

        // MED-03 — MEDIUM clears faster than the 2.5-minute half-time bar.
        ["medium_sub_half_time_clears"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'MEDIUM'::difficulty
              AND time_ms IS NOT NULL AND time_ms < 150000;",

        // MED-04 — MEDIUM clears faster than the 75-second quarter-time bar.
        ["medium_sub_quarter_time_clears"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'MEDIUM'::difficulty
              AND time_ms IS NOT NULL AND time_ms < 75000;",

        // MED-05 — total clear time accumulated on MEDIUM, in ms.
        ["medium_marathon_time_ms"] = @"
            SELECT COALESCE(SUM(time_ms), 0) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'MEDIUM'::difficulty AND time_ms IS NOT NULL;",

        // MED-06 — distinct calendar days with at least one MEDIUM clear.
        ["medium_daily_grind_days"] = @"
            SELECT COUNT(DISTINCT date_trunc('day', created_at)) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'MEDIUM'::difficulty;",

        // MED-07 — % of available gallery types cleared on MEDIUM.
        ["medium_type_coverage_cleared_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.type)
                    FROM level_scores ls
                    INNER JOIN galleries g ON g.id = ls.gallery_id
                    WHERE ls.user_id = @userId AND ls.difficulty = 'MEDIUM'::difficulty
                )
                / NULLIF((SELECT COUNT(DISTINCT type) FROM galleries), 0)
            ), 0)::bigint;",

        // MED-08 — % of available gallery themes cleared on MEDIUM.
        ["medium_theme_coverage_cleared_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.theme)
                    FROM level_scores ls
                    INNER JOIN galleries g ON g.id = ls.gallery_id
                    WHERE ls.user_id = @userId AND ls.difficulty = 'MEDIUM'::difficulty
                )
                / NULLIF((SELECT COUNT(DISTINCT theme) FROM galleries), 0)
            ), 0)::bigint;",

        // MED-09 — MEDIUM clears faster than the 3.75-minute consistency bar.
        ["medium_speed_consistency_clears"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'MEDIUM'::difficulty
              AND time_ms IS NOT NULL AND time_ms < 225000;",

        // MED-10 — % of the legacy (oldest-quartile) galleries cleared on MEDIUM.
        ["medium_legacy_galleries_cleared_pct"] = @"
            WITH legacy AS (
                SELECT id FROM galleries
                WHERE created_at <= to_timestamp((
                    SELECT percentile_cont(0.25) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM created_at))
                    FROM galleries
                ))
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT ls.gallery_id) FROM level_scores ls
                    INNER JOIN legacy l ON l.id = ls.gallery_id
                    WHERE ls.user_id = @userId AND ls.difficulty = 'MEDIUM'::difficulty
                )
                / NULLIF((SELECT COUNT(*) FROM legacy), 0)
            ), 0)::bigint;",

        // HRD-02 — % of the archive with at least one HARD clear.
        ["hard_gallery_coverage_cleared_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT gallery_id) FROM level_scores
                    WHERE user_id = @userId AND difficulty = 'HARD'::difficulty
                )
                / NULLIF((SELECT COUNT(*) FROM galleries), 0)
            ), 0)::bigint;",

        // HRD-03 — HARD clears faster than the 3-minute half-time bar.
        ["hard_sub_half_time_clears"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'HARD'::difficulty
              AND time_ms IS NOT NULL AND time_ms < 180000;",

        // HRD-04 — HARD clears faster than the 90-second quarter-time bar.
        ["hard_sub_quarter_time_clears"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'HARD'::difficulty
              AND time_ms IS NOT NULL AND time_ms < 90000;",

        // HRD-05 — total clear time accumulated on HARD, in ms.
        ["hard_marathon_time_ms"] = @"
            SELECT COALESCE(SUM(time_ms), 0) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'HARD'::difficulty AND time_ms IS NOT NULL;",

        // HRD-06 — distinct calendar days with at least one HARD clear.
        ["hard_daily_grind_days"] = @"
            SELECT COUNT(DISTINCT date_trunc('day', created_at)) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'HARD'::difficulty;",

        // HRD-07 — % of available gallery types cleared on HARD.
        ["hard_type_coverage_cleared_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.type)
                    FROM level_scores ls
                    INNER JOIN galleries g ON g.id = ls.gallery_id
                    WHERE ls.user_id = @userId AND ls.difficulty = 'HARD'::difficulty
                )
                / NULLIF((SELECT COUNT(DISTINCT type) FROM galleries), 0)
            ), 0)::bigint;",

        // HRD-08 — % of available gallery themes cleared on HARD.
        ["hard_theme_coverage_cleared_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.theme)
                    FROM level_scores ls
                    INNER JOIN galleries g ON g.id = ls.gallery_id
                    WHERE ls.user_id = @userId AND ls.difficulty = 'HARD'::difficulty
                )
                / NULLIF((SELECT COUNT(DISTINCT theme) FROM galleries), 0)
            ), 0)::bigint;",

        // HRD-09 — HARD clears faster than the 4.5-minute consistency bar.
        ["hard_speed_consistency_clears"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'HARD'::difficulty
              AND time_ms IS NOT NULL AND time_ms < 270000;",

        // HRD-10 — % of the legacy (oldest-quartile) galleries cleared on HARD.
        ["hard_legacy_galleries_cleared_pct"] = @"
            WITH legacy AS (
                SELECT id FROM galleries
                WHERE created_at <= to_timestamp((
                    SELECT percentile_cont(0.25) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM created_at))
                    FROM galleries
                ))
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT ls.gallery_id) FROM level_scores ls
                    INNER JOIN legacy l ON l.id = ls.gallery_id
                    WHERE ls.user_id = @userId AND ls.difficulty = 'HARD'::difficulty
                )
                / NULLIF((SELECT COUNT(*) FROM legacy), 0)
            ), 0)::bigint;",

        // CPL-01 — % of the archive cleared on EASY, MEDIUM and HARD (a gallery counts once
        // the user has at least one score row at each of the three difficulties).
        ["triathlon_coverage_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(*) FROM (
                        SELECT gallery_id FROM level_scores
                        WHERE user_id = @userId
                        GROUP BY gallery_id
                        HAVING COUNT(DISTINCT difficulty) = 3
                    ) triathloned
                )
                / NULLIF((SELECT COUNT(*) FROM galleries), 0)
            ), 0)::bigint;",

        // CPL-02 — % of available gallery types with at least one triathloned gallery.
        ["triathlon_type_coverage_pct"] = @"
            WITH triathloned AS (
                SELECT gallery_id FROM level_scores
                WHERE user_id = @userId
                GROUP BY gallery_id
                HAVING COUNT(DISTINCT difficulty) = 3
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.type)
                    FROM triathloned t
                    INNER JOIN galleries g ON g.id = t.gallery_id
                )
                / NULLIF((SELECT COUNT(DISTINCT type) FROM galleries), 0)
            ), 0)::bigint;",

        // CPL-03 — % of the archive triathloned where all three times beat each
        // difficulty's own half-time bar (EZY-03/MED-03/HRD-03's thresholds).
        ["speedrun_triathlon_coverage_pct"] = @"
            WITH speedrun_triathloned AS (
                SELECT gallery_id FROM level_scores
                WHERE user_id = @userId AND time_ms IS NOT NULL
                  AND (
                    (difficulty = 'EASY'::difficulty AND time_ms < 300000) OR
                    (difficulty = 'MEDIUM'::difficulty AND time_ms < 150000) OR
                    (difficulty = 'HARD'::difficulty AND time_ms < 180000)
                  )
                GROUP BY gallery_id
                HAVING COUNT(DISTINCT difficulty) = 3
            )
            SELECT COALESCE(FLOOR(
                100.0 * (SELECT COUNT(*) FROM speedrun_triathloned)
                / NULLIF((SELECT COUNT(*) FROM galleries), 0)
            ), 0)::bigint;",

        // CPL-04 — % of available gallery themes this user owns 100% (every gallery of
        // that theme unlocked). Same ""fully-owned set"" shape as COL-10, by theme.
        ["theme_full_unlock_coverage_pct"] = @"
            WITH theme_totals AS (
                SELECT theme, COUNT(*) AS total FROM galleries GROUP BY theme
            ),
            theme_unlocked AS (
                SELECT g.theme, COUNT(*) AS unlocked
                FROM user_galleries ug
                INNER JOIN galleries g ON g.id = ug.gallery_id
                WHERE ug.user_id = @userId AND ug.is_unlocked
                GROUP BY g.theme
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(*) FROM theme_totals tt
                    INNER JOIN theme_unlocked tu ON tu.theme = tt.theme AND tu.unlocked = tt.total
                )
                / NULLIF((SELECT COUNT(*) FROM theme_totals), 0)
            ), 0)::bigint;",

        // CPL-05 — % of available gallery types fully triathloned (every gallery of that
        // type has all three difficulties cleared by this user).
        ["type_full_triathlon_coverage_pct"] = @"
            WITH type_totals AS (
                SELECT type, COUNT(*) AS total FROM galleries GROUP BY type
            ),
            triathloned AS (
                SELECT gallery_id FROM level_scores
                WHERE user_id = @userId
                GROUP BY gallery_id
                HAVING COUNT(DISTINCT difficulty) = 3
            ),
            type_triathloned AS (
                SELECT g.type, COUNT(*) AS triathloned_count
                FROM triathloned t
                INNER JOIN galleries g ON g.id = t.gallery_id
                GROUP BY g.type
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(*) FROM type_totals tt
                    INNER JOIN type_triathloned tc ON tc.type = tt.type AND tc.triathloned_count = tt.total
                )
                / NULLIF((SELECT COUNT(*) FROM type_totals), 0)
            ), 0)::bigint;",

        // CPL-06 — same as CPL-05, for subtype.
        ["subtype_full_triathlon_coverage_pct"] = @"
            WITH subtype_totals AS (
                SELECT subtype, COUNT(*) AS total FROM galleries GROUP BY subtype
            ),
            triathloned AS (
                SELECT gallery_id FROM level_scores
                WHERE user_id = @userId
                GROUP BY gallery_id
                HAVING COUNT(DISTINCT difficulty) = 3
            ),
            subtype_triathloned AS (
                SELECT g.subtype, COUNT(*) AS triathloned_count
                FROM triathloned t
                INNER JOIN galleries g ON g.id = t.gallery_id
                GROUP BY g.subtype
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(*) FROM subtype_totals st
                    INNER JOIN subtype_triathloned sc ON sc.subtype = st.subtype AND sc.triathloned_count = st.total
                )
                / NULLIF((SELECT COUNT(*) FROM subtype_totals), 0)
            ), 0)::bigint;",

        // CPL-07 — most galleries this user has ever triathloned within a single rolling
        // (calendar) week, keyed by the completion moment: the later of the two
        // completions is the third difficulty submitted for that gallery.
        ["clean_sweep_week"] = @"
            WITH triathloned AS (
                SELECT gallery_id, MAX(created_at) AS completed_at
                FROM level_scores
                WHERE user_id = @userId
                GROUP BY gallery_id
                HAVING COUNT(DISTINCT difficulty) = 3
            )
            SELECT COALESCE(MAX(weekly_count), 0) FROM (
                SELECT COUNT(*) AS weekly_count
                FROM triathloned
                GROUP BY date_trunc('week', completed_at)
            ) by_week;",

        // CPL-08 — same as CPL-07, over a single calendar day.
        ["clean_sweep_day"] = @"
            WITH triathloned AS (
                SELECT gallery_id, MAX(created_at) AS completed_at
                FROM level_scores
                WHERE user_id = @userId
                GROUP BY gallery_id
                HAVING COUNT(DISTINCT difficulty) = 3
            )
            SELECT COALESCE(MAX(daily_count), 0) FROM (
                SELECT COUNT(*) AS daily_count
                FROM triathloned
                GROUP BY date_trunc('day', completed_at)
            ) by_day;",

        // CPL-09 — same as CPL-02, for theme.
        ["triathlon_theme_coverage_pct"] = @"
            WITH triathloned AS (
                SELECT gallery_id FROM level_scores
                WHERE user_id = @userId
                GROUP BY gallery_id
                HAVING COUNT(DISTINCT difficulty) = 3
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.theme)
                    FROM triathloned t
                    INNER JOIN galleries g ON g.id = t.gallery_id
                )
                / NULLIF((SELECT COUNT(DISTINCT theme) FROM galleries), 0)
            ), 0)::bigint;",

        // CPL-10 — same as CPL-02, for subtype.
        ["triathlon_subtype_coverage_pct"] = @"
            WITH triathloned AS (
                SELECT gallery_id FROM level_scores
                WHERE user_id = @userId
                GROUP BY gallery_id
                HAVING COUNT(DISTINCT difficulty) = 3
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.subtype)
                    FROM triathloned t
                    INNER JOIN galleries g ON g.id = t.gallery_id
                )
                / NULLIF((SELECT COUNT(DISTINCT subtype) FROM galleries), 0)
            ), 0)::bigint;",

        // VOL-01 — level clears submitted, any difficulty.
        ["total_clears"] = @"
            SELECT COUNT(*) FROM level_scores WHERE user_id = @userId;",

        // VOL-02 — % of the archive played, any difficulty (at least one clear submitted).
        ["gallery_coverage_played_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (SELECT COUNT(DISTINCT gallery_id) FROM level_scores WHERE user_id = @userId)
                / NULLIF((SELECT COUNT(*) FROM galleries), 0)
            ), 0)::bigint;",

        // VOL-03 — total clear time accumulated, any difficulty, in ms.
        ["total_playtime_ms"] = @"
            SELECT COALESCE(SUM(time_ms), 0) FROM level_scores
            WHERE user_id = @userId AND time_ms IS NOT NULL;",

        // VOL-04 — distinct calendar days with at least one clear, any difficulty.
        ["total_days_active"] = @"
            SELECT COUNT(DISTINCT date_trunc('day', created_at)) FROM level_scores WHERE user_id = @userId;",

        // VOL-05 — most clears submitted within a single rolling (calendar) week, any difficulty.
        ["weekly_regular"] = @"
            SELECT COALESCE(MAX(weekly_count), 0) FROM (
                SELECT COUNT(*) AS weekly_count FROM level_scores
                WHERE user_id = @userId
                GROUP BY date_trunc('week', created_at)
            ) by_week;",

        // VOL-06 — most clears submitted within a single calendar month, any difficulty.
        ["monthly_marathoner"] = @"
            SELECT COALESCE(MAX(monthly_count), 0) FROM (
                SELECT COUNT(*) AS monthly_count FROM level_scores
                WHERE user_id = @userId
                GROUP BY date_trunc('month', created_at)
            ) by_month;",

        // VOL-07 — clears beyond the first for any (gallery, difficulty) pair this user has
        // already cleared before (total submissions minus distinct pairs).
        ["replays"] = @"
            WITH counts AS (
                SELECT COUNT(*) AS total, COUNT(DISTINCT (gallery_id, difficulty)) AS distinct_pairs
                FROM level_scores WHERE user_id = @userId
            )
            SELECT COALESCE(total - distinct_pairs, 0) FROM counts;",

        // VOL-08 — distinct calendar days on which this user played 2 or more difficulties.
        ["easy_medium_hard_mix"] = @"
            SELECT COUNT(*) FROM (
                SELECT date_trunc('day', created_at) AS d FROM level_scores
                WHERE user_id = @userId
                GROUP BY d
                HAVING COUNT(DISTINCT difficulty) >= 2
            ) mixed_days;",

        // VOL-09 — days elapsed since this user's very first clear submission.
        ["veteran_account"] = @"
            SELECT COALESCE(EXTRACT(DAY FROM (now() - (
                SELECT MIN(created_at) FROM level_scores WHERE user_id = @userId
            )))::bigint, 0);",

        // VOL-10 — total minutes beaten off each difficulty's own countdown clock, across all
        // clears. Reuses EZY/MED/HRD-03's half-time bars (300000/150000/180000ms) doubled to
        // their full countdown length (600000/300000/360000ms) — the same design constants,
        // not new ones.
        ["grand_total_time_saved_min"] = @"
            SELECT COALESCE(FLOOR(SUM(
                CASE difficulty
                    WHEN 'EASY'::difficulty THEN GREATEST(600000 - time_ms, 0)
                    WHEN 'MEDIUM'::difficulty THEN GREATEST(300000 - time_ms, 0)
                    WHEN 'HARD'::difficulty THEN GREATEST(360000 - time_ms, 0)
                END
            ) / 60000.0), 0)::bigint
            FROM level_scores
            WHERE user_id = @userId AND time_ms IS NOT NULL;",

        // VAR-01 — % of available gallery types played (at least one score on a gallery of that type).
        ["type_coverage_played_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.type)
                    FROM level_scores ls
                    INNER JOIN galleries g ON g.id = ls.gallery_id
                    WHERE ls.user_id = @userId
                )
                / NULLIF((SELECT COUNT(DISTINCT type) FROM galleries), 0)
            ), 0)::bigint;",

        // VAR-02 — % of available gallery themes played (at least one score on a gallery of that theme).
        ["theme_coverage_played_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.theme)
                    FROM level_scores ls
                    INNER JOIN galleries g ON g.id = ls.gallery_id
                    WHERE ls.user_id = @userId
                )
                / NULLIF((SELECT COUNT(DISTINCT theme) FROM galleries), 0)
            ), 0)::bigint;",

        // VAR-03 — same as VAR-02, for subtype.
        ["subtype_coverage_played_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.subtype)
                    FROM level_scores ls
                    INNER JOIN galleries g ON g.id = ls.gallery_id
                    WHERE ls.user_id = @userId
                )
                / NULLIF((SELECT COUNT(DISTINCT subtype) FROM galleries), 0)
            ), 0)::bigint;",

        // VAR-04 — distinct active days whose set of difficulties played differs from the
        // immediately preceding active day's set (a genuine day-to-day switch, not just a
        // multi-difficulty day — that's VOL-08's easy_medium_hard_mix).
        ["difficulty_switch_days"] = @"
            WITH daily AS (
                SELECT date_trunc('day', created_at) AS d,
                       array_agg(DISTINCT difficulty ORDER BY difficulty) AS difficulties
                FROM level_scores
                WHERE user_id = @userId
                GROUP BY d
            ),
            switches AS (
                SELECT difficulties, LAG(difficulties) OVER (ORDER BY d) AS prev_difficulties
                FROM daily
            )
            SELECT COUNT(*) FROM switches
            WHERE prev_difficulties IS NOT NULL AND difficulties <> prev_difficulties;",

        // VAR-05 — calendar days with clears spanning 3 or more distinct gallery types.
        ["type_rainbow_days"] = @"
            SELECT COUNT(*) FROM (
                SELECT date_trunc('day', ls.created_at) AS d
                FROM level_scores ls
                INNER JOIN galleries g ON g.id = ls.gallery_id
                WHERE ls.user_id = @userId
                GROUP BY d
                HAVING COUNT(DISTINCT g.type) >= 3
            ) rainbow_days;",

        // VAR-06 — this user's best single rolling week for gallery-theme coverage, as a % of
        // every theme the archive currently has.
        ["theme_coverage_hopper_week_pct"] = @"
            WITH weekly_theme_coverage AS (
                SELECT date_trunc('week', ls.created_at) AS wk, COUNT(DISTINCT g.theme) AS theme_count
                FROM level_scores ls
                INNER JOIN galleries g ON g.id = ls.gallery_id
                WHERE ls.user_id = @userId
                GROUP BY wk
            )
            SELECT COALESCE(FLOOR(
                100.0 * (SELECT COALESCE(MAX(theme_count), 0) FROM weekly_theme_coverage)
                / NULLIF((SELECT COUNT(DISTINCT theme) FROM galleries), 0)
            ), 0)::bigint;",

        // VAR-07 — % of the archive's smallest quartile (by image_count) galleries cleared.
        // The live 25th-percentile image_count cutoff mirrors COL-09's live created_at cutoff
        // for ""legacy"" — same shape, different axis, still never a hardcoded constant.
        ["underdog_gallery_coverage_pct"] = @"
            WITH smallest AS (
                SELECT id FROM galleries
                WHERE image_count <= (
                    SELECT percentile_cont(0.25) WITHIN GROUP (ORDER BY image_count) FROM galleries
                )
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT ls.gallery_id) FROM level_scores ls
                    INNER JOIN smallest s ON s.id = ls.gallery_id
                    WHERE ls.user_id = @userId
                )
                / NULLIF((SELECT COUNT(*) FROM smallest), 0)
            ), 0)::bigint;",

        // VAR-08 — same as VAR-07, for the largest quartile (image_count >= the live
        // 75th-percentile cutoff).
        ["big_gallery_coverage_pct"] = @"
            WITH biggest AS (
                SELECT id FROM galleries
                WHERE image_count >= (
                    SELECT percentile_cont(0.75) WITHIN GROUP (ORDER BY image_count) FROM galleries
                )
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT ls.gallery_id) FROM level_scores ls
                    INNER JOIN biggest b ON b.id = ls.gallery_id
                    WHERE ls.user_id = @userId
                )
                / NULLIF((SELECT COUNT(*) FROM biggest), 0)
            ), 0)::bigint;",

        // VAR-09 — of the 5 earliest-added galleries in the archive, how many this user has
        // cleared (any difficulty). Same explicit >=5-galleries assumption as COL-08, absolute
        // 0-5 count, not a live-denominator _pct metric.
        ["first_galleries_cleared"] = @"
            WITH first_five AS (
                SELECT id FROM galleries ORDER BY created_at ASC, gallery_id ASC LIMIT 5
            )
            SELECT COUNT(DISTINCT ls.gallery_id)
            FROM level_scores ls
            INNER JOIN first_five f ON f.id = ls.gallery_id
            WHERE ls.user_id = @userId;",

        // VAR-10 — % of the archive's newest quartile galleries cleared. The live
        // 75th-percentile created_at cutoff is COL-09's legacy cutoff mirrored at the other
        // end of the distribution.
        ["deep_cut_coverage_pct"] = @"
            WITH newest AS (
                SELECT id FROM galleries
                WHERE created_at >= to_timestamp((
                    SELECT percentile_cont(0.75) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM created_at))
                    FROM galleries
                ))
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT ls.gallery_id) FROM level_scores ls
                    INNER JOIN newest n ON n.id = ls.gallery_id
                    WHERE ls.user_id = @userId
                )
                / NULLIF((SELECT COUNT(*) FROM newest), 0)
            ), 0)::bigint;",

        // SPD-01 — clears in under 1 minute, any difficulty.
        ["sub_1_minute_clears_total"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND time_ms IS NOT NULL AND time_ms < 60000;",

        // SPD-02 — clears in under 2 minutes, any difficulty.
        ["sub_2_minute_clears_total"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND time_ms IS NOT NULL AND time_ms < 120000;",

        // SPD-03 — % of the archive with at least one clear under 90 seconds, any difficulty.
        ["speed_gallery_coverage_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT gallery_id) FROM level_scores
                    WHERE user_id = @userId AND time_ms IS NOT NULL AND time_ms < 90000
                )
                / NULLIF((SELECT COUNT(*) FROM galleries), 0)
            ), 0)::bigint;",

        // SPD-04 — % of available gallery types with a sub-90-second clear.
        ["speed_type_coverage_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.type)
                    FROM level_scores ls
                    INNER JOIN galleries g ON g.id = ls.gallery_id
                    WHERE ls.user_id = @userId AND ls.time_ms IS NOT NULL AND ls.time_ms < 90000
                )
                / NULLIF((SELECT COUNT(DISTINCT type) FROM galleries), 0)
            ), 0)::bigint;",

        // SPD-05 — most sub-90-second clears within a single rolling (calendar) week.
        ["speed_week"] = @"
            SELECT COALESCE(MAX(weekly_count), 0) FROM (
                SELECT COUNT(*) AS weekly_count FROM level_scores
                WHERE user_id = @userId AND time_ms IS NOT NULL AND time_ms < 90000
                GROUP BY date_trunc('week', created_at)
            ) by_week;",

        // SPD-06 — submissions that set a new personal best on their (gallery, difficulty)
        // pair, including the very first submission to a pair (nothing to beat yet, so it's
        // automatically a record).
        ["personal_record_breaks"] = @"
            WITH ordered AS (
                SELECT time_ms,
                       MIN(time_ms) OVER (
                           PARTITION BY gallery_id, difficulty ORDER BY created_at ASC
                           ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                       ) AS prior_best
                FROM level_scores
                WHERE user_id = @userId AND time_ms IS NOT NULL
            )
            SELECT COUNT(*) FROM ordered WHERE prior_best IS NULL OR time_ms < prior_best;",

        // SPD-07 — sub-90-second clears specifically on HARD.
        ["hard_speed_specialist"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'HARD'::difficulty
              AND time_ms IS NOT NULL AND time_ms < 90000;",

        // SPD-08 — same as SPD-04, for theme.
        ["speed_theme_coverage_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.theme)
                    FROM level_scores ls
                    INNER JOIN galleries g ON g.id = ls.gallery_id
                    WHERE ls.user_id = @userId AND ls.time_ms IS NOT NULL AND ls.time_ms < 90000
                )
                / NULLIF((SELECT COUNT(DISTINCT theme) FROM galleries), 0)
            ), 0)::bigint;",

        // SPD-09 — most sub-90-second clears within a single calendar day.
        ["back_to_back_speed"] = @"
            SELECT COALESCE(MAX(daily_count), 0) FROM (
                SELECT COUNT(*) AS daily_count FROM level_scores
                WHERE user_id = @userId AND time_ms IS NOT NULL AND time_ms < 90000
                GROUP BY date_trunc('day', created_at)
            ) by_day;",

        // SPD-10 — clears in under 20 seconds, any difficulty.
        ["sub_20_second_clears"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND time_ms IS NOT NULL AND time_ms < 20000;",

        // RNK-01 — top-10 leaderboard placements, ranked by each user's own best time per
        // (gallery, difficulty) — repeated submissions to the same pair only count once.
        ["top10_finishes"] = @"
            WITH best AS (
                SELECT user_id, gallery_id, difficulty, MIN(time_ms) AS best_time_ms
                FROM level_scores
                WHERE time_ms IS NOT NULL
                GROUP BY user_id, gallery_id, difficulty
            ),
            ranked AS (
                SELECT user_id, gallery_id, difficulty,
                       ROW_NUMBER() OVER (PARTITION BY gallery_id, difficulty ORDER BY best_time_ms ASC) AS rnk
                FROM best
            )
            SELECT COUNT(*) FROM ranked WHERE user_id = @userId AND rnk <= 10;",

        // RNK-02 — same shape as RNK-01, for a top-3 placement.
        ["top3_finishes"] = @"
            WITH best AS (
                SELECT user_id, gallery_id, difficulty, MIN(time_ms) AS best_time_ms
                FROM level_scores
                WHERE time_ms IS NOT NULL
                GROUP BY user_id, gallery_id, difficulty
            ),
            ranked AS (
                SELECT user_id, gallery_id, difficulty,
                       ROW_NUMBER() OVER (PARTITION BY gallery_id, difficulty ORDER BY best_time_ms ASC) AS rnk
                FROM best
            )
            SELECT COUNT(*) FROM ranked WHERE user_id = @userId AND rnk <= 3;",

        // RNK-03 — same shape as RNK-01, for the #1 placement.
        ["first_place_finishes"] = @"
            WITH best AS (
                SELECT user_id, gallery_id, difficulty, MIN(time_ms) AS best_time_ms
                FROM level_scores
                WHERE time_ms IS NOT NULL
                GROUP BY user_id, gallery_id, difficulty
            ),
            ranked AS (
                SELECT user_id, gallery_id, difficulty,
                       ROW_NUMBER() OVER (PARTITION BY gallery_id, difficulty ORDER BY best_time_ms ASC) AS rnk
                FROM best
            )
            SELECT COUNT(*) FROM ranked WHERE user_id = @userId AND rnk = 1;",

        // RNK-04 — % of this user's own played (gallery, difficulty) pairs that rank in the
        // top quartile of that pair's own participant count (live per pair, never a fixed
        // rank cutoff — a pair with 4 participants and one with 400 both use their own
        // quartile boundary).
        ["top_quartile_finish_coverage_pct"] = @"
            WITH best AS (
                SELECT user_id, gallery_id, difficulty, MIN(time_ms) AS best_time_ms
                FROM level_scores
                WHERE time_ms IS NOT NULL
                GROUP BY user_id, gallery_id, difficulty
            ),
            ranked AS (
                SELECT user_id, gallery_id, difficulty,
                       ROW_NUMBER() OVER (PARTITION BY gallery_id, difficulty ORDER BY best_time_ms ASC) AS rnk,
                       COUNT(*) OVER (PARTITION BY gallery_id, difficulty) AS participants
                FROM best
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(*) FROM ranked
                    WHERE user_id = @userId AND rnk <= CEIL(participants / 4.0)
                )
                / NULLIF((SELECT COUNT(*) FROM ranked WHERE user_id = @userId), 0)
            ), 0)::bigint;",

        // RNK-05 — % of available gallery types where this user holds a top-3 placement on
        // at least one gallery of that type.
        ["podium_type_coverage_pct"] = @"
            WITH best AS (
                SELECT user_id, gallery_id, difficulty, MIN(time_ms) AS best_time_ms
                FROM level_scores
                WHERE time_ms IS NOT NULL
                GROUP BY user_id, gallery_id, difficulty
            ),
            ranked AS (
                SELECT user_id, gallery_id, difficulty,
                       ROW_NUMBER() OVER (PARTITION BY gallery_id, difficulty ORDER BY best_time_ms ASC) AS rnk
                FROM best
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.type)
                    FROM ranked r
                    INNER JOIN galleries g ON g.id = r.gallery_id
                    WHERE r.user_id = @userId AND r.rnk <= 3
                )
                / NULLIF((SELECT COUNT(DISTINCT type) FROM galleries), 0)
            ), 0)::bigint;",

        // RNK-06 — same as RNK-05, for theme.
        ["podium_theme_coverage_pct"] = @"
            WITH best AS (
                SELECT user_id, gallery_id, difficulty, MIN(time_ms) AS best_time_ms
                FROM level_scores
                WHERE time_ms IS NOT NULL
                GROUP BY user_id, gallery_id, difficulty
            ),
            ranked AS (
                SELECT user_id, gallery_id, difficulty,
                       ROW_NUMBER() OVER (PARTITION BY gallery_id, difficulty ORDER BY best_time_ms ASC) AS rnk
                FROM best
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT g.theme)
                    FROM ranked r
                    INNER JOIN galleries g ON g.id = r.gallery_id
                    WHERE r.user_id = @userId AND r.rnk <= 3
                )
                / NULLIF((SELECT COUNT(DISTINCT theme) FROM galleries), 0)
            ), 0)::bigint;",

        // RNK-07 — submissions that beat another user's best time on that (gallery,
        // difficulty) pair at the moment of submission (""taking #1 away""), regardless of
        // whether this user still holds it now.
        ["dethroned_someone"] = @"
            WITH attempts AS (
                SELECT ls.id, ls.time_ms,
                       (SELECT MIN(o.time_ms) FROM level_scores o
                        WHERE o.gallery_id = ls.gallery_id AND o.difficulty = ls.difficulty
                          AND o.user_id <> ls.user_id AND o.created_at < ls.created_at
                          AND o.time_ms IS NOT NULL) AS other_best_before
                FROM level_scores ls
                WHERE ls.user_id = @userId AND ls.time_ms IS NOT NULL
            )
            SELECT COUNT(*) FROM attempts WHERE other_best_before IS NOT NULL AND time_ms < other_best_before;",

        // RNK-08 — same shape as RNK-01, for the #1 placement specifically on EASY.
        ["easy_champion"] = @"
            WITH best AS (
                SELECT user_id, gallery_id, MIN(time_ms) AS best_time_ms
                FROM level_scores
                WHERE time_ms IS NOT NULL AND difficulty = 'EASY'::difficulty
                GROUP BY user_id, gallery_id
            ),
            ranked AS (
                SELECT user_id, gallery_id,
                       ROW_NUMBER() OVER (PARTITION BY gallery_id ORDER BY best_time_ms ASC) AS rnk
                FROM best
            )
            SELECT COUNT(*) FROM ranked WHERE user_id = @userId AND rnk = 1;",

        // RNK-09 — same as RNK-08, for HARD.
        ["hard_champion"] = @"
            WITH best AS (
                SELECT user_id, gallery_id, MIN(time_ms) AS best_time_ms
                FROM level_scores
                WHERE time_ms IS NOT NULL AND difficulty = 'HARD'::difficulty
                GROUP BY user_id, gallery_id
            ),
            ranked AS (
                SELECT user_id, gallery_id,
                       ROW_NUMBER() OVER (PARTITION BY gallery_id ORDER BY best_time_ms ASC) AS rnk
                FROM best
            )
            SELECT COUNT(*) FROM ranked WHERE user_id = @userId AND rnk = 1;",

        // RNK-10 — % of this user's own played (gallery, difficulty) pairs ranked in the
        // absolute top 50 (unlike RNK-04, not scaled to that pair's own participant count).
        ["leaderboard_presence_coverage_pct"] = @"
            WITH best AS (
                SELECT user_id, gallery_id, difficulty, MIN(time_ms) AS best_time_ms
                FROM level_scores
                WHERE time_ms IS NOT NULL
                GROUP BY user_id, gallery_id, difficulty
            ),
            ranked AS (
                SELECT user_id, gallery_id, difficulty,
                       ROW_NUMBER() OVER (PARTITION BY gallery_id, difficulty ORDER BY best_time_ms ASC) AS rnk
                FROM best
            )
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(*) FROM ranked WHERE user_id = @userId AND rnk <= 50
                )
                / NULLIF((SELECT COUNT(*) FROM ranked WHERE user_id = @userId), 0)
            ), 0)::bigint;",

        // LNG-01 — days elapsed since account creation.
        ["account_age_days"] = @"
            SELECT COALESCE(EXTRACT(DAY FROM (now() - u.created_at))::bigint, 0)
            FROM users u WHERE u.id = @userId;",

        // LNG-02 — days elapsed since this user's first level clear. Same formula as
        // VOL-09's veteran_account (the seed catalogue genuinely defines both achievements
        // identically) — kept as separate entries because each is its own metric key.
        ["days_since_first_clear"] = @"
            SELECT COALESCE(EXTRACT(DAY FROM (now() - (
                SELECT MIN(created_at) FROM level_scores WHERE user_id = @userId
            )))::bigint, 0);",

        // LNG-03 — longest run of consecutive calendar months with at least one clear (the
        // best streak ever reached, not just the current one — a broken streak shouldn't cost
        // the player their earned tier). Classic ""gaps and islands"": a month's ordinal minus
        // its row number is constant within one consecutive run.
        ["monthly_active_streak"] = @"
            WITH active_months AS (
                SELECT DISTINCT (EXTRACT(YEAR FROM created_at) * 12 + EXTRACT(MONTH FROM created_at))::bigint AS month_ordinal
                FROM level_scores WHERE user_id = @userId
            ),
            islands AS (
                SELECT month_ordinal - ROW_NUMBER() OVER (ORDER BY month_ordinal) AS island
                FROM active_months
            )
            SELECT COALESCE(MAX(streak_len), 0) FROM (
                SELECT COUNT(*) AS streak_len FROM islands GROUP BY island
            ) streaks;",

        // LNG-04 — same ""gaps and islands"" shape as LNG-03, for consecutive calendar weeks.
        // date_trunc('week', ...) always lands on the same weekday, so dividing its epoch by
        // 604800 (seconds/week) gives a stable, gap-detectable integer per week.
        ["weekly_active_streak"] = @"
            WITH active_weeks AS (
                SELECT DISTINCT FLOOR(EXTRACT(EPOCH FROM date_trunc('week', created_at)) / 604800)::bigint AS week_ordinal
                FROM level_scores WHERE user_id = @userId
            ),
            islands AS (
                SELECT week_ordinal - ROW_NUMBER() OVER (ORDER BY week_ordinal) AS island
                FROM active_weeks
            )
            SELECT COALESCE(MAX(streak_len), 0) FROM (
                SELECT COUNT(*) AS streak_len FROM islands GROUP BY island
            ) streaks;",

        // LNG-05 — distinct calendar months with at least one clear, lifetime (unlike LNG-03,
        // not required to be consecutive).
        ["seasonal_player"] = @"
            SELECT COUNT(DISTINCT date_trunc('month', created_at)) FROM level_scores WHERE user_id = @userId;",

        // LNG-06 — number of times this user returned to an active day after a 30+ day gap
        // since their previous active day.
        ["comeback_player"] = @"
            WITH active_days AS (
                SELECT DISTINCT date_trunc('day', created_at) AS d FROM level_scores WHERE user_id = @userId
            ),
            gaps AS (
                SELECT d - LAG(d) OVER (ORDER BY d) AS gap FROM active_days
            )
            SELECT COUNT(*) FROM gaps WHERE gap >= INTERVAL '30 days';",

        // LNG-07 — clears submitted late at night (22:00-03:59, wrapping past midnight).
        ["night_owl"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId
              AND (EXTRACT(HOUR FROM created_at) >= 22 OR EXTRACT(HOUR FROM created_at) < 4);",

        // LNG-08 — clears submitted early in the morning (04:00-06:59).
        ["early_bird"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId
              AND EXTRACT(HOUR FROM created_at) >= 4 AND EXTRACT(HOUR FROM created_at) < 7;",

        // LNG-09 — clears submitted on a Saturday or Sunday (Postgres DOW: 0=Sunday, 6=Saturday).
        ["weekend_warrior"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND EXTRACT(DOW FROM created_at) IN (0, 6);",

        // LNG-10 — calendar weeks with 3 or more clears submitted in that week.
        ["consistency_score"] = @"
            SELECT COUNT(*) FROM (
                SELECT date_trunc('week', created_at) AS wk
                FROM level_scores WHERE user_id = @userId
                GROUP BY wk
                HAVING COUNT(*) >= 3
            ) consistent_weeks;",

        // ====================================================================
        // M7 telemetry tier — COMBAT, POWER_UPS, SURVIVAL (ROADMAP.md M7-2a,
        // DECISIONS.md D-011). Reads level_scores.enemies_defeated/buffs_collected/
        // hits_taken, added by infra/003_achievements_telemetry_schema.sql — client-
        // reported per hacman-game-app's score-submission payload since v1.3.0.
        // ====================================================================

        // COM-01 — lifetime enemies defeated, summed across every run.
        ["total_enemies_defeated"] = @"
            SELECT COALESCE(SUM(enemies_defeated), 0) FROM level_scores WHERE user_id = @userId;",

        // COM-02 — enemies defeated on EASY.
        ["easy_enemies_defeated_total"] = @"
            SELECT COALESCE(SUM(enemies_defeated), 0) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'EASY'::difficulty;",

        // COM-03 — enemies defeated on MEDIUM.
        ["medium_enemies_defeated_total"] = @"
            SELECT COALESCE(SUM(enemies_defeated), 0) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'MEDIUM'::difficulty;",

        // COM-04 — enemies defeated on HARD.
        ["hard_enemies_defeated_total"] = @"
            SELECT COALESCE(SUM(enemies_defeated), 0) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'HARD'::difficulty;",

        // COM-05 — most enemies defeated in a single run.
        ["best_single_run_kills"] = @"
            SELECT COALESCE(MAX(enemies_defeated), 0) FROM level_scores WHERE user_id = @userId;",

        // COM-06 — longest run of consecutive submissions (ordered by created_at) with at
        // least one kill each. Classic ""gaps and islands"" over a boolean flag: partitioning
        // by the flag and subtracting each row's rank within its own partition from its
        // overall rank yields a constant ""island"" id for every contiguous run of the same
        // flag value — a kill-less submission in between breaks the island, same as LNG-03/04
        // break on a calendar gap.
        ["longest_kill_streak"] = @"
            WITH ordered AS (
                SELECT (enemies_defeated > 0) AS active, ROW_NUMBER() OVER (ORDER BY created_at) AS rn
                FROM level_scores WHERE user_id = @userId
            ),
            grp AS (
                SELECT rn, active, rn - ROW_NUMBER() OVER (PARTITION BY active ORDER BY rn) AS island
                FROM ordered
            )
            SELECT COALESCE(MAX(streak_len), 0) FROM (
                SELECT COUNT(*) AS streak_len FROM grp WHERE active GROUP BY island
            ) streaks;",

        // COM-07 — clears with at least one kill.
        ["clears_with_a_kill_total"] = @"
            SELECT COUNT(*) FROM level_scores WHERE user_id = @userId AND enemies_defeated > 0;",

        // COM-08 — clears with five or more kills in that single run.
        ["high_kill_clears_total"] = @"
            SELECT COUNT(*) FROM level_scores WHERE user_id = @userId AND enemies_defeated >= 5;",

        // COM-09 — % of the archive's galleries where this user has landed at least one kill.
        ["combat_active_gallery_coverage_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT gallery_id) FROM level_scores
                    WHERE user_id = @userId AND enemies_defeated > 0
                )
                / NULLIF((SELECT COUNT(*) FROM galleries), 0)
            ), 0)::bigint;",

        // COM-10 — distinct calendar days with at least one kill.
        ["kill_active_days_total"] = @"
            SELECT COUNT(DISTINCT date_trunc('day', created_at)) FROM level_scores
            WHERE user_id = @userId AND enemies_defeated > 0;",

        // PWR-01 — lifetime buffs collected, summed across every run.
        ["total_buffs_collected"] = @"
            SELECT COALESCE(SUM(buffs_collected), 0) FROM level_scores WHERE user_id = @userId;",

        // PWR-02 — buffs collected on EASY.
        ["easy_buffs_collected_total"] = @"
            SELECT COALESCE(SUM(buffs_collected), 0) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'EASY'::difficulty;",

        // PWR-03 — buffs collected on MEDIUM.
        ["medium_buffs_collected_total"] = @"
            SELECT COALESCE(SUM(buffs_collected), 0) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'MEDIUM'::difficulty;",

        // PWR-04 — buffs collected on HARD.
        ["hard_buffs_collected_total"] = @"
            SELECT COALESCE(SUM(buffs_collected), 0) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'HARD'::difficulty;",

        // PWR-05 — most buffs collected in a single run.
        ["best_single_run_buffs"] = @"
            SELECT COALESCE(MAX(buffs_collected), 0) FROM level_scores WHERE user_id = @userId;",

        // PWR-06 — longest run of consecutive submissions with at least one buff each. Same
        // gaps-and-islands shape as COM-06, over buffs_collected instead.
        ["longest_buff_streak"] = @"
            WITH ordered AS (
                SELECT (buffs_collected > 0) AS active, ROW_NUMBER() OVER (ORDER BY created_at) AS rn
                FROM level_scores WHERE user_id = @userId
            ),
            grp AS (
                SELECT rn, active, rn - ROW_NUMBER() OVER (PARTITION BY active ORDER BY rn) AS island
                FROM ordered
            )
            SELECT COALESCE(MAX(streak_len), 0) FROM (
                SELECT COUNT(*) AS streak_len FROM grp WHERE active GROUP BY island
            ) streaks;",

        // PWR-07 — clears with at least one buff collected.
        ["clears_with_a_buff_total"] = @"
            SELECT COUNT(*) FROM level_scores WHERE user_id = @userId AND buffs_collected > 0;",

        // PWR-08 — clears with five or more buffs collected in that single run.
        ["buff_rich_clears_total"] = @"
            SELECT COUNT(*) FROM level_scores WHERE user_id = @userId AND buffs_collected >= 5;",

        // PWR-09 — % of the archive's galleries where this user has collected at least one buff.
        ["buff_active_gallery_coverage_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT gallery_id) FROM level_scores
                    WHERE user_id = @userId AND buffs_collected > 0
                )
                / NULLIF((SELECT COUNT(*) FROM galleries), 0)
            ), 0)::bigint;",

        // PWR-10 — distinct calendar days with at least one buff collected.
        ["buff_active_days_total"] = @"
            SELECT COUNT(DISTINCT date_trunc('day', created_at)) FROM level_scores
            WHERE user_id = @userId AND buffs_collected > 0;",

        // SRV-01 — clears without taking a hit (hits_taken = 0).
        ["no_hit_clears_total"] = @"
            SELECT COUNT(*) FROM level_scores WHERE user_id = @userId AND hits_taken = 0;",

        // SRV-02 — no-hit clears on EASY.
        ["easy_no_hit_clears_total"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'EASY'::difficulty AND hits_taken = 0;",

        // SRV-03 — no-hit clears on MEDIUM.
        ["medium_no_hit_clears_total"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'MEDIUM'::difficulty AND hits_taken = 0;",

        // SRV-04 — no-hit clears on HARD.
        ["hard_no_hit_clears_total"] = @"
            SELECT COUNT(*) FROM level_scores
            WHERE user_id = @userId AND difficulty = 'HARD'::difficulty AND hits_taken = 0;",

        // SRV-05 — longest run of consecutive no-hit clears. Same gaps-and-islands shape as
        // COM-06/PWR-06, over hits_taken = 0 instead.
        ["longest_no_hit_streak"] = @"
            WITH ordered AS (
                SELECT (hits_taken = 0) AS active, ROW_NUMBER() OVER (ORDER BY created_at) AS rn
                FROM level_scores WHERE user_id = @userId
            ),
            grp AS (
                SELECT rn, active, rn - ROW_NUMBER() OVER (PARTITION BY active ORDER BY rn) AS island
                FROM ordered
            )
            SELECT COALESCE(MAX(streak_len), 0) FROM (
                SELECT COUNT(*) AS streak_len FROM grp WHERE active GROUP BY island
            ) streaks;",

        // SRV-06 — most hits survived in a single run that was still won (every level_scores
        // row is a win — useScoreSubmission only fires on the win transition — so a positive
        // hits_taken here means the player took damage and recovered, not that they lost).
        ["max_hits_survived_single_run"] = @"
            SELECT COALESCE(MAX(hits_taken), 0) FROM level_scores WHERE user_id = @userId;",

        // SRV-07 — % of the archive's galleries this user has cleared without taking a hit.
        ["no_hit_gallery_coverage_pct"] = @"
            SELECT COALESCE(FLOOR(
                100.0 * (
                    SELECT COUNT(DISTINCT gallery_id) FROM level_scores
                    WHERE user_id = @userId AND hits_taken = 0
                )
                / NULLIF((SELECT COUNT(*) FROM galleries), 0)
            ), 0)::bigint;",

        // SRV-08 — distinct calendar days with at least one no-hit clear.
        ["no_hit_active_days_total"] = @"
            SELECT COUNT(DISTINCT date_trunc('day', created_at)) FROM level_scores
            WHERE user_id = @userId AND hits_taken = 0;",

        // SRV-09 — lifetime hits survived, summed across every run.
        ["total_hits_survived"] = @"
            SELECT COALESCE(SUM(hits_taken), 0) FROM level_scores WHERE user_id = @userId;",

        // SRV-10 — galleries cleared without taking a hit on all three difficulties (at least
        // one no-hit clear per difficulty, not necessarily in the same run). Same
        // GROUP BY ... HAVING COUNT(DISTINCT difficulty) = 3 shape as CPL-01's triathlon
        // pattern, restricted to hits_taken = 0 rows.
        ["flawless_triathlon_galleries"] = @"
            SELECT COUNT(*) FROM (
                SELECT gallery_id FROM level_scores
                WHERE user_id = @userId AND hits_taken = 0
                GROUP BY gallery_id
                HAVING COUNT(DISTINCT difficulty) = 3
            ) flawless;"
    };
}

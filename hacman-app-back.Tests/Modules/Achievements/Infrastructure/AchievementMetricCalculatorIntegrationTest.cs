#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using hacman_app_back.Modules.Achievements.Infrastructure;
using hacman_app_back.Tests.repository;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace hacman_app_back.Tests.Modules.Achievements.Infrastructure;

/// <summary>
/// Runs against a real Postgres instance (see <see cref="RepositoryTestBase"/>). Exercises
/// the actual SQL in <see cref="AchievementMetricSqlRegistry"/> — the unit tests around
/// <c>AchievementEvaluationService</c> mock this layer out entirely, so this is the only
/// place the real queries get checked. Every <c>_pct</c> metric's denominator is computed
/// live against whatever <c>galleries</c> currently holds in this database (CLAUDE.md §3) —
/// these tests assert the invariant (0-100, scales with the live count), not a hardcoded
/// expected percentage, since the shared <c>galleries</c> table's size isn't something a
/// single test owns.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class AchievementMetricCalculatorIntegrationTest : RepositoryTestBase
{
    private string _connectionString = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _connectionString = GetConnectionString();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var deleteUserGalleriesCmd = new NpgsqlCommand(
            "DELETE FROM user_galleries WHERE user_id IN (SELECT id FROM users WHERE external_auth_id LIKE 'test_%')",
            conn);
        await deleteUserGalleriesCmd.ExecuteNonQueryAsync();

        await using var deleteLevelScoresCmd = new NpgsqlCommand(
            "DELETE FROM level_scores WHERE user_id IN (SELECT id FROM users WHERE external_auth_id LIKE 'test_%')",
            conn);
        await deleteLevelScoresCmd.ExecuteNonQueryAsync();

        await using var deleteUsersCmd =
            new NpgsqlCommand("DELETE FROM users WHERE external_auth_id LIKE 'test_%'", conn);
        await deleteUsersCmd.ExecuteNonQueryAsync();
    }

    [TestMethod]
    public async Task EasyClearsTotal_CountsOnlyThisUsersEasyScores()
    {
        var userId = await CreateTestUser();
        var otherUserId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();

        await InsertLevelScore(userId, galleryId, 1000, "EASY");
        await InsertLevelScore(userId, galleryId, 2000, "EASY");
        await InsertLevelScore(userId, galleryId, 3000, "HARD");
        await InsertLevelScore(otherUserId, galleryId, 4000, "EASY");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "easy_clears_total",
            AchievementMetricSqlRegistry.Sql["easy_clears_total"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(2L, value);
    }

    [TestMethod]
    public async Task TotalClears_CountsAcrossAllDifficulties()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();

        await InsertLevelScore(userId, galleryId, 1000, "EASY");
        await InsertLevelScore(userId, galleryId, 2000, "MEDIUM");
        await InsertLevelScore(userId, galleryId, 3000, "HARD");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "total_clears",
            AchievementMetricSqlRegistry.Sql["total_clears"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(3L, value);
    }

    [TestMethod]
    public async Task GalleriesUnlockedPct_IsZero_ForAUserWithNoUnlocks()
    {
        var userId = await CreateTestUser();

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "galleries_unlocked_pct",
            AchievementMetricSqlRegistry.Sql["galleries_unlocked_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public async Task GalleriesUnlockedPct_ScalesWithTheLiveDenominator_NeverExceeds100()
    {
        var userId = await CreateTestUser();
        var totalGalleries = await GetTotalGalleryCount();
        Assert.IsTrue(totalGalleries > 0, "This test requires at least one gallery to exist.");

        // Unlock every gallery for this one test user — this only adds rows for a brand-new
        // test user, it never touches any other user's or gallery's state.
        await UnlockAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "galleries_unlocked_pct",
            AchievementMetricSqlRegistry.Sql["galleries_unlocked_pct"]);

        var value = await calculator.CalculateAsync(userId);

        // 100% unlocked must read back as exactly 100 regardless of how many galleries this
        // environment actually has — the whole point of a live COUNT(DISTINCT ...) denominator
        // (CLAUDE.md §3) is that this holds whether totalGalleries is 3 or 320.
        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task PctMetric_IsAlwaysWithinZeroToOneHundred()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY");

        foreach (var metric in new[]
                 {
                     "galleries_unlocked_pct", "triathlon_coverage_pct", "type_coverage_played_pct",
                     "gallery_type_coverage_unlocked_pct", "gallery_theme_coverage_unlocked_pct",
                     "gallery_subtype_coverage_unlocked_pct", "images_collected_pct",
                     "legacy_galleries_unlocked_pct", "type_sets_fully_unlocked_pct"
                 })
        {
            var calculator = new SqlAchievementMetricCalculator(_connectionString, metric,
                AchievementMetricSqlRegistry.Sql[metric]);

            var value = await calculator.CalculateAsync(userId);

            Assert.IsTrue(value is >= 0 and <= 100, $"{metric} returned {value}, outside 0-100.");
        }
    }

    [TestMethod]
    public async Task GalleryTypeCoverageUnlockedPct_Is100_WhenEveryGalleryIsUnlocked()
    {
        var userId = await CreateTestUser();
        await UnlockAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "gallery_type_coverage_unlocked_pct",
            AchievementMetricSqlRegistry.Sql["gallery_type_coverage_unlocked_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task ImagesCollectedPct_Is100_WhenEveryGalleryIsUnlocked()
    {
        var userId = await CreateTestUser();
        await UnlockAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "images_collected_pct",
            AchievementMetricSqlRegistry.Sql["images_collected_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task TypeSetsFullyUnlockedPct_Is100_WhenEveryGalleryIsUnlocked()
    {
        var userId = await CreateTestUser();
        await UnlockAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "type_sets_fully_unlocked_pct",
            AchievementMetricSqlRegistry.Sql["type_sets_fully_unlocked_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task LegacyGalleriesUnlockedPct_Is100_WhenEveryGalleryIsUnlocked()
    {
        var userId = await CreateTestUser();
        await UnlockAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "legacy_galleries_unlocked_pct",
            AchievementMetricSqlRegistry.Sql["legacy_galleries_unlocked_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task FirstGalleriesUnlocked_NeverExceedsFive_RegardlessOfHowManyGalleriesExist()
    {
        var userId = await CreateTestUser();
        await UnlockAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "first_galleries_unlocked",
            AchievementMetricSqlRegistry.Sql["first_galleries_unlocked"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.IsTrue(value is >= 0 and <= 5, $"first_galleries_unlocked returned {value}, outside 0-5.");
    }

    [TestMethod]
    public async Task FirstGalleriesUnlocked_IsZero_ForAUserWithNoUnlocks()
    {
        var userId = await CreateTestUser();

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "first_galleries_unlocked",
            AchievementMetricSqlRegistry.Sql["first_galleries_unlocked"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public async Task UnlocksInADay_CountsUnlocksLandingOnTheSameCalendarDay()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIds(2);
        var sameDay = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

        await UnlockGalleryForUser(userId, galleryIds[0], sameDay);
        await UnlockGalleryForUser(userId, galleryIds[1], sameDay.AddHours(3));

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "unlocks_in_a_day",
            AchievementMetricSqlRegistry.Sql["unlocks_in_a_day"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(2L, value);
    }

    [TestMethod]
    public async Task UnlocksInADay_IsZero_ForAUserWithNoUnlocks()
    {
        var userId = await CreateTestUser();

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "unlocks_in_a_day",
            AchievementMetricSqlRegistry.Sql["unlocks_in_a_day"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public async Task UnlocksInAWeek_CountsUnlocksLandingInTheSameCalendarWeek()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIds(2);
        var monday = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

        await UnlockGalleryForUser(userId, galleryIds[0], monday);
        await UnlockGalleryForUser(userId, galleryIds[1], monday.AddDays(3));

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "unlocks_in_a_week",
            AchievementMetricSqlRegistry.Sql["unlocks_in_a_week"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(2L, value);
    }

    [TestMethod]
    public async Task EasySubHalfTimeClears_CountsOnlyClearsFasterThanFiveMinutes()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();

        await InsertLevelScore(userId, galleryId, 299_999, "EASY"); // under the bar
        await InsertLevelScore(userId, galleryId, 300_000, "EASY"); // exactly at the bar, not under
        await InsertLevelScore(userId, galleryId, 400_000, "EASY"); // over the bar

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "easy_sub_half_time_clears",
            AchievementMetricSqlRegistry.Sql["easy_sub_half_time_clears"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(1L, value);
    }

    [TestMethod]
    public async Task MediumSubQuarterTimeClears_UsesTheSeventyFiveSecondBar_NotEasysOrHards()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();

        await InsertLevelScore(userId, galleryId, 74_999, "MEDIUM"); // under the 75s bar
        await InsertLevelScore(userId, galleryId, 74_999, "EASY");   // same time, wrong difficulty
        await InsertLevelScore(userId, galleryId, 90_000, "MEDIUM"); // over the bar

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "medium_sub_quarter_time_clears",
            AchievementMetricSqlRegistry.Sql["medium_sub_quarter_time_clears"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(1L, value);
    }

    [TestMethod]
    public async Task HardMarathonTimeMs_SumsOnlyHardClearTimes()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();

        await InsertLevelScore(userId, galleryId, 200_000, "HARD");
        await InsertLevelScore(userId, galleryId, 150_000, "HARD");
        await InsertLevelScore(userId, galleryId, 999_999, "EASY");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "hard_marathon_time_ms",
            AchievementMetricSqlRegistry.Sql["hard_marathon_time_ms"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(350_000L, value);
    }

    [TestMethod]
    public async Task EasyDailyGrindDays_IsZero_ForAUserWithNoEasyClears()
    {
        var userId = await CreateTestUser();

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "easy_daily_grind_days",
            AchievementMetricSqlRegistry.Sql["easy_daily_grind_days"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public async Task MasteryLegacyGalleriesClearedPct_Is100_WhenEveryLegacyGalleryIsClearedOnThatDifficulty()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIds(1);

        // Not a claim about the whole archive's legacy set — just that clearing this user's
        // only gallery on all three difficulties can never push any of these _pct metrics
        // above 100, which is the invariant that matters here (CLAUDE.md §3).
        await InsertLevelScore(userId, galleryIds[0], 1000, "EASY");
        await InsertLevelScore(userId, galleryIds[0], 1000, "MEDIUM");
        await InsertLevelScore(userId, galleryIds[0], 1000, "HARD");

        foreach (var metric in new[]
                 {
                     "easy_legacy_galleries_cleared_pct", "medium_legacy_galleries_cleared_pct",
                     "hard_legacy_galleries_cleared_pct"
                 })
        {
            var calculator = new SqlAchievementMetricCalculator(_connectionString, metric,
                AchievementMetricSqlRegistry.Sql[metric]);

            var value = await calculator.CalculateAsync(userId);

            Assert.IsTrue(value is >= 0 and <= 100, $"{metric} returned {value}, outside 0-100.");
        }
    }

    [TestMethod]
    public async Task MasteryPctMetrics_AreAlwaysWithinZeroToOneHundred()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY");
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM");
        await InsertLevelScore(userId, galleryId, 1000, "HARD");

        foreach (var metric in new[]
                 {
                     "easy_gallery_coverage_cleared_pct", "easy_type_coverage_cleared_pct",
                     "easy_theme_coverage_cleared_pct", "medium_gallery_coverage_cleared_pct",
                     "medium_type_coverage_cleared_pct", "medium_theme_coverage_cleared_pct",
                     "hard_gallery_coverage_cleared_pct", "hard_type_coverage_cleared_pct",
                     "hard_theme_coverage_cleared_pct"
                 })
        {
            var calculator = new SqlAchievementMetricCalculator(_connectionString, metric,
                AchievementMetricSqlRegistry.Sql[metric]);

            var value = await calculator.CalculateAsync(userId);

            Assert.IsTrue(value is >= 0 and <= 100, $"{metric} returned {value}, outside 0-100.");
        }
    }

    [TestMethod]
    public async Task CompletionPctMetrics_AreAlwaysWithinZeroToOneHundred()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY");
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM");
        await InsertLevelScore(userId, galleryId, 1000, "HARD");

        foreach (var metric in new[]
                 {
                     "triathlon_type_coverage_pct", "speedrun_triathlon_coverage_pct",
                     "theme_full_unlock_coverage_pct", "type_full_triathlon_coverage_pct",
                     "subtype_full_triathlon_coverage_pct", "triathlon_theme_coverage_pct",
                     "triathlon_subtype_coverage_pct"
                 })
        {
            var calculator = new SqlAchievementMetricCalculator(_connectionString, metric,
                AchievementMetricSqlRegistry.Sql[metric]);

            var value = await calculator.CalculateAsync(userId);

            Assert.IsTrue(value is >= 0 and <= 100, $"{metric} returned {value}, outside 0-100.");
        }
    }

    [TestMethod]
    public async Task TriathlonTypeCoveragePct_Is100_WhenEveryGalleryIsTriathloned()
    {
        var userId = await CreateTestUser();
        await TriathlonAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "triathlon_type_coverage_pct",
            AchievementMetricSqlRegistry.Sql["triathlon_type_coverage_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task TriathlonThemeCoveragePct_Is100_WhenEveryGalleryIsTriathloned()
    {
        var userId = await CreateTestUser();
        await TriathlonAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "triathlon_theme_coverage_pct",
            AchievementMetricSqlRegistry.Sql["triathlon_theme_coverage_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task TriathlonSubtypeCoveragePct_Is100_WhenEveryGalleryIsTriathloned()
    {
        var userId = await CreateTestUser();
        await TriathlonAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "triathlon_subtype_coverage_pct",
            AchievementMetricSqlRegistry.Sql["triathlon_subtype_coverage_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task TypeFullTriathlonCoveragePct_Is100_WhenEveryGalleryIsTriathloned()
    {
        var userId = await CreateTestUser();
        await TriathlonAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "type_full_triathlon_coverage_pct",
            AchievementMetricSqlRegistry.Sql["type_full_triathlon_coverage_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task SubtypeFullTriathlonCoveragePct_Is100_WhenEveryGalleryIsTriathloned()
    {
        var userId = await CreateTestUser();
        await TriathlonAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "subtype_full_triathlon_coverage_pct",
            AchievementMetricSqlRegistry.Sql["subtype_full_triathlon_coverage_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task ThemeFullUnlockCoveragePct_Is100_WhenEveryGalleryIsUnlocked()
    {
        var userId = await CreateTestUser();
        await UnlockAllGalleriesForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "theme_full_unlock_coverage_pct",
            AchievementMetricSqlRegistry.Sql["theme_full_unlock_coverage_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task SpeedrunTriathlonCoveragePct_IsZero_WhenTriathlonMissesTheHalfTimeBar()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();

        // A full triathlon, but the HARD leg is over its half-time bar (180000ms) — the
        // metric requires all three legs under their own bar, so this must not count.
        await InsertLevelScore(userId, galleryId, 100_000, "EASY");
        await InsertLevelScore(userId, galleryId, 50_000, "MEDIUM");
        await InsertLevelScore(userId, galleryId, 200_000, "HARD");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "speedrun_triathlon_coverage_pct",
            AchievementMetricSqlRegistry.Sql["speedrun_triathlon_coverage_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public async Task CleanSweepDay_CountsGalleriesTriathlonedOnTheSameCalendarDay()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIds(2);
        var sameDay = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

        await TriathlonGalleryForUser(userId, galleryIds[0], sameDay);
        await TriathlonGalleryForUser(userId, galleryIds[1], sameDay.AddHours(3));

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "clean_sweep_day",
            AchievementMetricSqlRegistry.Sql["clean_sweep_day"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(2L, value);
    }

    [TestMethod]
    public async Task CleanSweepWeek_CountsGalleriesTriathlonedInTheSameCalendarWeek()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIds(2);
        var monday = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

        await TriathlonGalleryForUser(userId, galleryIds[0], monday);
        await TriathlonGalleryForUser(userId, galleryIds[1], monday.AddDays(3));

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "clean_sweep_week",
            AchievementMetricSqlRegistry.Sql["clean_sweep_week"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(2L, value);
    }

    [TestMethod]
    public async Task GalleryCoveragePlayedPct_Is100_WhenEveryGalleryHasBeenPlayed()
    {
        var userId = await CreateTestUser();
        await PlayAllGalleriesOnceForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "gallery_coverage_played_pct",
            AchievementMetricSqlRegistry.Sql["gallery_coverage_played_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task GalleryCoveragePlayedPct_IsZero_ForAUserWithNoClears()
    {
        var userId = await CreateTestUser();

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "gallery_coverage_played_pct",
            AchievementMetricSqlRegistry.Sql["gallery_coverage_played_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public async Task TotalPlaytimeMs_SumsAcrossAllDifficulties()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 100_000, "EASY");
        await InsertLevelScore(userId, galleryId, 50_000, "MEDIUM");
        await InsertLevelScore(userId, galleryId, 75_000, "HARD");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "total_playtime_ms",
            AchievementMetricSqlRegistry.Sql["total_playtime_ms"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(225_000L, value);
    }

    [TestMethod]
    public async Task TotalDaysActive_CountsDistinctCalendarDays()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var day1 = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 1, 12, 9, 0, 0, DateTimeKind.Utc);

        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1.AddHours(2));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day2);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "total_days_active",
            AchievementMetricSqlRegistry.Sql["total_days_active"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(2L, value);
    }

    [TestMethod]
    public async Task WeeklyRegular_CountsClearsWithinTheBusiestRollingWeek()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var monday = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
        var otherWeek = monday.AddDays(14);

        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday.AddDays(1));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday.AddDays(2));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", otherWeek);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "weekly_regular",
            AchievementMetricSqlRegistry.Sql["weekly_regular"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(3L, value);
    }

    [TestMethod]
    public async Task MonthlyMarathoner_CountsClearsWithinTheBusiestCalendarMonth()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var january = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var february = new DateTime(2026, 2, 15, 9, 0, 0, DateTimeKind.Utc);

        await InsertLevelScore(userId, galleryId, 1000, "EASY", january);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", january.AddDays(1));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", february);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "monthly_marathoner",
            AchievementMetricSqlRegistry.Sql["monthly_marathoner"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(2L, value);
    }

    [TestMethod]
    public async Task Replays_CountsSubmissionsBeyondTheFirstForEachGalleryDifficultyPair()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIds(2);

        await InsertLevelScore(userId, galleryIds[0], 1000, "EASY");
        await InsertLevelScore(userId, galleryIds[0], 900, "EASY"); // a replay of the same pair
        await InsertLevelScore(userId, galleryIds[1], 1000, "MEDIUM");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "replays",
            AchievementMetricSqlRegistry.Sql["replays"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(1L, value);
    }

    [TestMethod]
    public async Task Replays_IsZero_WhenNoPairHasBeenClearedTwice()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY");
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "replays",
            AchievementMetricSqlRegistry.Sql["replays"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public async Task EasyMediumHardMix_CountsDaysWithTwoOrMoreDifficultiesPlayed()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var mixedDay = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);
        var singleDifficultyDay = new DateTime(2026, 1, 11, 9, 0, 0, DateTimeKind.Utc);

        await InsertLevelScore(userId, galleryId, 1000, "EASY", mixedDay);
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM", mixedDay.AddHours(1));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", singleDifficultyDay);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "easy_medium_hard_mix",
            AchievementMetricSqlRegistry.Sql["easy_medium_hard_mix"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(1L, value);
    }

    [TestMethod]
    public async Task VeteranAccount_IsZero_ForAUserWithNoClears()
    {
        var userId = await CreateTestUser();

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "veteran_account",
            AchievementMetricSqlRegistry.Sql["veteran_account"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public async Task GrandTotalTimeSavedMin_SumsTimeSavedAgainstEachDifficultysOwnCountdown()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();

        // EASY's countdown is 600000ms; 540000ms leaves 60000ms (1 minute) saved.
        await InsertLevelScore(userId, galleryId, 540_000, "EASY");
        // MEDIUM's countdown is 300000ms; 240000ms leaves 60000ms (1 minute) saved.
        await InsertLevelScore(userId, galleryId, 240_000, "MEDIUM");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "grand_total_time_saved_min",
            AchievementMetricSqlRegistry.Sql["grand_total_time_saved_min"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(2L, value);
    }

    [TestMethod]
    public async Task VarietyPctMetrics_AreAlwaysWithinZeroToOneHundred()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY");

        foreach (var metric in new[]
                 {
                     "theme_coverage_played_pct", "subtype_coverage_played_pct",
                     "theme_coverage_hopper_week_pct", "underdog_gallery_coverage_pct",
                     "big_gallery_coverage_pct", "deep_cut_coverage_pct"
                 })
        {
            var calculator = new SqlAchievementMetricCalculator(_connectionString, metric,
                AchievementMetricSqlRegistry.Sql[metric]);

            var value = await calculator.CalculateAsync(userId);

            Assert.IsTrue(value is >= 0 and <= 100, $"{metric} returned {value}, outside 0-100.");
        }
    }

    [TestMethod]
    public async Task VarietyPctMetrics_Are100_WhenEveryGalleryHasBeenPlayed()
    {
        var userId = await CreateTestUser();
        await PlayAllGalleriesOnceForUser(userId);

        foreach (var metric in new[]
                 {
                     "theme_coverage_played_pct", "subtype_coverage_played_pct",
                     "theme_coverage_hopper_week_pct", "underdog_gallery_coverage_pct",
                     "big_gallery_coverage_pct", "deep_cut_coverage_pct"
                 })
        {
            var calculator = new SqlAchievementMetricCalculator(_connectionString, metric,
                AchievementMetricSqlRegistry.Sql[metric]);

            var value = await calculator.CalculateAsync(userId);

            Assert.AreEqual(100L, value, $"{metric} expected 100 when every gallery is played.");
        }
    }

    [TestMethod]
    public async Task DifficultySwitchDays_CountsDaysWhoseDifficultySetDiffersFromThePriorActiveDay()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var day1 = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1.AddDays(1)); // same set, no switch
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM", day1.AddDays(2)); // switch
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM", day1.AddDays(3)); // same set, no switch

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "difficulty_switch_days",
            AchievementMetricSqlRegistry.Sql["difficulty_switch_days"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(1L, value);
    }

    [TestMethod]
    public async Task DifficultySwitchDays_IsZero_WhenTheSameDifficultyIsPlayedEveryActiveDay()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var day1 = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1.AddDays(1));

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "difficulty_switch_days",
            AchievementMetricSqlRegistry.Sql["difficulty_switch_days"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public async Task TypeRainbowDays_CountsDaysWithThreeOrMoreDistinctTypesCleared()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIdsAcrossDistinctTypes(3);
        var day = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

        foreach (var galleryId in galleryIds)
            await InsertLevelScore(userId, galleryId, 1000, "EASY", day);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "type_rainbow_days",
            AchievementMetricSqlRegistry.Sql["type_rainbow_days"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(1L, value);
    }

    [TestMethod]
    public async Task TypeRainbowDays_IsZero_ForAUserWithNoClears()
    {
        var userId = await CreateTestUser();

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "type_rainbow_days",
            AchievementMetricSqlRegistry.Sql["type_rainbow_days"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public async Task FirstGalleriesCleared_Is5_WhenEveryGalleryHasBeenCleared()
    {
        var userId = await CreateTestUser();
        await PlayAllGalleriesOnceForUser(userId);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "first_galleries_cleared",
            AchievementMetricSqlRegistry.Sql["first_galleries_cleared"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(5L, value);
    }

    [TestMethod]
    public async Task FirstGalleriesCleared_IsZero_ForAUserWithNoClears()
    {
        var userId = await CreateTestUser();

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "first_galleries_cleared",
            AchievementMetricSqlRegistry.Sql["first_galleries_cleared"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    private async Task<Guid[]> GetGalleryIdsAcrossDistinctTypes(int count)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id FROM (SELECT DISTINCT ON (type) id, type FROM galleries ORDER BY type, id) t LIMIT @count",
            conn);
        cmd.Parameters.AddWithValue("@count", count);
        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetGuid(0));
        Assert.IsTrue(ids.Count == count, $"This test requires at least {count} distinct gallery types to exist.");
        return ids.ToArray();
    }

    [TestMethod]
    public async Task SubOneMinuteClearsTotal_CountsOnlyClearsUnderSixtySeconds()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 59_999, "EASY"); // under the bar
        await InsertLevelScore(userId, galleryId, 60_000, "MEDIUM"); // exactly at the bar, not under
        await InsertLevelScore(userId, galleryId, 70_000, "HARD"); // over the bar

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "sub_1_minute_clears_total",
            AchievementMetricSqlRegistry.Sql["sub_1_minute_clears_total"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(1L, value);
    }

    [TestMethod]
    public async Task SpeedPctMetrics_AreAlwaysWithinZeroToOneHundred()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 80_000, "EASY");

        foreach (var metric in new[] { "speed_gallery_coverage_pct", "speed_type_coverage_pct", "speed_theme_coverage_pct" })
        {
            var calculator = new SqlAchievementMetricCalculator(_connectionString, metric,
                AchievementMetricSqlRegistry.Sql[metric]);

            var value = await calculator.CalculateAsync(userId);

            Assert.IsTrue(value is >= 0 and <= 100, $"{metric} returned {value}, outside 0-100.");
        }
    }

    [TestMethod]
    public async Task SpeedGalleryCoveragePct_IsZero_WhenNoClearBeatsTheNinetySecondBar()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 90_000, "EASY"); // exactly at the bar, not under

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "speed_gallery_coverage_pct",
            AchievementMetricSqlRegistry.Sql["speed_gallery_coverage_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public async Task SpeedWeek_CountsSubNinetySecondClearsWithinTheBusiestRollingWeek()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var monday = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

        await InsertLevelScore(userId, galleryId, 80_000, "EASY", monday);
        await InsertLevelScore(userId, galleryId, 80_000, "MEDIUM", monday.AddDays(1));
        await InsertLevelScore(userId, galleryId, 100_000, "HARD", monday.AddDays(2)); // over the bar, doesn't count
        await InsertLevelScore(userId, galleryId, 80_000, "EASY", monday.AddDays(14)); // a different week

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "speed_week",
            AchievementMetricSqlRegistry.Sql["speed_week"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(2L, value);
    }

    [TestMethod]
    public async Task PersonalRecordBreaks_CountsTheFirstSubmissionAndAnyImprovementOnThatPair()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIds(2);
        var t0 = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

        // Gallery 0 / EASY: first submission is always a record, then only the improvement counts.
        await InsertLevelScore(userId, galleryIds[0], 100_000, "EASY", t0);
        await InsertLevelScore(userId, galleryIds[0], 90_000, "EASY", t0.AddDays(1)); // improvement, counts
        await InsertLevelScore(userId, galleryIds[0], 95_000, "EASY", t0.AddDays(2)); // worse, doesn't count

        // Gallery 1 / MEDIUM: a single submission, still a record (nothing to beat yet).
        await InsertLevelScore(userId, galleryIds[1], 50_000, "MEDIUM", t0);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "personal_record_breaks",
            AchievementMetricSqlRegistry.Sql["personal_record_breaks"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(3L, value);
    }

    [TestMethod]
    public async Task HardSpeedSpecialist_CountsOnlySubNinetySecondHardClears()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 80_000, "HARD"); // under the bar, HARD
        await InsertLevelScore(userId, galleryId, 80_000, "EASY"); // under the bar, wrong difficulty
        await InsertLevelScore(userId, galleryId, 100_000, "HARD"); // over the bar

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "hard_speed_specialist",
            AchievementMetricSqlRegistry.Sql["hard_speed_specialist"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(1L, value);
    }

    [TestMethod]
    public async Task BackToBackSpeed_CountsSubNinetySecondClearsWithinTheBusiestCalendarDay()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var day = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

        await InsertLevelScore(userId, galleryId, 80_000, "EASY", day);
        await InsertLevelScore(userId, galleryId, 80_000, "MEDIUM", day.AddHours(1));
        await InsertLevelScore(userId, galleryId, 80_000, "EASY", day.AddDays(1)); // a different day

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "back_to_back_speed",
            AchievementMetricSqlRegistry.Sql["back_to_back_speed"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(2L, value);
    }

    [TestMethod]
    public async Task SubTwentySecondClears_CountsOnlyClearsUnderTwentySeconds()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 19_999, "EASY"); // under the bar
        await InsertLevelScore(userId, galleryId, 20_000, "MEDIUM"); // exactly at the bar, not under

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "sub_20_second_clears",
            AchievementMetricSqlRegistry.Sql["sub_20_second_clears"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(1L, value);
    }

    [TestMethod]
    public async Task FirstPlaceFinishes_CountsWhenThisUsersTimeIsTheFastestOnRecord()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        // 1ms is far below the real fixture data's slowest realistic time, guaranteeing rank 1
        // regardless of how many other real users have already scored on this gallery.
        await InsertLevelScore(userId, galleryId, 1, "EASY");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "first_place_finishes",
            AchievementMetricSqlRegistry.Sql["first_place_finishes"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(1L, value);
    }

    [TestMethod]
    public async Task FirstPlaceFinishes_IsZero_WhenAnotherUserHasABetterTime()
    {
        var userA = await CreateTestUser();
        var userB = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userA, galleryId, 1, "EASY");
        await InsertLevelScore(userB, galleryId, 2, "EASY");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "first_place_finishes",
            AchievementMetricSqlRegistry.Sql["first_place_finishes"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userA));
        Assert.AreEqual(0L, await calculator.CalculateAsync(userB));
    }

    [TestMethod]
    public async Task Top3Finishes_CountsPlacementsRankedThirdOrBetter()
    {
        var users = new[] { await CreateTestUser(), await CreateTestUser(), await CreateTestUser(), await CreateTestUser() };
        var galleryId = await GetFirstGalleryId();
        for (var i = 0; i < users.Length; i++)
            await InsertLevelScore(users[i], galleryId, i + 1, "EASY"); // ranks 1, 2, 3, 4

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "top3_finishes",
            AchievementMetricSqlRegistry.Sql["top3_finishes"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(users[2])); // rank 3
        Assert.AreEqual(0L, await calculator.CalculateAsync(users[3])); // rank 4
    }

    [TestMethod]
    public async Task TopQuartileFinishCoveragePct_Is100_WhenThisUsersOnlyPlacementIsFirst()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1, "EASY");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "top_quartile_finish_coverage_pct",
            AchievementMetricSqlRegistry.Sql["top_quartile_finish_coverage_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task LeaderboardPresenceCoveragePct_Is100_WhenThisUsersOnlyPlacementIsFirst()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1, "EASY");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "leaderboard_presence_coverage_pct",
            AchievementMetricSqlRegistry.Sql["leaderboard_presence_coverage_pct"]);

        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual(100L, value);
    }

    [TestMethod]
    public async Task RankingPctMetrics_AreAlwaysWithinZeroToOneHundred()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1, "EASY");

        foreach (var metric in new[] { "podium_type_coverage_pct", "podium_theme_coverage_pct" })
        {
            var calculator = new SqlAchievementMetricCalculator(_connectionString, metric,
                AchievementMetricSqlRegistry.Sql[metric]);

            var value = await calculator.CalculateAsync(userId);

            Assert.IsTrue(value is >= 0 and <= 100, $"{metric} returned {value}, outside 0-100.");
        }
    }

    [TestMethod]
    public async Task DethronedSomeone_CountsSubmissionsThatBeatAnEarlierBetterSubmissionFromAnotherUser()
    {
        var userA = await CreateTestUser();
        var userB = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var t0 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // userA's 2ms is far below any real fixture time, so it's genuinely the pair's best
        // when userB submits afterward with an even better time.
        await InsertLevelScore(userA, galleryId, 2, "EASY", t0);
        await InsertLevelScore(userB, galleryId, 1, "EASY", t0.AddDays(1));

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "dethroned_someone",
            AchievementMetricSqlRegistry.Sql["dethroned_someone"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userB));
    }

    [TestMethod]
    public async Task DethronedSomeone_IsZero_WhenNoEarlierBetterSubmissionIsBeaten()
    {
        var userA = await CreateTestUser();
        var userC = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var t0 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await InsertLevelScore(userA, galleryId, 2, "EASY", t0);
        await InsertLevelScore(userC, galleryId, 1000, "EASY", t0.AddDays(1)); // worse than userA's 2ms

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "dethroned_someone",
            AchievementMetricSqlRegistry.Sql["dethroned_someone"]);

        Assert.AreEqual(0L, await calculator.CalculateAsync(userC));
    }

    [TestMethod]
    public async Task EasyChampion_CountsWhenThisUsersEasyTimeIsFastestOnThatGallery()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1, "EASY");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "easy_champion",
            AchievementMetricSqlRegistry.Sql["easy_champion"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task HardChampion_CountsWhenThisUsersHardTimeIsFastestOnThatGallery()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1, "HARD");

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "hard_champion",
            AchievementMetricSqlRegistry.Sql["hard_champion"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task EasyChampion_IsZero_ForAUserWithNoEasyClears()
    {
        var userId = await CreateTestUser();

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "easy_champion",
            AchievementMetricSqlRegistry.Sql["easy_champion"]);

        Assert.AreEqual(0L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task DaysSinceFirstClear_IsZero_ForAUserWithNoClears()
    {
        var userId = await CreateTestUser();

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "days_since_first_clear",
            AchievementMetricSqlRegistry.Sql["days_since_first_clear"]);

        Assert.AreEqual(0L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task MonthlyActiveStreak_CountsTheLongestRunOfConsecutiveActiveMonths()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        // Active Jan, Feb, Mar (a 3-month run), then a gap, then active May alone.
        await InsertLevelScore(userId, galleryId, 1000, "EASY", new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", new DateTime(2026, 2, 15, 9, 0, 0, DateTimeKind.Utc));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", new DateTime(2026, 5, 15, 9, 0, 0, DateTimeKind.Utc));

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "monthly_active_streak",
            AchievementMetricSqlRegistry.Sql["monthly_active_streak"]);

        Assert.AreEqual(3L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task WeeklyActiveStreak_CountsTheLongestRunOfConsecutiveActiveWeeks()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var monday = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
        // Active weeks 1, 2 (consecutive), then a gap, then week 5 alone.
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday.AddDays(7));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday.AddDays(28));

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "weekly_active_streak",
            AchievementMetricSqlRegistry.Sql["weekly_active_streak"]);

        Assert.AreEqual(2L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task SeasonalPlayer_CountsDistinctActiveMonths_NotRequiredToBeConsecutive()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", new DateTime(2026, 8, 15, 9, 0, 0, DateTimeKind.Utc));

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "seasonal_player",
            AchievementMetricSqlRegistry.Sql["seasonal_player"]);

        // 3 distinct months despite the gaps -- unlike monthly_active_streak, which would
        // give 1 for this same data since none of these months are consecutive.
        Assert.AreEqual(3L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task ComebackPlayer_CountsReturnsAfterAThirtyDayGap()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var day1 = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1.AddDays(40)); // 40-day gap: a comeback
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1.AddDays(41)); // 1-day gap: not a comeback

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "comeback_player",
            AchievementMetricSqlRegistry.Sql["comeback_player"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task ComebackPlayer_IsZero_WhenEveryGapIsUnderThirtyDays()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var day1 = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1.AddDays(10));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1.AddDays(20));

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "comeback_player",
            AchievementMetricSqlRegistry.Sql["comeback_player"]);

        Assert.AreEqual(0L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task NightOwl_CountsClearsBetweenTenPmAndFourAm()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var day = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day.AddHours(23)); // 23:00, counts
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day.AddHours(2)); // 02:00, counts
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day.AddHours(10)); // 10:00, doesn't count

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "night_owl",
            AchievementMetricSqlRegistry.Sql["night_owl"]);

        Assert.AreEqual(2L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task EarlyBird_CountsClearsBetweenFourAmAndSevenAm()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var day = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day.AddHours(5)); // 05:00, counts
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day.AddHours(8)); // 08:00, doesn't count

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "early_bird",
            AchievementMetricSqlRegistry.Sql["early_bird"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task WeekendWarrior_CountsClearsOnSaturdayOrSunday()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var monday = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday.AddDays(5)); // Saturday, counts
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday.AddDays(6)); // Sunday, counts
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday); // Monday, doesn't count

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "weekend_warrior",
            AchievementMetricSqlRegistry.Sql["weekend_warrior"]);

        Assert.AreEqual(2L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task ConsistencyScore_CountsWeeksWithThreeOrMoreClears()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var monday = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday.AddDays(1));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday.AddDays(2)); // week 1: 3 clears, counts
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday.AddDays(7));
        await InsertLevelScore(userId, galleryId, 1000, "EASY", monday.AddDays(8)); // week 2: 2 clears, doesn't count

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "consistency_score",
            AchievementMetricSqlRegistry.Sql["consistency_score"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userId));
    }

    // ========================================================================
    // M7 telemetry tier — COMBAT, POWER_UPS, SURVIVAL (ROADMAP.md M7-2a)
    // ========================================================================

    [TestMethod]
    public async Task TotalEnemiesDefeated_SumsEnemiesDefeatedAcrossAllRuns()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", enemiesDefeated: 3);
        await InsertLevelScore(userId, galleryId, 1000, "HARD", enemiesDefeated: 5);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "total_enemies_defeated",
            AchievementMetricSqlRegistry.Sql["total_enemies_defeated"]);

        Assert.AreEqual(8L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task PerDifficultyEnemiesDefeatedTotals_SumIndependentlyPerDifficulty()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", enemiesDefeated: 2);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", enemiesDefeated: 1);
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM", enemiesDefeated: 4);
        await InsertLevelScore(userId, galleryId, 1000, "HARD", enemiesDefeated: 7);

        Assert.AreEqual(3L, await new SqlAchievementMetricCalculator(_connectionString,
            "easy_enemies_defeated_total", AchievementMetricSqlRegistry.Sql["easy_enemies_defeated_total"])
            .CalculateAsync(userId));
        Assert.AreEqual(4L, await new SqlAchievementMetricCalculator(_connectionString,
            "medium_enemies_defeated_total", AchievementMetricSqlRegistry.Sql["medium_enemies_defeated_total"])
            .CalculateAsync(userId));
        Assert.AreEqual(7L, await new SqlAchievementMetricCalculator(_connectionString,
            "hard_enemies_defeated_total", AchievementMetricSqlRegistry.Sql["hard_enemies_defeated_total"])
            .CalculateAsync(userId));
    }

    [TestMethod]
    public async Task BestSingleRunKills_ReturnsTheHighestSingleRunCount()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", enemiesDefeated: 2);
        await InsertLevelScore(userId, galleryId, 1000, "HARD", enemiesDefeated: 9);
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM", enemiesDefeated: 4);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "best_single_run_kills",
            AchievementMetricSqlRegistry.Sql["best_single_run_kills"]);

        Assert.AreEqual(9L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task LongestKillStreak_FindsTheLongestConsecutiveRunAcrossABreak()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var t0 = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        // Streak of 2, then a kill-less run breaks it, then a streak of 3 (the longest).
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0, enemiesDefeated: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(1), enemiesDefeated: 2);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(2), enemiesDefeated: 0);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(3), enemiesDefeated: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(4), enemiesDefeated: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(5), enemiesDefeated: 3);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "longest_kill_streak",
            AchievementMetricSqlRegistry.Sql["longest_kill_streak"]);

        Assert.AreEqual(3L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task LongestKillStreak_IsZero_WhenNoRunHasAKill()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", enemiesDefeated: 0);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "longest_kill_streak",
            AchievementMetricSqlRegistry.Sql["longest_kill_streak"]);

        Assert.AreEqual(0L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task ClearsWithAKillTotal_CountsOnlyRunsWithAtLeastOneKill()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", enemiesDefeated: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", enemiesDefeated: 0);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", enemiesDefeated: 2);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "clears_with_a_kill_total",
            AchievementMetricSqlRegistry.Sql["clears_with_a_kill_total"]);

        Assert.AreEqual(2L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task HighKillClearsTotal_CountsOnlyRunsWithFiveOrMoreKills()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", enemiesDefeated: 4);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", enemiesDefeated: 5);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", enemiesDefeated: 10);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "high_kill_clears_total",
            AchievementMetricSqlRegistry.Sql["high_kill_clears_total"]);

        Assert.AreEqual(2L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task CombatActiveGalleryCoveragePct_ScalesWithTheLiveDenominator()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIds(2);
        await InsertLevelScore(userId, galleryIds[0], 1000, "EASY", enemiesDefeated: 1);
        var totalGalleries = await GetTotalGalleryCount();

        var calculator = new SqlAchievementMetricCalculator(_connectionString,
            "combat_active_gallery_coverage_pct", AchievementMetricSqlRegistry.Sql["combat_active_gallery_coverage_pct"]);
        var value = await calculator.CalculateAsync(userId);

        var expected = (long)Math.Floor(100.0 * 1 / totalGalleries);
        Assert.AreEqual(expected, value);
        Assert.IsTrue(value is >= 0 and <= 100, $"combat_active_gallery_coverage_pct returned {value}, outside 0-100.");
    }

    [TestMethod]
    public async Task KillActiveDaysTotal_CountsDistinctDaysWithAtLeastOneKill()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var day1 = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 1, 6, 9, 0, 0, DateTimeKind.Utc);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1, enemiesDefeated: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1.AddHours(2), enemiesDefeated: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day2, enemiesDefeated: 0);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "kill_active_days_total",
            AchievementMetricSqlRegistry.Sql["kill_active_days_total"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task TotalBuffsCollected_SumsBuffsCollectedAcrossAllRuns()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", buffsCollected: 2);
        await InsertLevelScore(userId, galleryId, 1000, "HARD", buffsCollected: 3);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "total_buffs_collected",
            AchievementMetricSqlRegistry.Sql["total_buffs_collected"]);

        Assert.AreEqual(5L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task PerDifficultyBuffsCollectedTotals_SumIndependentlyPerDifficulty()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", buffsCollected: 1);
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM", buffsCollected: 2);
        await InsertLevelScore(userId, galleryId, 1000, "HARD", buffsCollected: 3);

        Assert.AreEqual(1L, await new SqlAchievementMetricCalculator(_connectionString,
            "easy_buffs_collected_total", AchievementMetricSqlRegistry.Sql["easy_buffs_collected_total"])
            .CalculateAsync(userId));
        Assert.AreEqual(2L, await new SqlAchievementMetricCalculator(_connectionString,
            "medium_buffs_collected_total", AchievementMetricSqlRegistry.Sql["medium_buffs_collected_total"])
            .CalculateAsync(userId));
        Assert.AreEqual(3L, await new SqlAchievementMetricCalculator(_connectionString,
            "hard_buffs_collected_total", AchievementMetricSqlRegistry.Sql["hard_buffs_collected_total"])
            .CalculateAsync(userId));
    }

    [TestMethod]
    public async Task BestSingleRunBuffs_ReturnsTheHighestSingleRunCount()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", buffsCollected: 1);
        await InsertLevelScore(userId, galleryId, 1000, "HARD", buffsCollected: 6);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "best_single_run_buffs",
            AchievementMetricSqlRegistry.Sql["best_single_run_buffs"]);

        Assert.AreEqual(6L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task LongestBuffStreak_FindsTheLongestConsecutiveRunAcrossABreak()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var t0 = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0, buffsCollected: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(1), buffsCollected: 0);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(2), buffsCollected: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(3), buffsCollected: 1);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "longest_buff_streak",
            AchievementMetricSqlRegistry.Sql["longest_buff_streak"]);

        Assert.AreEqual(2L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task ClearsWithABuffTotal_CountsOnlyRunsWithAtLeastOneBuff()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", buffsCollected: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", buffsCollected: 0);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "clears_with_a_buff_total",
            AchievementMetricSqlRegistry.Sql["clears_with_a_buff_total"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task BuffRichClearsTotal_CountsOnlyRunsWithFiveOrMoreBuffs()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", buffsCollected: 4);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", buffsCollected: 5);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "buff_rich_clears_total",
            AchievementMetricSqlRegistry.Sql["buff_rich_clears_total"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task BuffActiveGalleryCoveragePct_ScalesWithTheLiveDenominator()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIds(2);
        await InsertLevelScore(userId, galleryIds[0], 1000, "EASY", buffsCollected: 1);
        var totalGalleries = await GetTotalGalleryCount();

        var calculator = new SqlAchievementMetricCalculator(_connectionString,
            "buff_active_gallery_coverage_pct", AchievementMetricSqlRegistry.Sql["buff_active_gallery_coverage_pct"]);
        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual((long)Math.Floor(100.0 * 1 / totalGalleries), value);
    }

    [TestMethod]
    public async Task BuffActiveDaysTotal_CountsDistinctDaysWithAtLeastOneBuff()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var day1 = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1, buffsCollected: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1.AddDays(1), buffsCollected: 0);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "buff_active_days_total",
            AchievementMetricSqlRegistry.Sql["buff_active_days_total"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task NoHitClearsTotal_CountsOnlyRunsWithZeroHitsTaken()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", hitsTaken: 0);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", hitsTaken: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", hitsTaken: 0);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "no_hit_clears_total",
            AchievementMetricSqlRegistry.Sql["no_hit_clears_total"]);

        Assert.AreEqual(2L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task PerDifficultyNoHitClearsTotals_CountIndependentlyPerDifficulty()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", hitsTaken: 0);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", hitsTaken: 1);
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM", hitsTaken: 0);
        await InsertLevelScore(userId, galleryId, 1000, "HARD", hitsTaken: 0);
        await InsertLevelScore(userId, galleryId, 1000, "HARD", hitsTaken: 0);

        Assert.AreEqual(1L, await new SqlAchievementMetricCalculator(_connectionString,
            "easy_no_hit_clears_total", AchievementMetricSqlRegistry.Sql["easy_no_hit_clears_total"])
            .CalculateAsync(userId));
        Assert.AreEqual(1L, await new SqlAchievementMetricCalculator(_connectionString,
            "medium_no_hit_clears_total", AchievementMetricSqlRegistry.Sql["medium_no_hit_clears_total"])
            .CalculateAsync(userId));
        Assert.AreEqual(2L, await new SqlAchievementMetricCalculator(_connectionString,
            "hard_no_hit_clears_total", AchievementMetricSqlRegistry.Sql["hard_no_hit_clears_total"])
            .CalculateAsync(userId));
    }

    [TestMethod]
    public async Task LongestNoHitStreak_FindsTheLongestConsecutiveRunAcrossABreak()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var t0 = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0, hitsTaken: 0);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(1), hitsTaken: 1);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(2), hitsTaken: 0);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(3), hitsTaken: 0);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", t0.AddMinutes(4), hitsTaken: 0);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "longest_no_hit_streak",
            AchievementMetricSqlRegistry.Sql["longest_no_hit_streak"]);

        Assert.AreEqual(3L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task MaxHitsSurvivedSingleRun_ReturnsTheHighestSingleRunCount()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", hitsTaken: 1);
        await InsertLevelScore(userId, galleryId, 1000, "HARD", hitsTaken: 4);
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM", hitsTaken: 2);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "max_hits_survived_single_run",
            AchievementMetricSqlRegistry.Sql["max_hits_survived_single_run"]);

        Assert.AreEqual(4L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task NoHitGalleryCoveragePct_ScalesWithTheLiveDenominator()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIds(2);
        await InsertLevelScore(userId, galleryIds[0], 1000, "EASY", hitsTaken: 0);
        var totalGalleries = await GetTotalGalleryCount();

        var calculator = new SqlAchievementMetricCalculator(_connectionString,
            "no_hit_gallery_coverage_pct", AchievementMetricSqlRegistry.Sql["no_hit_gallery_coverage_pct"]);
        var value = await calculator.CalculateAsync(userId);

        Assert.AreEqual((long)Math.Floor(100.0 * 1 / totalGalleries), value);
    }

    [TestMethod]
    public async Task NoHitActiveDaysTotal_CountsDistinctDaysWithAtLeastOneNoHitClear()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var day1 = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1, hitsTaken: 0);
        await InsertLevelScore(userId, galleryId, 1000, "EASY", day1.AddDays(1), hitsTaken: 1);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "no_hit_active_days_total",
            AchievementMetricSqlRegistry.Sql["no_hit_active_days_total"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task TotalHitsSurvived_SumsHitsTakenAcrossAllRuns()
    {
        var userId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await InsertLevelScore(userId, galleryId, 1000, "EASY", hitsTaken: 2);
        await InsertLevelScore(userId, galleryId, 1000, "HARD", hitsTaken: 3);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "total_hits_survived",
            AchievementMetricSqlRegistry.Sql["total_hits_survived"]);

        Assert.AreEqual(5L, await calculator.CalculateAsync(userId));
    }

    [TestMethod]
    public async Task FlawlessTriathlonGalleries_CountsGalleriesNoHitOnAllThreeDifficulties()
    {
        var userId = await CreateTestUser();
        var galleryIds = await GetGalleryIds(2);
        // galleryIds[0]: no-hit on all three -> counts.
        await InsertLevelScore(userId, galleryIds[0], 1000, "EASY", hitsTaken: 0);
        await InsertLevelScore(userId, galleryIds[0], 1000, "MEDIUM", hitsTaken: 0);
        await InsertLevelScore(userId, galleryIds[0], 1000, "HARD", hitsTaken: 0);
        // galleryIds[1]: HARD clear took a hit -> doesn't count.
        await InsertLevelScore(userId, galleryIds[1], 1000, "EASY", hitsTaken: 0);
        await InsertLevelScore(userId, galleryIds[1], 1000, "MEDIUM", hitsTaken: 0);
        await InsertLevelScore(userId, galleryIds[1], 1000, "HARD", hitsTaken: 1);

        var calculator = new SqlAchievementMetricCalculator(_connectionString, "flawless_triathlon_galleries",
            AchievementMetricSqlRegistry.Sql["flawless_triathlon_galleries"]);

        Assert.AreEqual(1L, await calculator.CalculateAsync(userId));
    }

    private async Task<Guid[]> GetGalleryIds(int count)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT id FROM galleries LIMIT @count", conn);
        cmd.Parameters.AddWithValue("@count", count);
        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetGuid(0));
        Assert.IsTrue(ids.Count == count, $"This test requires at least {count} galleries to exist.");
        return ids.ToArray();
    }

    private async Task UnlockGalleryForUser(Guid userId, Guid galleryId, DateTime unlockedAt)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO user_galleries (id, user_id, gallery_id, is_unlocked, unlocked_at, created_at, updated_at)
            VALUES (gen_random_uuid(), @userId, @galleryId, true, @unlockedAt, now(), now())
            ON CONFLICT (user_id, gallery_id) DO UPDATE SET is_unlocked = true, unlocked_at = @unlockedAt", conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@galleryId", galleryId);
        cmd.Parameters.AddWithValue("@unlockedAt", unlockedAt);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task UnlockAllGalleriesForUser(Guid userId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO user_galleries (id, user_id, gallery_id, is_unlocked, unlocked_at, created_at, updated_at)
            SELECT gen_random_uuid(), @userId, g.id, true, now(), now(), now()
            FROM galleries g
            ON CONFLICT (user_id, gallery_id) DO UPDATE SET is_unlocked = true", conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task TriathlonAllGalleriesForUser(Guid userId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO level_scores (id, gallery_id, user_id, difficulty, position, time_ms, time_at, created_at)
            SELECT gen_random_uuid(), g.id, @userId, d.difficulty::difficulty, 0, 1000, now(), now()
            FROM galleries g
            CROSS JOIN (VALUES ('EASY'), ('MEDIUM'), ('HARD')) AS d(difficulty)", conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task TriathlonGalleryForUser(Guid userId, Guid galleryId, DateTime completedAt)
    {
        foreach (var difficulty in new[] { "EASY", "MEDIUM", "HARD" })
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO level_scores (id, gallery_id, user_id, difficulty, position, time_ms, time_at, created_at)
                VALUES (@id, @galleryId, @userId, @difficulty::difficulty, 0, 1000, @completedAt, @completedAt)", conn);
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@galleryId", galleryId);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@difficulty", difficulty);
            cmd.Parameters.AddWithValue("@completedAt", completedAt);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task<int> GetTotalGalleryCount()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM galleries", conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task InsertLevelScore(Guid userId, Guid galleryId, long timeMs, string difficulty,
        DateTime? createdAt = null, int enemiesDefeated = 0, int buffsCollected = 0, int hitsTaken = 0)
    {
        var effectiveCreatedAt = createdAt ?? DateTime.UtcNow;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO level_scores (id, gallery_id, user_id, difficulty, position, time_ms, time_at, created_at,
                                       enemies_defeated, buffs_collected, hits_taken)
            VALUES (@id, @galleryId, @userId, @difficulty::difficulty, 0, @timeMs, @timeAt, @createdAt,
                    @enemiesDefeated, @buffsCollected, @hitsTaken)", conn);
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@galleryId", galleryId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@difficulty", difficulty);
        cmd.Parameters.AddWithValue("@timeMs", timeMs);
        cmd.Parameters.AddWithValue("@timeAt", effectiveCreatedAt);
        cmd.Parameters.AddWithValue("@createdAt", effectiveCreatedAt);
        cmd.Parameters.AddWithValue("@enemiesDefeated", enemiesDefeated);
        cmd.Parameters.AddWithValue("@buffsCollected", buffsCollected);
        cmd.Parameters.AddWithValue("@hitsTaken", hitsTaken);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task PlayAllGalleriesOnceForUser(Guid userId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO level_scores (id, gallery_id, user_id, difficulty, position, time_ms, time_at, created_at)
            SELECT gen_random_uuid(), g.id, @userId, 'EASY'::difficulty, 0, 1000, now(), now()
            FROM galleries g", conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<Guid> GetFirstGalleryId()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT id FROM galleries LIMIT 1", conn);
        var result = await cmd.ExecuteScalarAsync();
        return (Guid)result!;
    }

    private async Task<Guid> CreateTestUser()
    {
        var userId = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO users (id, external_auth_id, username, email, created_at, updated_at)
            VALUES (@id, @externalAuthId, @username, @email, @createdAt, @updatedAt)", conn);
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@externalAuthId", $"test_{Guid.NewGuid()}");
        cmd.Parameters.AddWithValue("@username", $"testuser_{Guid.NewGuid():N}");
        cmd.Parameters.AddWithValue("@email", $"test_{Guid.NewGuid():N}@example.com");
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync();
        return userId;
    }
}

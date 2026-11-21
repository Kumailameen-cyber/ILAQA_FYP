using ILAQA.Features.Leaderboard;
using ILAQA.Models;

namespace ILAQA.Tests;

// FR-22 (runner dashboard) and FR-23 (Karachi rankings by area, distance and
// territory count). The ranking rules are pure functions, tested here without
// a database.
public class LeaderboardTests
{
    private static RunnerTotals Totals(string id, int territories = 0, double area = 0,
        double distance = 0, int runs = 0, int seconds = 0) =>
        new(id, territories, area, distance, runs, seconds);

    private static ApplicationUser User(string id, string name, bool isPublic = true) =>
        new() { Id = id, UserName = id, DisplayName = name, IsProfilePublic = isPublic };

    private static readonly Dictionary<string, ApplicationUser> Users = new()
    {
        ["a"] = User("a", "Ali"),
        ["b"] = User("b", "Bilal"),
        ["c"] = User("c", "Comfort", isPublic: false),
        ["d"] = User("d", "Dua"),
    };

    [Theory]
    [InlineData(null, LeaderboardMetric.Area)]
    [InlineData("", LeaderboardMetric.Area)]
    [InlineData("area", LeaderboardMetric.Area)]
    [InlineData("DISTANCE", LeaderboardMetric.Distance)]
    [InlineData("territories", LeaderboardMetric.Territories)]
    public void Metric_IsReadWithoutCase_AndDefaultsToArea(string? text, LeaderboardMetric expected)
    {
        Assert.True(LeaderboardRules.TryParseMetric(text, out var metric));
        Assert.Equal(expected, metric);
    }

    [Fact]
    public void UnknownMetric_IsRefused()
    {
        Assert.False(LeaderboardRules.TryParseMetric("xp", out _));
        Assert.False(LeaderboardRules.TryParseMetric("1", out _));
    }

    [Theory]
    [InlineData("area1")]
    [InlineData("a-b")]
    [InlineData("area,distance")]
    [InlineData("-1")]
    [InlineData("99999999999")]
    [InlineData("area;drop")]
    public void MetricText_ThatIsNotOneWord_IsRefused(string text)
    {
        Assert.False(LeaderboardRules.TryParseMetric(text, out _));
    }

    [Fact]
    public void RunnersAreRankedByTheChosenMetric_HighestFirst()
    {
        var totals = new[] { Totals("a", area: 500), Totals("b", area: 900), Totals("d", area: 100) };

        var ranked = LeaderboardRules.Rank(totals, LeaderboardMetric.Area);

        Assert.Equal(new[] { "b", "a", "d" }, ranked.Select(r => r.Totals.UserId));
        Assert.Equal(new[] { 1, 2, 3 }, ranked.Select(r => r.Rank));
    }

    [Fact]
    public void EqualValues_ShareARank_AndTheNextRankIsSkipped()
    {
        var totals = new[]
        {
            Totals("a", territories: 3), Totals("b", territories: 5),
            Totals("c", territories: 3), Totals("d", territories: 1)
        };

        var ranked = LeaderboardRules.Rank(totals, LeaderboardMetric.Territories);

        Assert.Equal(new[] { 1, 2, 2, 4 }, ranked.Select(r => r.Rank));
    }

    [Fact]
    public void EqualValues_AreListedByUserId_WhateverOrderTheyArriveIn()
    {
        var arrivedOneWay = new[] { Totals("d", area: 400), Totals("a", area: 400), Totals("b", area: 400) };
        var arrivedTheOther = arrivedOneWay.Reverse().ToArray();

        var first = LeaderboardRules.Rank(arrivedOneWay, LeaderboardMetric.Area);
        var second = LeaderboardRules.Rank(arrivedTheOther, LeaderboardMetric.Area);

        Assert.Equal(new[] { "a", "b", "d" }, first.Select(r => r.Totals.UserId));
        Assert.Equal(new[] { "a", "b", "d" }, second.Select(r => r.Totals.UserId));
    }

    [Fact]
    public void RunnersWithZero_AreNotRanked()
    {
        var totals = new[] { Totals("a", distance: 1200), Totals("b", distance: 0), Totals("d", area: 300) };

        var ranked = LeaderboardRules.Rank(totals, LeaderboardMetric.Distance);

        Assert.Equal("a", Assert.Single(ranked).Totals.UserId);
    }

    [Fact]
    public void Board_ShowsTopEntries_AndTheViewersOwnRowEvenOutsideTheTop()
    {
        var totals = new[] { Totals("a", area: 900), Totals("b", area: 800), Totals("d", area: 100) };

        var board = LeaderboardRules.BuildBoard(totals, Users, LeaderboardMetric.Area, viewerUserId: "d", limit: 2);

        Assert.Equal(new[] { "Ali", "Bilal" }, board.Entries.Select(e => e.Name));
        Assert.NotNull(board.Me);
        Assert.Equal(3, board.Me!.Rank);
        Assert.True(board.Me.IsMine);
        Assert.Equal(100, board.Me.Value);
    }

    [Fact]
    public void ViewerWithNothingYet_HasNoRow()
    {
        var board = LeaderboardRules.BuildBoard(
            new[] { Totals("a", area: 900) }, Users, LeaderboardMetric.Area, viewerUserId: "b", limit: 50);

        Assert.Null(board.Me);
    }

    [Fact]
    public void PrivateRunners_AreRankedButShownAsAnonymous_ExceptToThemselves()
    {
        var totals = new[] { Totals("c", area: 700), Totals("a", area: 100) };

        var seenByAli = LeaderboardRules.BuildBoard(totals, Users, LeaderboardMetric.Area, "a", 50);
        var seenByComfort = LeaderboardRules.BuildBoard(totals, Users, LeaderboardMetric.Area, "c", 50);

        Assert.Equal("Anonymous profile", seenByAli.Entries[0].Name);
        Assert.Equal("Comfort", seenByComfort.Entries[0].Name);
    }

    [Fact]
    public void RunnerWithNoUserRecord_IsShownAsAnonymous_NotAsTheUserId()
    {
        var totals = new[] { Totals("ghost-id-123", area: 700), Totals("a", area: 100) };

        var board = LeaderboardRules.BuildBoard(totals, Users, LeaderboardMetric.Area, "a", 50);

        Assert.Equal("Anonymous profile", board.Entries[0].Name);
        Assert.DoesNotContain("ghost-id-123", board.Entries[0].Name);
    }

    [Fact]
    public void Values_AreRoundedForDisplay()
    {
        var board = LeaderboardRules.BuildBoard(
            new[] { Totals("a", area: 1234.5678) }, Users, LeaderboardMetric.Area, "a", 50);

        Assert.Equal(1234.57, board.Entries[0].Value);
    }

    [Fact]
    public void Limit_IsKeptBetween1And100()
    {
        Assert.Equal(50, LeaderboardRules.ClampLimit(null));
        Assert.Equal(1, LeaderboardRules.ClampLimit(0));
        Assert.Equal(100, LeaderboardRules.ClampLimit(5000));
        Assert.Equal(20, LeaderboardRules.ClampLimit(20));
    }

    [Fact]
    public void NegativeLimit_IsRaisedToOne()
    {
        Assert.Equal(1, LeaderboardRules.ClampLimit(-5));
    }

    [Fact]
    public void MyStats_AddUpMyLandRunsAndRanks()
    {
        var totals = new[]
        {
            Totals("a", territories: 2, area: 500, distance: 3000, runs: 2, seconds: 1800),
            Totals("b", territories: 4, area: 900, distance: 1000, runs: 1, seconds: 600),
        };

        var stats = LeaderboardRules.StatsFor("a", totals, shieldedCount: 1, vulnerableCount: 1);

        Assert.Equal(2, stats.TerritoryCount);
        Assert.Equal(500, stats.TotalAreaSquareMeters);
        Assert.Equal(1, stats.ShieldedCount);
        Assert.Equal(1, stats.VulnerableCount);
        Assert.Equal(2, stats.CompletedRuns);
        Assert.Equal(3000, stats.TotalDistanceMeters);
        Assert.Equal(1800, stats.TotalActiveSeconds);
        Assert.Equal(2, stats.AreaRank);
        Assert.Equal(1, stats.DistanceRank);
        Assert.Equal(2, stats.TerritoryRank);
    }

    [Fact]
    public void NewRunner_GetsZerosAndNoRanks()
    {
        var stats = LeaderboardRules.StatsFor("new", new[] { Totals("a", area: 10) }, 0, 0);

        Assert.Equal(0, stats.TerritoryCount);
        Assert.Equal(0, stats.TotalDistanceMeters);
        Assert.Null(stats.AreaRank);
        Assert.Null(stats.DistanceRank);
        Assert.Null(stats.TerritoryRank);
    }
}

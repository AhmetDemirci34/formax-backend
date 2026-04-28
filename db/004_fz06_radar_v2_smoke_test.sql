-- FZ-06 smoke test
SELECT TOP 20 Id, UserId, [Layer], [Key], Score, LastEventAtUtc, UpdatedAtUtc
FROM UserInterestScores
ORDER BY Id DESC;

-- Expected radar response fields after FZ-06:
-- behaviorMomentumScore
-- matchHeatScore
-- explainability starting with RadarV2

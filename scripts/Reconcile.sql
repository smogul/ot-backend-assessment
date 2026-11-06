-- Example: count the betting slips in the box, and add up their amounts. The number of slips and the total must match what the shop handed in.
USE OT_Assessment_DB;
SET NOCOUNT ON;

SELECT
    COUNT(*) AS WagersStored,
    COUNT(DISTINCT WagerId) AS UniqueWagerIds,
    SUM(Amount) AS TotalAmount,
    DATEDIFF(MILLISECOND, MIN(SavedDateUtc), MAX(SavedDateUtc)) / 1000.0 AS SecondsFirstSaveToLastSave
FROM dbo.CasinoWager;

SELECT
    (SELECT COUNT(*) FROM dbo.Player) AS Players,
    (SELECT COUNT(*) FROM dbo.Provider) AS Providers,
    (SELECT COUNT(*) FROM dbo.Game) AS Games;

EXEC dbo.usp_Player_GetTopSpenders @Count = 1;
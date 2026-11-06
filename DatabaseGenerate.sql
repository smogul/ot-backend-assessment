USE master;
GO

-- Example: build the warehouse only if the plot is still empty.
IF DB_ID(N'OT_Assessment_DB') IS NULL
    CREATE DATABASE OT_Assessment_DB;
GO

-- Example: you can read today's printed notice while someone is typing tomorrow's.
ALTER DATABASE OT_Assessment_DB SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

USE OT_Assessment_DB;
GO

-- Example: Thabo is stored once here, even if he places 1000 wagers.
IF OBJECT_ID(N'dbo.Player', N'U') IS NULL
CREATE TABLE dbo.Player
(
    PlayerKey INT IDENTITY(1,1) NOT NULL,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    Username NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_Player PRIMARY KEY CLUSTERED (PlayerKey),
    CONSTRAINT UQ_Player_AccountId UNIQUE (AccountId)
);
GO

-- Example: "Fun Games" is stored once, however many games it makes.
IF OBJECT_ID(N'dbo.Provider', N'U') IS NULL
CREATE TABLE dbo.Provider
(
    ProviderKey INT IDENTITY(1,1) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_Provider PRIMARY KEY CLUSTERED (ProviderKey),
    CONSTRAINT UQ_Provider_Name UNIQUE (Name)
);
GO

-- Example: "Lucky Fish" by Fun Games and "Lucky Fish" by Big Wins are two games.
IF OBJECT_ID(N'dbo.Game', N'U') IS NULL
CREATE TABLE dbo.Game
(
    GameKey INT IDENTITY(1,1) NOT NULL,
    ProviderKey INT NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    Theme NVARCHAR(50) NOT NULL,
    CONSTRAINT PK_Game PRIMARY KEY CLUSTERED (GameKey),
    CONSTRAINT UQ_Game_ProviderKey_Name UNIQUE (ProviderKey, Name),
    CONSTRAINT FK_Game_Provider FOREIGN KEY (ProviderKey) REFERENCES dbo.Provider (ProviderKey)
);
GO

-- Example: "Thabo bet R50 on Lucky Fish" is one row with Thabo's number, the game's number and the amount 50.
IF OBJECT_ID(N'dbo.CasinoWager', N'U') IS NULL
CREATE TABLE dbo.CasinoWager
(
    CasinoWagerKey BIGINT IDENTITY(1,1) NOT NULL,
    WagerId UNIQUEIDENTIFIER NOT NULL,
    PlayerKey INT NOT NULL,
    GameKey INT NOT NULL,
    Amount DECIMAL(28,15) NOT NULL,
    CreatedDateUtc DATETIME2(7) NOT NULL,
    TransactionId UNIQUEIDENTIFIER NOT NULL,
    ExternalReferenceId UNIQUEIDENTIFIER NOT NULL,
    BrandId UNIQUEIDENTIFIER NOT NULL,
    TransactionTypeId UNIQUEIDENTIFIER NOT NULL,
    NumberOfBets INT NOT NULL,
    CountryCode CHAR(2) NOT NULL,
    Duration BIGINT NOT NULL,
    SavedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CasinoWager_SavedDateUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_CasinoWager PRIMARY KEY CLUSTERED (CasinoWagerKey),
    CONSTRAINT UQ_CasinoWager_WagerId UNIQUE (WagerId),
    CONSTRAINT FK_CasinoWager_Player FOREIGN KEY (PlayerKey) REFERENCES dbo.Player (PlayerKey),
    CONSTRAINT FK_CasinoWager_Game FOREIGN KEY (GameKey) REFERENCES dbo.Game (GameKey)
);
GO

-- Example: a school register sorted by child, then by date. To find Thabo's latest marks, or add up his marks, you open his section and never read the whole book.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CasinoWager_PlayerKey_CreatedDateUtc')
CREATE NONCLUSTERED INDEX IX_CasinoWager_PlayerKey_CreatedDateUtc
    ON dbo.CasinoWager (PlayerKey, CreatedDateUtc DESC, CasinoWagerKey DESC)
    INCLUDE (WagerId, GameKey, Amount);
GO

-- Example: a form where box 1 is always the wager number and box 2 the player.
IF TYPE_ID(N'dbo.CasinoWagerTableType') IS NULL
CREATE TYPE dbo.CasinoWagerTableType AS TABLE
(
    WagerId UNIQUEIDENTIFIER NOT NULL,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    Username NVARCHAR(100) NOT NULL,
    ProviderName NVARCHAR(100) NOT NULL,
    GameName NVARCHAR(100) NOT NULL,
    Theme NVARCHAR(50) NOT NULL,
    Amount DECIMAL(28,15) NOT NULL,
    CreatedDateUtc DATETIME2(7) NOT NULL,
    TransactionId UNIQUEIDENTIFIER NOT NULL,
    ExternalReferenceId UNIQUEIDENTIFIER NOT NULL,
    BrandId UNIQUEIDENTIFIER NOT NULL,
    TransactionTypeId UNIQUEIDENTIFIER NOT NULL,
    NumberOfBets INT NOT NULL,
    CountryCode CHAR(2) NOT NULL,
    Duration BIGINT NOT NULL
);
GO

-- Example: 500 wagers arrive and 2 of them were saved before a crash. The 498 new ones are added and the 2 old ones are skipped, so no wager is counted twice.
-- Example: the same game sent with two themes.
CREATE OR ALTER PROCEDURE dbo.usp_CasinoWager_InsertBatch
    @Wagers dbo.CasinoWagerTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    INSERT INTO dbo.Provider (Name)
    SELECT DISTINCT incoming.ProviderName
    FROM @Wagers AS incoming
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.Provider AS saved WHERE saved.Name = incoming.ProviderName
    );

    INSERT INTO dbo.Game (ProviderKey, Name, Theme)
    SELECT provider.ProviderKey, incoming.GameName, MIN(incoming.Theme)
    FROM @Wagers AS incoming
    INNER JOIN dbo.Provider AS provider ON provider.Name = incoming.ProviderName
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Game AS saved
        WHERE saved.ProviderKey = provider.ProviderKey
          AND saved.Name = incoming.GameName
    )
    GROUP BY provider.ProviderKey, incoming.GameName;

    INSERT INTO dbo.Player (AccountId, Username)
    SELECT incoming.AccountId, MIN(incoming.Username)
    FROM @Wagers AS incoming
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.Player AS saved WHERE saved.AccountId = incoming.AccountId
    )
    GROUP BY incoming.AccountId;

    WITH NumberedWagers AS
    (
        SELECT *, ROW_NUMBER() OVER (PARTITION BY WagerId ORDER BY WagerId) AS CopyNumber
        FROM @Wagers
    )
    INSERT INTO dbo.CasinoWager
    (
        WagerId, PlayerKey, GameKey, Amount, CreatedDateUtc, TransactionId,
        ExternalReferenceId, BrandId, TransactionTypeId, NumberOfBets, CountryCode, Duration
    )
    SELECT
        incoming.WagerId, player.PlayerKey, game.GameKey, incoming.Amount,
        incoming.CreatedDateUtc, incoming.TransactionId, incoming.ExternalReferenceId,
        incoming.BrandId, incoming.TransactionTypeId, incoming.NumberOfBets,
        incoming.CountryCode, incoming.Duration
    FROM NumberedWagers AS incoming
    INNER JOIN dbo.Player AS player ON player.AccountId = incoming.AccountId
    INNER JOIN dbo.Provider AS provider ON provider.Name = incoming.ProviderName
    INNER JOIN dbo.Game AS game
        ON game.ProviderKey = provider.ProviderKey
        AND game.Name = incoming.GameName
    WHERE incoming.CopyNumber = 1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.CasinoWager AS saved WHERE saved.WagerId = incoming.WagerId
      );

    COMMIT TRANSACTION;
END;
GO

-- Example: Thabo has 35 wagers. Page 2 with a page size of 10 skips his newest 10, returns the next 10, and then returns 35.
CREATE OR ALTER PROCEDURE dbo.usp_CasinoWager_GetByPlayer
    @AccountId UNIQUEIDENTIFIER,
    @Page INT,
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PlayerKey INT = (SELECT PlayerKey FROM dbo.Player WHERE AccountId = @AccountId);

    SELECT
        wager.WagerId,
        game.Name AS Game,
        provider.Name AS Provider,
        wager.Amount,
        wager.CreatedDateUtc AS CreatedDate
    FROM dbo.CasinoWager AS wager
    INNER JOIN dbo.Game AS game ON game.GameKey = wager.GameKey
    INNER JOIN dbo.Provider AS provider ON provider.ProviderKey = game.ProviderKey
    WHERE wager.PlayerKey = @PlayerKey
    ORDER BY wager.CreatedDateUtc DESC, wager.CasinoWagerKey DESC
    OFFSET (CAST(@Page AS BIGINT) - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS Total
    FROM dbo.CasinoWager
    WHERE PlayerKey = @PlayerKey;
END;
GO

-- Example: Thabo spent R150 and Lerato spent R100. A count of 1 returns only Thabo.
CREATE OR ALTER PROCEDURE dbo.usp_Player_GetTopSpenders
    @Count INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Count)
        player.AccountId,
        player.Username,
        SUM(wager.Amount) AS TotalAmountSpend
    FROM dbo.CasinoWager AS wager
    INNER JOIN dbo.Player AS player ON player.PlayerKey = wager.PlayerKey
    GROUP BY player.AccountId, player.Username
    ORDER BY TotalAmountSpend DESC, player.AccountId;
END;
GO
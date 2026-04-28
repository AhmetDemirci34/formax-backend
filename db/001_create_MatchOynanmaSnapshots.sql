-- FORMAX v1.3 — DB ADD: MatchOynanmaSnapshots
-- Amaç: OynanmaSkoru snapshot saklamak (Backtest & history)
-- FormaxDB üzerinde çalıştır.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MatchOynanmaSnapshots')
BEGIN
    CREATE TABLE [dbo].[MatchOynanmaSnapshots](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [MatchId] INT NOT NULL,
        [OynanmaSkoru] INT NOT NULL,
        [CapturedAtUtc] DATETIME2 NOT NULL,
        [Source] NVARCHAR(32) NULL
    );

    CREATE INDEX IX_MatchOynanmaSnapshots_MatchId_CapturedAtUtc
        ON [dbo].[MatchOynanmaSnapshots]([MatchId],[CapturedAtUtc]);

    -- FK opsiyonel (Match silme davranışına göre ayarlanır)
    -- ALTER TABLE [dbo].[MatchOynanmaSnapshots]
    -- ADD CONSTRAINT FK_MatchOynanmaSnapshots_Matches
    -- FOREIGN KEY ([MatchId]) REFERENCES [dbo].[Matches]([Id]);
END
GO

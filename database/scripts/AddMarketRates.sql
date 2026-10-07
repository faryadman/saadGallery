BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE TABLE [MarketFetchLease] (
        [Id] int NOT NULL IDENTITY,
        [OwnerId] nvarchar(64) NULL,
        [AcquiredAtUtc] datetimeoffset NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_MarketFetchLease] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE TABLE [MarketRateFetchRuns] (
        [Id] bigint NOT NULL IDENTITY,
        [ProviderId] nvarchar(64) NOT NULL,
        [Trigger] nvarchar(32) NOT NULL,
        [StartedAtUtc] datetimeoffset NOT NULL,
        [FinishedAtUtc] datetimeoffset NULL,
        [DurationMs] int NOT NULL,
        [Outcome] int NOT NULL,
        [HttpStatusCode] int NULL,
        [AcceptedCount] int NOT NULL,
        [FlaggedCount] int NOT NULL,
        [RejectedCount] int NOT NULL,
        [ErrorCode] nvarchar(64) NULL,
        [Notes] nvarchar(512) NULL,
        CONSTRAINT [PK_MarketRateFetchRuns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE TABLE [MarketRates] (
        [Id] bigint NOT NULL IDENTITY,
        [AssetCode] nvarchar(64) NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [QuoteUnit] int NOT NULL,
        [ProviderId] nvarchar(64) NOT NULL,
        [QuotedAtUtc] datetimeoffset NOT NULL,
        [FetchedAtUtc] datetimeoffset NOT NULL,
        [Quality] int NOT NULL,
        [ProviderRawValue] decimal(18,4) NULL,
        [ScaleApplied] decimal(18,6) NOT NULL,
        [IsAnomalySuspected] bit NOT NULL,
        [FetchRunId] bigint NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_MarketRates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MarketRates_MarketRateFetchRuns_FetchRunId] FOREIGN KEY ([FetchRunId]) REFERENCES [MarketRateFetchRuns] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AcquiredAtUtc', N'ExpiresAtUtc', N'OwnerId') AND [object_id] = OBJECT_ID(N'[MarketFetchLease]'))
        SET IDENTITY_INSERT [MarketFetchLease] ON;
    EXEC(N'INSERT INTO [MarketFetchLease] ([Id], [AcquiredAtUtc], [ExpiresAtUtc], [OwnerId])
    VALUES (1, ''1970-01-01T00:00:00.0000000+00:00'', ''1970-01-01T00:00:00.0000000+00:00'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AcquiredAtUtc', N'ExpiresAtUtc', N'OwnerId') AND [object_id] = OBJECT_ID(N'[MarketFetchLease]'))
        SET IDENTITY_INSERT [MarketFetchLease] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE INDEX [IX_MarketRateFetchRuns_StartedAtUtc] ON [MarketRateFetchRuns] ([StartedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE INDEX [IX_MarketRates_AssetCode_Id] ON [MarketRates] ([AssetCode], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE INDEX [IX_MarketRates_FetchedAtUtc] ON [MarketRates] ([FetchedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE INDEX [IX_MarketRates_FetchRunId] ON [MarketRates] ([FetchRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007223427_AddMarketRates', N'10.0.12');
END;

COMMIT;
GO


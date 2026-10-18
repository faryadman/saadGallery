BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    CREATE TABLE [GoldTradingSettings] (
        [Id] int NOT NULL,
        [Enabled] bit NOT NULL,
        [FeePercent] decimal(6,3) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_GoldTradingSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_GoldSettings_Fee] CHECK (([FeePercent] IS NULL AND [Enabled] = 0) OR ([FeePercent] IS NOT NULL AND [FeePercent] BETWEEN 0 AND 100)),
        CONSTRAINT [CK_GoldSettings_Singleton] CHECK ([Id] = 1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    CREATE TABLE [GoldWallets] (
        [UserId] int NOT NULL,
        [Enabled] bit NOT NULL,
        [CashIrt] decimal(18,0) NOT NULL,
        [ReservedIrt] decimal(18,0) NOT NULL,
        [Grams750] decimal(18,3) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_GoldWallets] PRIMARY KEY ([UserId]),
        CONSTRAINT [CK_GoldWallet_Balances] CHECK ([CashIrt] >= 0 AND [CashIrt] <= 999999999999999 AND [ReservedIrt] >= 0 AND [ReservedIrt] <= [CashIrt] AND [Grams750] >= 0 AND [Grams750] <= 999999),
        CONSTRAINT [FK_GoldWallets_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    CREATE TABLE [GoldOrders] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] int NOT NULL,
        [Status] int NOT NULL,
        [RateSnapshotId] bigint NOT NULL,
        [Grams750] decimal(18,3) NOT NULL,
        [RateIrt] decimal(18,4) NOT NULL,
        [FeePercent] decimal(6,3) NOT NULL,
        [PrincipalIrt] decimal(18,0) NOT NULL,
        [FeeIrt] decimal(18,0) NOT NULL,
        [TotalIrt] decimal(18,0) NOT NULL,
        [Fineness] int NOT NULL,
        [FormulaVersion] nvarchar(32) NOT NULL,
        [QuotedAtUtc] datetimeoffset NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        [RequestedAtUtc] datetimeoffset NULL,
        [ReviewedAtUtc] datetimeoffset NULL,
        [ReviewerId] int NULL,
        [ReviewNote] nvarchar(300) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_GoldOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_GoldOrder_Amounts] CHECK ([Grams750] > 0 AND [Grams750] <= 999999 AND [RateIrt] > 0 AND [RateIrt] <= 999999999999999 AND [FeePercent] BETWEEN 0 AND 100 AND [PrincipalIrt] > 0 AND [FeeIrt] >= 0 AND [TotalIrt] = [PrincipalIrt] + [FeeIrt] AND [TotalIrt] <= 999999999999999 AND [Fineness] = 750),
        CONSTRAINT [CK_GoldOrder_Status] CHECK (([Status] = 0 AND [RequestedAtUtc] IS NULL AND [ReviewedAtUtc] IS NULL AND [ReviewerId] IS NULL) OR ([Status] = 1 AND [RequestedAtUtc] IS NOT NULL AND [ReviewedAtUtc] IS NULL AND [ReviewerId] IS NULL) OR ([Status] IN (2,3,4) AND [RequestedAtUtc] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL AND [ReviewerId] IS NOT NULL)),
        CONSTRAINT [FK_GoldOrders_AspNetUsers_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GoldOrders_GoldWallets_UserId] FOREIGN KEY ([UserId]) REFERENCES [GoldWallets] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GoldOrders_MarketRates_RateSnapshotId] FOREIGN KEY ([RateSnapshotId]) REFERENCES [MarketRates] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    CREATE TABLE [GoldLedger] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] int NOT NULL,
        [ActorId] int NOT NULL,
        [OrderId] uniqueidentifier NULL,
        [Kind] int NOT NULL,
        [CashDeltaIrt] decimal(18,0) NOT NULL,
        [GoldDeltaGrams] decimal(18,3) NOT NULL,
        [Reason] nvarchar(300) NOT NULL,
        [AtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_GoldLedger] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_GoldLedger_Kind] CHECK (([Kind] = 1 AND [CashDeltaIrt] > 0 AND [GoldDeltaGrams] = 0 AND [OrderId] IS NULL) OR ([Kind] = 2 AND [CashDeltaIrt] = 0 AND [GoldDeltaGrams] > 0 AND [OrderId] IS NULL) OR ([Kind] = 3 AND [CashDeltaIrt] < 0 AND [GoldDeltaGrams] > 0 AND [OrderId] IS NOT NULL)),
        CONSTRAINT [FK_GoldLedger_AspNetUsers_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GoldLedger_GoldOrders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [GoldOrders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GoldLedger_GoldWallets_UserId] FOREIGN KEY ([UserId]) REFERENCES [GoldWallets] ([UserId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'FeePercent') AND [object_id] = OBJECT_ID(N'[GoldTradingSettings]'))
        SET IDENTITY_INSERT [GoldTradingSettings] ON;
    EXEC(N'INSERT INTO [GoldTradingSettings] ([Id], [Enabled], [FeePercent])
    VALUES (1, CAST(0 AS bit), NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'FeePercent') AND [object_id] = OBJECT_ID(N'[GoldTradingSettings]'))
        SET IDENTITY_INSERT [GoldTradingSettings] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    CREATE INDEX [IX_GoldLedger_ActorId] ON [GoldLedger] ([ActorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_GoldLedger_OrderId] ON [GoldLedger] ([OrderId]) WHERE [OrderId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    CREATE INDEX [IX_GoldLedger_UserId_AtUtc] ON [GoldLedger] ([UserId], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    CREATE INDEX [IX_GoldOrders_RateSnapshotId] ON [GoldOrders] ([RateSnapshotId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    CREATE INDEX [IX_GoldOrders_ReviewerId] ON [GoldOrders] ([ReviewerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    CREATE INDEX [IX_GoldOrders_Status_RequestedAtUtc] ON [GoldOrders] ([Status], [RequestedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    CREATE INDEX [IX_GoldOrders_UserId_RequestedAtUtc] ON [GoldOrders] ([UserId], [RequestedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    EXEC(N'CREATE TRIGGER [TR_GoldLedger_AppendOnly] ON [GoldLedger]
    INSTEAD OF UPDATE, DELETE AS
    BEGIN
        SET NOCOUNT ON;
        THROW 51001, ''Gold ledger is append-only'', 1;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018214550_GoldTradingWallets'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018214550_GoldTradingWallets', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016010911_AdministrationSettingsAudit'
)
BEGIN
    CREATE TABLE [AuditEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [ActorUserId] int NULL,
        [Action] nvarchar(80) NOT NULL,
        [EntityType] nvarchar(80) NOT NULL,
        [EntityId] nvarchar(64) NOT NULL,
        [Fields] nvarchar(1000) NOT NULL,
        [IpAddress] nvarchar(45) NULL,
        [CorrelationId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016010911_AdministrationSettingsAudit'
)
BEGIN
    CREATE TABLE [StoreSettings] (
        [Id] int NOT NULL,
        [RatesPaused] bit NOT NULL,
        [FetchIntervalSeconds] int NULL,
        [UpdatedAtUtc] datetimeoffset NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_StoreSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StoreSettings_Interval] CHECK ([FetchIntervalSeconds] IS NULL OR [FetchIntervalSeconds] BETWEEN 10 AND 3600),
        CONSTRAINT [CK_StoreSettings_Singleton] CHECK ([Id] = 1),
        CONSTRAINT [FK_StoreSettings_AspNetUsers_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016010911_AdministrationSettingsAudit'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FetchIntervalSeconds', N'RatesPaused', N'UpdatedAtUtc', N'UpdatedByUserId') AND [object_id] = OBJECT_ID(N'[StoreSettings]'))
        SET IDENTITY_INSERT [StoreSettings] ON;
    EXEC(N'INSERT INTO [StoreSettings] ([Id], [FetchIntervalSeconds], [RatesPaused], [UpdatedAtUtc], [UpdatedByUserId])
    VALUES (1, NULL, CAST(0 AS bit), NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FetchIntervalSeconds', N'RatesPaused', N'UpdatedAtUtc', N'UpdatedByUserId') AND [object_id] = OBJECT_ID(N'[StoreSettings]'))
        SET IDENTITY_INSERT [StoreSettings] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016010911_AdministrationSettingsAudit'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_ActorUserId_OccurredAtUtc] ON [AuditEvents] ([ActorUserId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016010911_AdministrationSettingsAudit'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_OccurredAtUtc] ON [AuditEvents] ([OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016010911_AdministrationSettingsAudit'
)
BEGIN
    CREATE INDEX [IX_StoreSettings_UpdatedByUserId] ON [StoreSettings] ([UpdatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016010911_AdministrationSettingsAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016010911_AdministrationSettingsAudit', N'10.0.12');
END;

COMMIT;
GO

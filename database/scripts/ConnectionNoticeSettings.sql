BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016003908_ConnectionNoticeSettings'
)
BEGIN
    CREATE TABLE [ConnectionNoticeEdits] (
        [Id] bigint NOT NULL IDENTITY,
        [ActorUserId] int NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [Fields] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ConnectionNoticeEdits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConnectionNoticeEdits_AspNetUsers_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016003908_ConnectionNoticeSettings'
)
BEGIN
    CREATE TABLE [ConnectionNoticeSettings] (
        [Id] int NOT NULL,
        [OfflineTitle] nvarchar(80) NOT NULL,
        [OfflineDescription] nvarchar(200) NOT NULL,
        [OnlineTitle] nvarchar(80) NOT NULL,
        [OnlineDescription] nvarchar(200) NOT NULL,
        [UpdatedAtUtc] datetimeoffset NULL,
        [UpdatedByUserId] int NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ConnectionNoticeSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ConnectionNoticeSettings_Singleton] CHECK ([Id] = 1),
        CONSTRAINT [FK_ConnectionNoticeSettings_AspNetUsers_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016003908_ConnectionNoticeSettings'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'OfflineDescription', N'OfflineTitle', N'OnlineDescription', N'OnlineTitle', N'UpdatedAtUtc', N'UpdatedByUserId') AND [object_id] = OBJECT_ID(N'[ConnectionNoticeSettings]'))
        SET IDENTITY_INSERT [ConnectionNoticeSettings] ON;
    EXEC(N'INSERT INTO [ConnectionNoticeSettings] ([Id], [OfflineDescription], [OfflineTitle], [OnlineDescription], [OnlineTitle], [UpdatedAtUtc], [UpdatedByUserId])
    VALUES (1, N''اطلاعات صفحه به‌روز نمی‌شود؛ نرخ‌های ذخیره‌شده لحظه‌ای نیستند.'', N''اتصال اینترنت قطع است'', N''برای دریافت اطلاعات تازه، صفحه را به‌روز کنید.'', N''اتصال شبکه برگشت'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'OfflineDescription', N'OfflineTitle', N'OnlineDescription', N'OnlineTitle', N'UpdatedAtUtc', N'UpdatedByUserId') AND [object_id] = OBJECT_ID(N'[ConnectionNoticeSettings]'))
        SET IDENTITY_INSERT [ConnectionNoticeSettings] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016003908_ConnectionNoticeSettings'
)
BEGIN
    CREATE INDEX [IX_ConnectionNoticeEdits_ActorUserId] ON [ConnectionNoticeEdits] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016003908_ConnectionNoticeSettings'
)
BEGIN
    CREATE INDEX [IX_ConnectionNoticeEdits_CreatedAtUtc] ON [ConnectionNoticeEdits] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016003908_ConnectionNoticeSettings'
)
BEGIN
    CREATE INDEX [IX_ConnectionNoticeSettings_UpdatedByUserId] ON [ConnectionNoticeSettings] ([UpdatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016003908_ConnectionNoticeSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016003908_ConnectionNoticeSettings', N'10.0.12');
END;

COMMIT;
GO


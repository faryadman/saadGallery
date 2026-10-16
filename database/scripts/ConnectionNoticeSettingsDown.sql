BEGIN TRANSACTION;
DROP TABLE [ConnectionNoticeEdits];

DROP TABLE [ConnectionNoticeSettings];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261016003908_ConnectionNoticeSettings';

COMMIT;
GO


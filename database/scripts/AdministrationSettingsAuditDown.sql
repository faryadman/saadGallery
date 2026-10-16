BEGIN TRANSACTION;
DROP TABLE [AuditEvents];

DROP TABLE [StoreSettings];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261016010911_AdministrationSettingsAudit';

COMMIT;
GO

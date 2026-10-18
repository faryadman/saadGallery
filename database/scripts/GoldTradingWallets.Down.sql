BEGIN TRANSACTION;
DROP TABLE [GoldLedger];

DROP TABLE [GoldTradingSettings];

DROP TABLE [GoldOrders];

DROP TABLE [GoldWallets];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261018214550_GoldTradingWallets';

COMMIT;
GO

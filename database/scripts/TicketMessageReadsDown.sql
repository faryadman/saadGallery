BEGIN TRANSACTION;
DROP TABLE [TicketMessageReads];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261013222157_TicketMessageReads';

COMMIT;
GO


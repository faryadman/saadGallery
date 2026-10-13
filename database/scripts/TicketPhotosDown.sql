BEGIN TRANSACTION;
DROP TABLE [TicketPhotos];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261013214044_TicketPhotos';

COMMIT;
GO

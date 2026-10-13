BEGIN TRANSACTION;
DROP TABLE [TicketEvents];

DROP TABLE [TicketInternalNotes];

DROP TABLE [TicketMessages];

DROP TABLE [SupportTickets];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261013201424_TicketsSchema';

COMMIT;
GO

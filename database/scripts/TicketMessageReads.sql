BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013222157_TicketMessageReads'
)
BEGIN
    CREATE TABLE [TicketMessageReads] (
        [UserId] int NOT NULL,
        [OperatorView] bit NOT NULL,
        [MessageId] bigint NOT NULL,
        [ReadAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TicketMessageReads] PRIMARY KEY ([UserId], [OperatorView], [MessageId]),
        CONSTRAINT [FK_TicketMessageReads_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TicketMessageReads_TicketMessages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [TicketMessages] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013222157_TicketMessageReads'
)
BEGIN
    CREATE INDEX [IX_TicketMessageReads_MessageId] ON [TicketMessageReads] ([MessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013222157_TicketMessageReads'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013222157_TicketMessageReads', N'10.0.12');
END;

COMMIT;
GO


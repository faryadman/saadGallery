IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] int NOT NULL IDENTITY,
        [DisplayName] nvarchar(100) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [LastLoginAtUtc] datetimeoffset NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] int NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_IsActive] ON [AspNetUsers] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005172327_InitialIdentity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005172327_InitialIdentity', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007153604_ini-01'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007153604_ini-01', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE TABLE [MarketFetchLease] (
        [Id] int NOT NULL IDENTITY,
        [OwnerId] nvarchar(64) NULL,
        [AcquiredAtUtc] datetimeoffset NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_MarketFetchLease] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE TABLE [MarketRateFetchRuns] (
        [Id] bigint NOT NULL IDENTITY,
        [ProviderId] nvarchar(64) NOT NULL,
        [Trigger] nvarchar(32) NOT NULL,
        [StartedAtUtc] datetimeoffset NOT NULL,
        [FinishedAtUtc] datetimeoffset NULL,
        [DurationMs] int NOT NULL,
        [Outcome] int NOT NULL,
        [HttpStatusCode] int NULL,
        [AcceptedCount] int NOT NULL,
        [FlaggedCount] int NOT NULL,
        [RejectedCount] int NOT NULL,
        [ErrorCode] nvarchar(64) NULL,
        [Notes] nvarchar(512) NULL,
        CONSTRAINT [PK_MarketRateFetchRuns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE TABLE [MarketRates] (
        [Id] bigint NOT NULL IDENTITY,
        [AssetCode] nvarchar(64) NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [QuoteUnit] int NOT NULL,
        [ProviderId] nvarchar(64) NOT NULL,
        [QuotedAtUtc] datetimeoffset NOT NULL,
        [FetchedAtUtc] datetimeoffset NOT NULL,
        [Quality] int NOT NULL,
        [ProviderRawValue] decimal(18,4) NULL,
        [ScaleApplied] decimal(18,6) NOT NULL,
        [IsAnomalySuspected] bit NOT NULL,
        [FetchRunId] bigint NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_MarketRates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MarketRates_MarketRateFetchRuns_FetchRunId] FOREIGN KEY ([FetchRunId]) REFERENCES [MarketRateFetchRuns] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AcquiredAtUtc', N'ExpiresAtUtc', N'OwnerId') AND [object_id] = OBJECT_ID(N'[MarketFetchLease]'))
        SET IDENTITY_INSERT [MarketFetchLease] ON;
    EXEC(N'INSERT INTO [MarketFetchLease] ([Id], [AcquiredAtUtc], [ExpiresAtUtc], [OwnerId])
    VALUES (1, ''1970-01-01T00:00:00.0000000+00:00'', ''1970-01-01T00:00:00.0000000+00:00'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AcquiredAtUtc', N'ExpiresAtUtc', N'OwnerId') AND [object_id] = OBJECT_ID(N'[MarketFetchLease]'))
        SET IDENTITY_INSERT [MarketFetchLease] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE INDEX [IX_MarketRateFetchRuns_StartedAtUtc] ON [MarketRateFetchRuns] ([StartedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE INDEX [IX_MarketRates_AssetCode_Id] ON [MarketRates] ([AssetCode], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE INDEX [IX_MarketRates_FetchedAtUtc] ON [MarketRates] ([FetchedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    CREATE INDEX [IX_MarketRates_FetchRunId] ON [MarketRates] ([FetchRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007223427_AddMarketRates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007223427_AddMarketRates', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084707_AddProductsAndMedia'
)
BEGIN
    CREATE TABLE [ProductCategories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084707_AddProductsAndMedia'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Summary] nvarchar(400) NULL,
        [Description] nvarchar(max) NULL,
        [CategoryId] int NULL,
        [PricePolicy] int NOT NULL,
        [FixedPriceIrt] decimal(18,2) NULL,
        [WeightGrams] decimal(10,3) NULL,
        [Karat] decimal(5,2) NULL,
        [MakingChargePercent] decimal(6,3) NOT NULL,
        [ProfitPercent] decimal(6,3) NOT NULL,
        [TaxPercent] decimal(6,3) NOT NULL,
        [IsInStock] bit NOT NULL,
        [IsPublished] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NULL,
        [PublishedAtUtc] datetimeoffset NULL,
        [DeletedAtUtc] datetimeoffset NULL,
        [CreatedByUserId] int NOT NULL,
        [UpdatedByUserId] int NULL,
        [DeletedByUserId] int NULL,
        [PriceTotalIrt] decimal(18,2) NULL,
        [PriceGoldValueIrt] decimal(18,2) NULL,
        [PriceMakingIrt] decimal(18,2) NULL,
        [PriceProfitIrt] decimal(18,2) NULL,
        [PriceTaxIrt] decimal(18,2) NULL,
        [PriceRateAmountIrt] decimal(18,2) NULL,
        [PriceRateQuotedAtUtc] datetimeoffset NULL,
        [PriceWeightGrams] decimal(10,3) NULL,
        [PriceKarat] decimal(5,2) NULL,
        [PriceMakingPercent] decimal(6,3) NULL,
        [PriceProfitPercent] decimal(6,3) NULL,
        [PriceTaxPercent] decimal(6,3) NULL,
        [PriceComputedAtUtc] datetimeoffset NULL,
        [PriceFormulaVersion] nvarchar(32) NULL,
        [PriceReason] nvarchar(500) NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_ProductCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [ProductCategories] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084707_AddProductsAndMedia'
)
BEGIN
    CREATE TABLE [ProductImages] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [StoredFileName] nvarchar(64) NOT NULL,
        [ThumbnailFileName] nvarchar(64) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Width] int NOT NULL,
        [Height] int NOT NULL,
        [DetectedFormat] nvarchar(16) NOT NULL,
        [OriginalFileName] nvarchar(200) NULL,
        [ClaimedContentType] nvarchar(100) NULL,
        [UploadedByUserId] int NOT NULL,
        [UploadedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084707_AddProductsAndMedia'
)
BEGIN
    CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084707_AddProductsAndMedia'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084707_AddProductsAndMedia'
)
BEGIN
    CREATE INDEX [IX_Products_CreatedAtUtc] ON [Products] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084707_AddProductsAndMedia'
)
BEGIN
    CREATE INDEX [IX_Products_IsPublished_IsDeleted_PublishedAtUtc] ON [Products] ([IsPublished], [IsDeleted], [PublishedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084707_AddProductsAndMedia'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010084707_AddProductsAndMedia', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE TABLE [SupportTickets] (
        [Id] uniqueidentifier NOT NULL,
        [Number] bigint NOT NULL IDENTITY,
        [CustomerUserId] int NOT NULL,
        [Subject] nvarchar(150) NOT NULL,
        [Type] int NOT NULL,
        [RelatedProductId] int NULL,
        [Status] int NOT NULL,
        [AssignedOperatorUserId] int NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [LastActivityAtUtc] datetimeoffset NOT NULL,
        [FirstResponseAtUtc] datetimeoffset NULL,
        [ClosedAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SupportTickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupportTickets_AspNetUsers_AssignedOperatorUserId] FOREIGN KEY ([AssignedOperatorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportTickets_AspNetUsers_CustomerUserId] FOREIGN KEY ([CustomerUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportTickets_Products_RelatedProductId] FOREIGN KEY ([RelatedProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE TABLE [TicketEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [TicketId] uniqueidentifier NULL,
        [ActorUserId] int NULL,
        [Kind] nvarchar(80) NOT NULL,
        [Detail] nvarchar(500) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TicketEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE TABLE [TicketInternalNotes] (
        [Id] bigint NOT NULL IDENTITY,
        [TicketId] uniqueidentifier NOT NULL,
        [AuthorUserId] int NOT NULL,
        [Body] nvarchar(2000) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TicketInternalNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketInternalNotes_AspNetUsers_AuthorUserId] FOREIGN KEY ([AuthorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TicketInternalNotes_SupportTickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [SupportTickets] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE TABLE [TicketMessages] (
        [Id] bigint NOT NULL IDENTITY,
        [TicketId] uniqueidentifier NOT NULL,
        [AuthorUserId] int NOT NULL,
        [IsOperator] bit NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TicketMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketMessages_AspNetUsers_AuthorUserId] FOREIGN KEY ([AuthorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TicketMessages_SupportTickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [SupportTickets] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTickets_AssignedOperatorUserId_Status] ON [SupportTickets] ([AssignedOperatorUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTickets_CreatedAtUtc] ON [SupportTickets] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTickets_CustomerUserId_LastActivityAtUtc] ON [SupportTickets] ([CustomerUserId], [LastActivityAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SupportTickets_Number] ON [SupportTickets] ([Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTickets_RelatedProductId] ON [SupportTickets] ([RelatedProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTickets_Status_LastActivityAtUtc] ON [SupportTickets] ([Status], [LastActivityAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTickets_Subject] ON [SupportTickets] ([Subject]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_TicketEvents_CreatedAtUtc] ON [TicketEvents] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_TicketEvents_TicketId_CreatedAtUtc] ON [TicketEvents] ([TicketId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_TicketInternalNotes_AuthorUserId] ON [TicketInternalNotes] ([AuthorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_TicketInternalNotes_TicketId_CreatedAtUtc] ON [TicketInternalNotes] ([TicketId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_TicketMessages_AuthorUserId] ON [TicketMessages] ([AuthorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    CREATE INDEX [IX_TicketMessages_TicketId_CreatedAtUtc] ON [TicketMessages] ([TicketId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013201424_TicketsSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013201424_TicketsSchema', N'10.0.12');
END;

COMMIT;
GO

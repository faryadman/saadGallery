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


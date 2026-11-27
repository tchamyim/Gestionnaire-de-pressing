using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Pressings.Models;

namespace Pressings.Services;

public sealed class AccessRepository
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly bool _useSqlServer;
    private readonly bool _useMySql;

    public AccessRepository(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"] ?? "Access";
        _useSqlServer = string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase);
        _useMySql = string.Equals(provider, "MySql", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(provider, "MariaDB", StringComparison.OrdinalIgnoreCase);
        if (_useMySql)
        {
            _databasePath = "MySQL/MariaDB";
            var connectionString = configuration.GetConnectionString("PressingDb") ??
                                  configuration["MYSQL_URL"] ?? configuration["MYSQL_PUBLIC_URL"] ?? "";
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("MySQL est sélectionné, mais ConnectionStrings:PressingDb ou MYSQL_URL n’est pas configurée.");
            _connectionString = NormalizeMySqlConnectionString(connectionString);
            return;
        }
        if (_useSqlServer)
        {
            _databasePath = "Azure SQL";
            _connectionString = configuration.GetConnectionString("PressingDb") ?? "";
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("La base SQL est sélectionnée, mais ConnectionStrings:PressingDb n’est pas configurée.");
            return;
        }

        var configuredPath = configuration["DatabasePath"];
        _databasePath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "Gestion pressing.accdb")
            : configuredPath);
        _connectionString = $"Driver={{Microsoft Access Driver (*.mdb, *.accdb)}};DBQ={_databasePath};";
    }

    public string DatabasePath => _databasePath;
    public bool UsesSqlServer => _useSqlServer;
    public bool UsesMySql => _useMySql;

    public void Initialize()
    {
        if (_useMySql)
        {
            InitializeMySql();
            return;
        }
        if (_useSqlServer)
        {
            InitializeSqlServer();
            return;
        }

        EnsureDatabaseFile();
        using var connection = OpenAccess();
        var tables = new[]
        {
            "CREATE TABLE [AdminUsers] ([AdminUserID] COUNTER PRIMARY KEY, [Email] TEXT(255) NOT NULL, [PasswordHash] TEXT(255) NOT NULL, [CreatedAt] DATETIME NOT NULL)",
            "CREATE TABLE [Clients] ([ClientID] COUNTER PRIMARY KEY, [LastName] TEXT(100) NOT NULL, [FirstName] TEXT(100) NOT NULL, [Phone] TEXT(30) NOT NULL, [WhatsApp] TEXT(30), [Email] TEXT(255), [PortalPinHash] TEXT(255), [CreatedAt] DATETIME NOT NULL, [IsActive] YESNO NOT NULL)",
            "CREATE TABLE [TariffCategories] ([CategoryID] COUNTER PRIMARY KEY, [CategoryName] TEXT(100) NOT NULL, [BasePrice] CURRENCY NOT NULL, [ExpressPercent] DOUBLE NOT NULL, [IsActive] YESNO NOT NULL, [SortOrder] INTEGER NOT NULL)",
            "CREATE TABLE [Orders] ([OrderID] COUNTER PRIMARY KEY, [TicketNo] TEXT(40) NOT NULL, [ClientID] LONG NOT NULL, [CreatedAt] DATETIME NOT NULL, [OrderStatus] TEXT(30) NOT NULL, [Notes] TEXT(255))",
            "CREATE TABLE [OrderItems] ([ItemID] COUNTER PRIMARY KEY, [OrderID] LONG NOT NULL, [CategoryID] LONG NOT NULL, [Description] TEXT(150), [UnitPrice] CURRENCY NOT NULL, [ExpressFee] CURRENCY NOT NULL, [IsExpress] YESNO NOT NULL, [DepositAt] DATETIME NOT NULL, [ExpectedPickupAt] DATETIME, [WithdrawnAt] DATETIME, [ItemStatus] TEXT(30) NOT NULL, [Notes] TEXT(255))",
            "CREATE TABLE [Payments] ([PaymentID] COUNTER PRIMARY KEY, [OrderID] LONG NOT NULL, [ClientID] LONG NOT NULL, [Amount] CURRENCY NOT NULL, [PaymentAt] DATETIME NOT NULL, [PaymentMethod] TEXT(30) NOT NULL, [Reference] TEXT(100), [ReceiptNo] TEXT(40) NOT NULL, [RecordedBy] LONG)",
            "CREATE TABLE [CashMovements] ([CashMovementID] COUNTER PRIMARY KEY, [PaymentID] LONG, [Direction] TEXT(10) NOT NULL, [Amount] CURRENCY NOT NULL, [Description] TEXT(150) NOT NULL, [CreatedAt] DATETIME NOT NULL, [RecordedBy] LONG)",
            "CREATE TABLE [ActivityLog] ([ActivityID] COUNTER PRIMARY KEY, [ClientID] LONG, [OrderID] LONG, [ItemID] LONG, [AdminUserID] LONG, [EventType] TEXT(40) NOT NULL, [Details] TEXT(255), [EventAt] DATETIME NOT NULL)",
            "CREATE TABLE [CommunicationMessages] ([CommunicationMessageID] COUNTER PRIMARY KEY, [ClientID] LONG, [OrderID] LONG, [AdminUserID] LONG, [Channel] TEXT(20) NOT NULL, [Recipient] TEXT(255) NOT NULL, [Subject] TEXT(180), [MessageBody] MEMO, [Status] TEXT(20) NOT NULL, [ErrorDetails] MEMO, [CreatedAt] DATETIME NOT NULL, [SentAt] DATETIME)",
            "CREATE TABLE [ClientMessages] ([MessageID] COUNTER PRIMARY KEY, [ClientID] LONG NOT NULL, [OrderID] LONG, [SenderRole] TEXT(10) NOT NULL, [SenderUserID] LONG, [Subject] TEXT(160) NOT NULL, [Body] MEMO NOT NULL, [SentAt] DATETIME NOT NULL, [IsRead] YESNO NOT NULL)",
            "CREATE TABLE [InventoryItems] ([InventoryItemID] COUNTER PRIMARY KEY, [Name] TEXT(100) NOT NULL, [Unit] TEXT(24) NOT NULL, [QuantityOnHand] DOUBLE NOT NULL, [ReorderAt] DOUBLE NOT NULL, [UnitCost] CURRENCY NOT NULL, [UpdatedAt] DATETIME NOT NULL, [IsActive] YESNO NOT NULL)",
            "CREATE TABLE [InventoryTransactions] ([InventoryTransactionID] COUNTER PRIMARY KEY, [InventoryItemID] LONG NOT NULL, [Direction] TEXT(10) NOT NULL, [Quantity] DOUBLE NOT NULL, [UnitCost] CURRENCY, [Reason] TEXT(150) NOT NULL, [AdminUserID] LONG, [CreatedAt] DATETIME NOT NULL)",
            "CREATE TABLE [AppSettings] ([SettingKey] TEXT(100) NOT NULL, [SettingValue] TEXT(255))"
        };

        foreach (var sql in tables)
            EnsureTable(connection, sql, TableName(sql));

        EnsureIndex(connection, "UX_AdminUsers_Email", "CREATE UNIQUE INDEX [UX_AdminUsers_Email] ON [AdminUsers] ([Email])");
        EnsureIndex(connection, "UX_Orders_TicketNo", "CREATE UNIQUE INDEX [UX_Orders_TicketNo] ON [Orders] ([TicketNo])");
        EnsureIndex(connection, "UX_AppSettings_Key", "CREATE UNIQUE INDEX [UX_AppSettings_Key] ON [AppSettings] ([SettingKey])");
        EnsureIndex(connection, "IX_ClientMessages_Client_Sent", "CREATE INDEX [IX_ClientMessages_Client_Sent] ON [ClientMessages] ([ClientID], [SentAt], [MessageID])");
        EnsureIndex(connection, "IX_ClientMessages_Unread", "CREATE INDEX [IX_ClientMessages_Unread] ON [ClientMessages] ([ClientID], [SenderRole], [IsRead])");

        TryCreateRelation(connection, "ALTER TABLE [Orders] ADD CONSTRAINT [FK_Orders_Clients] FOREIGN KEY ([ClientID]) REFERENCES [Clients] ([ClientID])");
        TryCreateRelation(connection, "ALTER TABLE [OrderItems] ADD CONSTRAINT [FK_Items_Orders] FOREIGN KEY ([OrderID]) REFERENCES [Orders] ([OrderID])");
        TryCreateRelation(connection, "ALTER TABLE [OrderItems] ADD CONSTRAINT [FK_Items_Tariffs] FOREIGN KEY ([CategoryID]) REFERENCES [TariffCategories] ([CategoryID])");
        TryCreateRelation(connection, "ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_Orders] FOREIGN KEY ([OrderID]) REFERENCES [Orders] ([OrderID])");
        TryCreateRelation(connection, "ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_Clients] FOREIGN KEY ([ClientID]) REFERENCES [Clients] ([ClientID])");

        if (Scalar<int>(connection, null, "SELECT COUNT(*) FROM [TariffCategories]") == 0)
        {
            var starterPrices = new (string Name, decimal Price, int Sort)[]
            {
                ("Chemise", 1000, 1), ("Pantalon", 1500, 2), ("Veste", 2500, 3),
                ("Costume 2 pièces", 4000, 4), ("Robe simple", 2500, 5),
                ("Robe de soirée", 5000, 6), ("Jupe", 1500, 7), ("Manteau", 4000, 8),
                ("Drap simple", 1500, 9), ("Drap double", 2500, 10),
                ("Couette", 5000, 11), ("Rideaux (paire)", 3500, 12)
            };
            foreach (var item in starterPrices)
                Execute(connection, null,
                    "INSERT INTO [TariffCategories] ([CategoryName], [BasePrice], [ExpressPercent], [IsActive], [SortOrder]) VALUES (?, ?, ?, ?, ?)",
                    item.Name, item.Price, 30, true, item.Sort);
        }

        EnsureColumn(connection, "ALTER TABLE [Orders] ADD COLUMN [DiscountAmount] CURRENCY");
        EnsureColumn(connection, "ALTER TABLE [OrderItems] ADD COLUMN [ConditionNotes] TEXT(255)");
        EnsureColumn(connection, "ALTER TABLE [OrderItems] ADD COLUMN [StorageLocation] TEXT(60)");

        SetDefaultSetting(connection, "Brand", "Fresh & Care");
        SetDefaultSetting(connection, "Currency", "FCFA");
    }

    private void InitializeSqlServer()
    {
        using var connection = Open();
        var schema = new[]
        {
            "IF OBJECT_ID(N'dbo.AdminUsers', N'U') IS NULL CREATE TABLE [dbo].[AdminUsers] ([AdminUserID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [Email] NVARCHAR(255) NOT NULL, [PasswordHash] NVARCHAR(255) NOT NULL, [CreatedAt] DATETIME2 NOT NULL)",
            "IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL CREATE TABLE [dbo].[Clients] ([ClientID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [LastName] NVARCHAR(100) NOT NULL, [FirstName] NVARCHAR(100) NOT NULL, [Phone] NVARCHAR(30) NOT NULL, [WhatsApp] NVARCHAR(30) NULL, [Email] NVARCHAR(255) NULL, [PortalPinHash] NVARCHAR(255) NULL, [CreatedAt] DATETIME2 NOT NULL, [IsActive] BIT NOT NULL)",
            "IF OBJECT_ID(N'dbo.TariffCategories', N'U') IS NULL CREATE TABLE [dbo].[TariffCategories] ([CategoryID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [CategoryName] NVARCHAR(100) NOT NULL, [BasePrice] DECIMAL(19,4) NOT NULL, [ExpressPercent] DECIMAL(9,3) NOT NULL, [IsActive] BIT NOT NULL, [SortOrder] INT NOT NULL)",
            "IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL CREATE TABLE [dbo].[Orders] ([OrderID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [TicketNo] NVARCHAR(40) NOT NULL, [ClientID] INT NOT NULL CONSTRAINT [FK_Orders_Clients] REFERENCES [dbo].[Clients]([ClientID]), [CreatedAt] DATETIME2 NOT NULL, [OrderStatus] NVARCHAR(30) NOT NULL, [Notes] NVARCHAR(255) NULL)",
            "IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL CREATE TABLE [dbo].[OrderItems] ([ItemID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [OrderID] INT NOT NULL CONSTRAINT [FK_Items_Orders] REFERENCES [dbo].[Orders]([OrderID]), [CategoryID] INT NOT NULL CONSTRAINT [FK_Items_Tariffs] REFERENCES [dbo].[TariffCategories]([CategoryID]), [Description] NVARCHAR(150) NULL, [UnitPrice] DECIMAL(19,4) NOT NULL, [ExpressFee] DECIMAL(19,4) NOT NULL, [IsExpress] BIT NOT NULL, [DepositAt] DATETIME2 NOT NULL, [ExpectedPickupAt] DATETIME2 NULL, [WithdrawnAt] DATETIME2 NULL, [ItemStatus] NVARCHAR(30) NOT NULL, [Notes] NVARCHAR(255) NULL)",
            "IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL CREATE TABLE [dbo].[Payments] ([PaymentID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [OrderID] INT NOT NULL CONSTRAINT [FK_Payments_Orders] REFERENCES [dbo].[Orders]([OrderID]), [ClientID] INT NOT NULL CONSTRAINT [FK_Payments_Clients] REFERENCES [dbo].[Clients]([ClientID]), [Amount] DECIMAL(19,4) NOT NULL, [PaymentAt] DATETIME2 NOT NULL, [PaymentMethod] NVARCHAR(30) NOT NULL, [Reference] NVARCHAR(100) NULL, [ReceiptNo] NVARCHAR(40) NOT NULL, [RecordedBy] INT NULL)",
            "IF OBJECT_ID(N'dbo.CashMovements', N'U') IS NULL CREATE TABLE [dbo].[CashMovements] ([CashMovementID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [PaymentID] INT NULL, [Direction] NVARCHAR(10) NOT NULL, [Amount] DECIMAL(19,4) NOT NULL, [Description] NVARCHAR(150) NOT NULL, [CreatedAt] DATETIME2 NOT NULL, [RecordedBy] INT NULL)",
            "IF OBJECT_ID(N'dbo.ActivityLog', N'U') IS NULL CREATE TABLE [dbo].[ActivityLog] ([ActivityID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [ClientID] INT NULL, [OrderID] INT NULL, [ItemID] INT NULL, [AdminUserID] INT NULL, [EventType] NVARCHAR(40) NOT NULL, [Details] NVARCHAR(255) NULL, [EventAt] DATETIME2 NOT NULL)",
            "IF OBJECT_ID(N'dbo.CommunicationMessages', N'U') IS NULL CREATE TABLE [dbo].[CommunicationMessages] ([CommunicationMessageID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [ClientID] INT NULL, [OrderID] INT NULL, [AdminUserID] INT NULL, [Channel] NVARCHAR(20) NOT NULL, [Recipient] NVARCHAR(255) NOT NULL, [Subject] NVARCHAR(180) NULL, [MessageBody] NVARCHAR(MAX) NULL, [Status] NVARCHAR(20) NOT NULL, [ErrorDetails] NVARCHAR(MAX) NULL, [CreatedAt] DATETIME2 NOT NULL, [SentAt] DATETIME2 NULL)",
            "IF OBJECT_ID(N'dbo.ClientMessages', N'U') IS NULL CREATE TABLE [dbo].[ClientMessages] ([MessageID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [ClientID] INT NOT NULL, [OrderID] INT NULL, [SenderRole] NVARCHAR(10) NOT NULL, [SenderUserID] INT NULL, [Subject] NVARCHAR(160) NOT NULL, [Body] NVARCHAR(MAX) NOT NULL, [SentAt] DATETIME2 NOT NULL, [IsRead] BIT NOT NULL)",
            "IF OBJECT_ID(N'dbo.InventoryItems', N'U') IS NULL CREATE TABLE [dbo].[InventoryItems] ([InventoryItemID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [Name] NVARCHAR(100) NOT NULL, [Unit] NVARCHAR(24) NOT NULL, [QuantityOnHand] DECIMAL(18,3) NOT NULL, [ReorderAt] DECIMAL(18,3) NOT NULL, [UnitCost] DECIMAL(19,4) NOT NULL, [UpdatedAt] DATETIME2 NOT NULL, [IsActive] BIT NOT NULL)",
            "IF OBJECT_ID(N'dbo.InventoryTransactions', N'U') IS NULL CREATE TABLE [dbo].[InventoryTransactions] ([InventoryTransactionID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [InventoryItemID] INT NOT NULL, [Direction] NVARCHAR(10) NOT NULL, [Quantity] DECIMAL(18,3) NOT NULL, [UnitCost] DECIMAL(19,4) NULL, [Reason] NVARCHAR(150) NOT NULL, [AdminUserID] INT NULL, [CreatedAt] DATETIME2 NOT NULL)",
            "IF OBJECT_ID(N'dbo.AppSettings', N'U') IS NULL CREATE TABLE [dbo].[AppSettings] ([SettingKey] NVARCHAR(100) NOT NULL PRIMARY KEY, [SettingValue] NVARCHAR(255) NULL)",
            "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name]=N'UX_AdminUsers_Email' AND [object_id]=OBJECT_ID(N'dbo.AdminUsers')) CREATE UNIQUE INDEX [UX_AdminUsers_Email] ON [dbo].[AdminUsers] ([Email])",
            "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name]=N'UX_Orders_TicketNo' AND [object_id]=OBJECT_ID(N'dbo.Orders')) CREATE UNIQUE INDEX [UX_Orders_TicketNo] ON [dbo].[Orders] ([TicketNo])",
            "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name]=N'IX_ClientMessages_Client_Sent' AND [object_id]=OBJECT_ID(N'dbo.ClientMessages')) CREATE INDEX [IX_ClientMessages_Client_Sent] ON [dbo].[ClientMessages] ([ClientID], [SentAt], [MessageID])",
            "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name]=N'IX_ClientMessages_Unread' AND [object_id]=OBJECT_ID(N'dbo.ClientMessages')) CREATE INDEX [IX_ClientMessages_Unread] ON [dbo].[ClientMessages] ([ClientID], [SenderRole], [IsRead])"
        };
        foreach (var statement in schema) Execute(connection, null, statement);

        if (Scalar<int>(connection, null, "SELECT COUNT(*) FROM [TariffCategories]") == 0)
        {
            var starterPrices = new (string Name, decimal Price, int Sort)[]
            {
                ("Chemise", 1000, 1), ("Pantalon", 1500, 2), ("Veste", 2500, 3),
                ("Costume 2 pièces", 4000, 4), ("Robe simple", 2500, 5), ("Robe de soirée", 5000, 6),
                ("Jupe", 1500, 7), ("Manteau", 4000, 8), ("Drap simple", 1500, 9),
                ("Drap double", 2500, 10), ("Couette", 5000, 11), ("Rideaux (paire)", 3500, 12)
            };
            foreach (var item in starterPrices)
                Execute(connection, null,
                    "INSERT INTO [TariffCategories] ([CategoryName], [BasePrice], [ExpressPercent], [IsActive], [SortOrder]) VALUES (?, ?, ?, ?, ?)",
                    item.Name, item.Price, 30, true, item.Sort);
        }

        EnsureColumn(connection, "IF COL_LENGTH(N'dbo.Orders', N'DiscountAmount') IS NULL ALTER TABLE [dbo].[Orders] ADD [DiscountAmount] DECIMAL(19,4) NULL");
        EnsureColumn(connection, "IF COL_LENGTH(N'dbo.OrderItems', N'ConditionNotes') IS NULL ALTER TABLE [dbo].[OrderItems] ADD [ConditionNotes] NVARCHAR(255) NULL");
        EnsureColumn(connection, "IF COL_LENGTH(N'dbo.OrderItems', N'StorageLocation') IS NULL ALTER TABLE [dbo].[OrderItems] ADD [StorageLocation] NVARCHAR(60) NULL");

        SetDefaultSetting(connection, "Brand", "Fresh & Care");
        SetDefaultSetting(connection, "Currency", "FCFA");
    }

    private void InitializeMySql()
    {
        using var connection = Open();
        var schema = new[]
        {
            "CREATE TABLE IF NOT EXISTS [AdminUsers] ([AdminUserID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [Email] VARCHAR(255) NOT NULL, [PasswordHash] VARCHAR(255) NOT NULL, [CreatedAt] DATETIME(6) NOT NULL, UNIQUE KEY [UX_AdminUsers_Email] ([Email])) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [Clients] ([ClientID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [LastName] VARCHAR(100) NOT NULL, [FirstName] VARCHAR(100) NOT NULL, [Phone] VARCHAR(30) NOT NULL, [WhatsApp] VARCHAR(30) NULL, [Email] VARCHAR(255) NULL, [PortalPinHash] VARCHAR(255) NULL, [CreatedAt] DATETIME(6) NOT NULL, [IsActive] TINYINT(1) NOT NULL, KEY [IX_Clients_Phone] ([Phone])) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [TariffCategories] ([CategoryID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [CategoryName] VARCHAR(100) NOT NULL, [BasePrice] DECIMAL(19,4) NOT NULL, [ExpressPercent] DOUBLE NOT NULL, [IsActive] TINYINT(1) NOT NULL, [SortOrder] INT NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [Orders] ([OrderID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [TicketNo] VARCHAR(40) NOT NULL, [ClientID] INT NOT NULL, [CreatedAt] DATETIME(6) NOT NULL, [OrderStatus] VARCHAR(30) NOT NULL, [Notes] VARCHAR(255) NULL, UNIQUE KEY [UX_Orders_TicketNo] ([TicketNo]), KEY [IX_Orders_ClientID] ([ClientID]), CONSTRAINT [FK_Orders_Clients] FOREIGN KEY ([ClientID]) REFERENCES [Clients]([ClientID])) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [OrderItems] ([ItemID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [OrderID] INT NOT NULL, [CategoryID] INT NOT NULL, [Description] VARCHAR(150) NULL, [UnitPrice] DECIMAL(19,4) NOT NULL, [ExpressFee] DECIMAL(19,4) NOT NULL, [IsExpress] TINYINT(1) NOT NULL, [DepositAt] DATETIME(6) NOT NULL, [ExpectedPickupAt] DATETIME(6) NULL, [WithdrawnAt] DATETIME(6) NULL, [ItemStatus] VARCHAR(30) NOT NULL, [Notes] VARCHAR(255) NULL, KEY [IX_OrderItems_OrderID] ([OrderID]), CONSTRAINT [FK_Items_Orders] FOREIGN KEY ([OrderID]) REFERENCES [Orders]([OrderID]), CONSTRAINT [FK_Items_Tariffs] FOREIGN KEY ([CategoryID]) REFERENCES [TariffCategories]([CategoryID])) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [Payments] ([PaymentID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [OrderID] INT NOT NULL, [ClientID] INT NOT NULL, [Amount] DECIMAL(19,4) NOT NULL, [PaymentAt] DATETIME(6) NOT NULL, [PaymentMethod] VARCHAR(30) NOT NULL, [Reference] VARCHAR(100) NULL, [ReceiptNo] VARCHAR(40) NOT NULL, [RecordedBy] INT NULL, KEY [IX_Payments_OrderID] ([OrderID]), CONSTRAINT [FK_Payments_Orders] FOREIGN KEY ([OrderID]) REFERENCES [Orders]([OrderID]), CONSTRAINT [FK_Payments_Clients] FOREIGN KEY ([ClientID]) REFERENCES [Clients]([ClientID])) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [CashMovements] ([CashMovementID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [PaymentID] INT NULL, [Direction] VARCHAR(10) NOT NULL, [Amount] DECIMAL(19,4) NOT NULL, [Description] VARCHAR(150) NOT NULL, [CreatedAt] DATETIME(6) NOT NULL, [RecordedBy] INT NULL, CONSTRAINT [FK_CashMovements_Payments] FOREIGN KEY ([PaymentID]) REFERENCES [Payments]([PaymentID])) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [ActivityLog] ([ActivityID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [ClientID] INT NULL, [OrderID] INT NULL, [ItemID] INT NULL, [AdminUserID] INT NULL, [EventType] VARCHAR(40) NOT NULL, [Details] VARCHAR(255) NULL, [EventAt] DATETIME(6) NOT NULL, KEY [IX_ActivityLog_ClientID] ([ClientID])) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [CommunicationMessages] ([CommunicationMessageID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [ClientID] INT NULL, [OrderID] INT NULL, [AdminUserID] INT NULL, [Channel] VARCHAR(20) NOT NULL, [Recipient] VARCHAR(255) NOT NULL, [Subject] VARCHAR(180) NULL, [MessageBody] TEXT NULL, [Status] VARCHAR(20) NOT NULL, [ErrorDetails] TEXT NULL, [CreatedAt] DATETIME(6) NOT NULL, [SentAt] DATETIME(6) NULL, KEY [IX_CommunicationMessages_CreatedAt] ([CreatedAt])) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [ClientMessages] ([MessageID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [ClientID] INT NOT NULL, [OrderID] INT NULL, [SenderRole] VARCHAR(10) NOT NULL, [SenderUserID] INT NULL, [Subject] VARCHAR(160) NOT NULL, [Body] TEXT NOT NULL, [SentAt] DATETIME(6) NOT NULL, [IsRead] TINYINT(1) NOT NULL, KEY [IX_ClientMessages_Client_Sent] ([ClientID], [SentAt], [MessageID]), KEY [IX_ClientMessages_Unread] ([ClientID], [SenderRole], [IsRead])) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [InventoryItems] ([InventoryItemID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [Name] VARCHAR(100) NOT NULL, [Unit] VARCHAR(24) NOT NULL, [QuantityOnHand] DECIMAL(18,3) NOT NULL, [ReorderAt] DECIMAL(18,3) NOT NULL, [UnitCost] DECIMAL(19,4) NOT NULL, [UpdatedAt] DATETIME(6) NOT NULL, [IsActive] TINYINT(1) NOT NULL, KEY [IX_InventoryItems_IsActive] ([IsActive])) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [InventoryTransactions] ([InventoryTransactionID] INT NOT NULL AUTO_INCREMENT PRIMARY KEY, [InventoryItemID] INT NOT NULL, [Direction] VARCHAR(10) NOT NULL, [Quantity] DECIMAL(18,3) NOT NULL, [UnitCost] DECIMAL(19,4) NULL, [Reason] VARCHAR(150) NOT NULL, [AdminUserID] INT NULL, [CreatedAt] DATETIME(6) NOT NULL, KEY [IX_InventoryTransactions_Item_Created] ([InventoryItemID], [CreatedAt]), CONSTRAINT [FK_InventoryTransactions_Items] FOREIGN KEY ([InventoryItemID]) REFERENCES [InventoryItems]([InventoryItemID])) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            "CREATE TABLE IF NOT EXISTS [AppSettings] ([SettingKey] VARCHAR(100) NOT NULL PRIMARY KEY, [SettingValue] VARCHAR(255) NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
        };
        foreach (var statement in schema) Execute(connection, null, statement);
        EnsureMySqlIndex(connection, "ClientMessages", "IX_ClientMessages_Client_Sent",
            "CREATE INDEX `IX_ClientMessages_Client_Sent` ON `ClientMessages` (`ClientID`, `SentAt`, `MessageID`)");
        EnsureMySqlIndex(connection, "ClientMessages", "IX_ClientMessages_Unread",
            "CREATE INDEX `IX_ClientMessages_Unread` ON `ClientMessages` (`ClientID`, `SenderRole`, `IsRead`)");

        if (Scalar<int>(connection, null, "SELECT COUNT(*) FROM [TariffCategories]") == 0)
        {
            var starterPrices = new (string Name, decimal Price, int Sort)[]
            {
                ("Chemise", 1000, 1), ("Pantalon", 1500, 2), ("Veste", 2500, 3),
                ("Costume 2 pièces", 4000, 4), ("Robe simple", 2500, 5), ("Robe de soirée", 5000, 6),
                ("Jupe", 1500, 7), ("Manteau", 4000, 8), ("Drap simple", 1500, 9),
                ("Drap double", 2500, 10), ("Couette", 5000, 11), ("Rideaux (paire)", 3500, 12)
            };
            foreach (var item in starterPrices)
                Execute(connection, null,
                    "INSERT INTO [TariffCategories] ([CategoryName], [BasePrice], [ExpressPercent], [IsActive], [SortOrder]) VALUES (?, ?, ?, ?, ?)",
                    item.Name, item.Price, 30, true, item.Sort);
        }

        EnsureColumn(connection, "ALTER TABLE `Orders` ADD COLUMN `DiscountAmount` DECIMAL(19,4) NULL");
        EnsureColumn(connection, "ALTER TABLE `OrderItems` ADD COLUMN `ConditionNotes` VARCHAR(255) NULL");
        EnsureColumn(connection, "ALTER TABLE `OrderItems` ADD COLUMN `StorageLocation` VARCHAR(60) NULL");

        SetDefaultSetting(connection, "Brand", "Fresh & Care");
        SetDefaultSetting(connection, "Currency", "FCFA");
    }

    private static void EnsureMySqlIndex(DbConnection connection, string tableName, string indexName, string createSql)
    {
        var exists = Scalar<int>(connection, null,
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=? AND INDEX_NAME=?",
            tableName, indexName);
        if (exists == 0) Execute(connection, null, createSql);
    }

    private static string NormalizeMySqlConnectionString(string configured)
    {
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "mysql" && uri.Scheme != "mariadb")) return configured;

        var separator = uri.UserInfo.IndexOf(':');
        var user = separator < 0 ? Uri.UnescapeDataString(uri.UserInfo) : Uri.UnescapeDataString(uri.UserInfo[..separator]);
        var password = separator < 0 ? "" : Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]);
        var builder = new MySqlConnectionStringBuilder
        {
            Server = uri.Host,
            Port = uri.IsDefaultPort || uri.Port < 1 ? 3306U : checked((uint)uri.Port),
            UserID = user,
            Password = password,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))
        };
        return builder.ConnectionString;
    }

    private void EnsureDatabaseFile()
    {
        if (File.Exists(_databasePath)) return;
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Le fournisseur Access nécessite Windows et Microsoft Access. Configurez MySQL ou SQL Server pour l’hébergement.");
        var accessType = Type.GetTypeFromProgID("Access.Application");
        if (accessType is null)
            throw new InvalidOperationException("Microsoft Access n’est pas installé. Installe Access ou crée le fichier .accdb avant de lancer l’application.");

        object? access = null;
        try
        {
            access = Activator.CreateInstance(accessType);
            dynamic app = access!;
            app.Visible = false;
            app.NewCurrentDatabase(_databasePath);
            app.CloseCurrentDatabase();
            app.Quit();
        }
        finally
        {
            if (access is not null && Marshal.IsComObject(access)) Marshal.FinalReleaseComObject(access);
        }
    }

    private static string TableName(string createSql)
    {
        var start = createSql.IndexOf('[', StringComparison.Ordinal) + 1;
        var end = createSql.IndexOf(']', start);
        return createSql[start..end];
    }

    private static void EnsureTable(OdbcConnection connection, string createSql, string name)
    {
        try { using var cmd = new OdbcCommand($"SELECT TOP 1 * FROM [{name}]", connection); cmd.ExecuteReader().Close(); }
        catch (OdbcException) { using var cmd = new OdbcCommand(createSql, connection); cmd.ExecuteNonQuery(); }
    }

    private static void EnsureColumn(DbConnection connection, string alterSql)
    {
        try { using var cmd = connection.CreateCommand(); cmd.CommandText = alterSql; cmd.ExecuteNonQuery(); }
        catch { /* Column already exists */ }
    }

    private static void EnsureIndex(OdbcConnection connection, string name, string createSql)
    {
        try
        {
            var schema = connection.GetSchema("Indexes");
            if (schema.Rows.Cast<DataRow>().Any(r => string.Equals(Convert.ToString(r["INDEX_NAME"]), name, StringComparison.OrdinalIgnoreCase))) return;
        }
        catch { /* The Access ODBC driver may not expose index metadata; try the DDL below. */ }
        try { using var cmd = new OdbcCommand(createSql, connection); cmd.ExecuteNonQuery(); }
        catch (OdbcException ex) when (
            ex.Message.Contains("déjà", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("existe", StringComparison.OrdinalIgnoreCase)) { }
    }

    private static void TryCreateRelation(OdbcConnection connection, string sql)
    {
        try { using var cmd = new OdbcCommand(sql, connection); cmd.ExecuteNonQuery(); }
        catch (OdbcException ex) when (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("existe", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("constraint", StringComparison.OrdinalIgnoreCase)) { }
    }

    private static void SetDefaultSetting(OdbcConnection connection, string key, string value)
    {
        if (Scalar<int>(connection, null, "SELECT COUNT(*) FROM [AppSettings] WHERE [SettingKey]=?", key) == 0)
            Execute(connection, null, "INSERT INTO [AppSettings] ([SettingKey], [SettingValue]) VALUES (?, ?)", key, value);
    }

    private static void SetDefaultSetting(DbConnection connection, string key, string value)
    {
        if (Scalar<int>(connection, null, "SELECT COUNT(*) FROM [AppSettings] WHERE [SettingKey]=?", key) == 0)
            Execute(connection, null, "INSERT INTO [AppSettings] ([SettingKey], [SettingValue]) VALUES (?, ?)", key, value);
    }

    private OdbcConnection OpenAccess()
    {
        var connection = new OdbcConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private DbConnection Open()
    {
        if (_useMySql)
        {
            var mysql = new MySqlConnection(_connectionString);
            mysql.Open();
            return mysql;
        }
        if (!_useSqlServer) return OpenAccess();
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static DbCommand Command(DbConnection connection, DbTransaction? transaction, string sql, params object?[] args)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        if (connection is SqlConnection)
        {
            var parameterIndex = 0;
            var sqlServerText = new System.Text.StringBuilder(sql.Length + args.Length * 4);
            foreach (var ch in sql)
                sqlServerText.Append(ch == '?' ? "@p" + parameterIndex++ : ch.ToString());
            command.CommandText = sqlServerText.ToString()
                .Replace("=True", "=1", StringComparison.OrdinalIgnoreCase)
                .Replace("=False", "=0", StringComparison.OrdinalIgnoreCase);
        }
        else if (connection is MySqlConnection)
        {
            var parameterIndex = 0;
            var mySqlText = new System.Text.StringBuilder(sql.Length + args.Length * 4);
            foreach (var ch in sql)
                mySqlText.Append(ch switch
                {
                    '?' => "@p" + parameterIndex++,
                    '[' => "`",
                    ']' => "`",
                    _ => ch.ToString()
                });
            command.CommandText = mySqlText.ToString()
                .Replace("=True", "=1", StringComparison.OrdinalIgnoreCase)
                .Replace("=False", "=0", StringComparison.OrdinalIgnoreCase);
        }
        else command.CommandText = sql;

        for (var index = 0; index < args.Length; index++)
        {
            if (connection is OdbcConnection odbc)
            {
                ((OdbcCommand)command).Parameters.AddWithValue("p" + index, args[index] ?? DBNull.Value);
                continue;
            }
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p" + index;
            parameter.Value = args[index] ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private static int Execute(DbConnection connection, DbTransaction? transaction, string sql, params object?[] args)
    {
        using var command = Command(connection, transaction, sql, args);
        return command.ExecuteNonQuery();
    }

    private static T Scalar<T>(DbConnection connection, DbTransaction? transaction, string sql, params object?[] args)
    {
        using var command = Command(connection, transaction, sql, args);
        var result = command.ExecuteScalar();
        if (result is null || result is DBNull) return default!;
        return (T)Convert.ChangeType(result, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), CultureInfo.InvariantCulture);
    }

    private List<T> Query<T>(string sql, Func<DbDataReader, T> map, params object?[] args)
    {
        using var connection = Open();
        using var command = Command(connection, null, sql, args);
        using var reader = command.ExecuteReader();
        var result = new List<T>();
        while (reader.Read()) result.Add(map(reader));
        return result;
    }

    private static int InsertAndGetId(DbConnection connection, DbTransaction transaction, string sql, params object?[] args)
    {
        Execute(connection, transaction, sql, args);
        var identityQuery = connection switch
        {
            SqlConnection => "SELECT CAST(SCOPE_IDENTITY() AS INT)",
            MySqlConnection => "SELECT LAST_INSERT_ID()",
            _ => "SELECT @@IDENTITY"
        };
        return Scalar<int>(connection, transaction, identityQuery);
    }

    private static string Text(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "";
    private static decimal Money(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    private static int Number(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    private static bool Flag(DbDataReader reader, int ordinal) => !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    private static DateTime Date(DbDataReader reader, int ordinal) => Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    private static DateTime? MaybeDate(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Date(reader, ordinal);
    private static string Ticket() => $"FC-{DateTime.Now:yyMMdd}-{Random.Shared.Next(0x100000, 0xFFFFFF):X6}";
    private static string Receipt() => $"RC-{DateTime.Now:yyMMddHHmmss}-{Random.Shared.Next(100, 999)}";

    public int AdminCount() => Query("SELECT COUNT(*) FROM [AdminUsers]", r => Number(r, 0)).FirstOrDefault();

    public AdminAccount? FindAdmin(string email) => Query(
        "SELECT [AdminUserID], [Email], [PasswordHash] FROM [AdminUsers] WHERE [Email]=?",
        r => new AdminAccount { Id = Number(r, 0), Email = Text(r, 1), PasswordHash = Text(r, 2) }, email.Trim().ToLowerInvariant()).FirstOrDefault();

    public void CreateAdmin(string email, string passwordHash)
    {
        using var connection = Open();
        Execute(connection, null, "INSERT INTO [AdminUsers] ([Email], [PasswordHash], [CreatedAt]) VALUES (?, ?, ?)", email.Trim().ToLowerInvariant(), passwordHash, DateTime.Now);
    }

    public List<TariffCategory> GetTariffs(bool activeOnly = false) => Query(
        "SELECT [CategoryID], [CategoryName], [BasePrice], [ExpressPercent], [IsActive] FROM [TariffCategories] " +
        (activeOnly ? "WHERE [IsActive]=True " : "") + "ORDER BY [SortOrder], [CategoryName]",
        r => new TariffCategory { CategoryId = Number(r, 0), CategoryName = Text(r, 1), BasePrice = Money(r, 2), ExpressPercent = Money(r, 3), IsActive = Flag(r, 4) });

    public void SaveTariff(TariffInput input)
    {
        using var connection = Open();
        if (input.CategoryId == 0)
            Execute(connection, null, "INSERT INTO [TariffCategories] ([CategoryName], [BasePrice], [ExpressPercent], [IsActive], [SortOrder]) VALUES (?, ?, ?, ?, ?)", input.CategoryName.Trim(), input.BasePrice, input.ExpressPercent, input.IsActive, 999);
        else
            Execute(connection, null, "UPDATE [TariffCategories] SET [CategoryName]=?, [BasePrice]=?, [ExpressPercent]=?, [IsActive]=? WHERE [CategoryID]=?", input.CategoryName.Trim(), input.BasePrice, input.ExpressPercent, input.IsActive, input.CategoryId);
    }

    public List<ClientRecord> GetClients(string? search = null)
    {
        var clients = Query(
            "SELECT [ClientID], [FirstName], [LastName], [Phone], [WhatsApp], [Email], [CreatedAt] FROM [Clients] WHERE [IsActive]=True ORDER BY [LastName], [FirstName]",
            r => new ClientRecord { ClientId = Number(r, 0), FirstName = Text(r, 1), LastName = Text(r, 2), Phone = Text(r, 3), WhatsApp = Text(r, 4), Email = Text(r, 5), CreatedAt = Date(r, 6) });
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            clients = clients.Where(c => $"{c.FirstName} {c.LastName} {c.Phone} {c.WhatsApp} {c.Email}".Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        var balances = GetOrderSummaries(includeReadyItems: false)
            .GroupBy(order => order.ClientId)
            .ToDictionary(group => group.Key, group => group.Sum(order => order.Remaining));
        foreach (var client in clients) client.Balance = balances.GetValueOrDefault(client.ClientId);
        return clients;
    }

    public ClientRecord? GetClient(int id) => Query(
        "SELECT [ClientID], [FirstName], [LastName], [Phone], [WhatsApp], [Email], [CreatedAt] FROM [Clients] WHERE [ClientID]=? AND [IsActive]=True",
        r => new ClientRecord { ClientId = Number(r, 0), FirstName = Text(r, 1), LastName = Text(r, 2), Phone = Text(r, 3), WhatsApp = Text(r, 4), Email = Text(r, 5), CreatedAt = Date(r, 6) }, id).FirstOrDefault();

    public int CreateClient(ClientInput input, string pinHash)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var id = InsertAndGetId(connection, transaction,
            "INSERT INTO [Clients] ([LastName], [FirstName], [Phone], [WhatsApp], [Email], [PortalPinHash], [CreatedAt], [IsActive]) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
            input.LastName.Trim(), input.FirstName.Trim(), input.Phone.Trim(), input.WhatsApp?.Trim() ?? "", input.Email?.Trim() ?? "", pinHash, DateTime.Now, true);
        AddEvent(connection, transaction, id, null, null, null, "client créé", "Fiche client créée.");
        transaction.Commit();
        return id;
    }

    public void UpdateClient(int id, ClientInput input)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, "UPDATE [Clients] SET [LastName]=?, [FirstName]=?, [Phone]=?, [WhatsApp]=?, [Email]=? WHERE [ClientID]=?", input.LastName.Trim(), input.FirstName.Trim(), input.Phone.Trim(), input.WhatsApp?.Trim() ?? "", input.Email?.Trim() ?? "", id);
        AddEvent(connection, transaction, id, null, null, null, "client modifié", "Coordonnées mises à jour.");
        transaction.Commit();
    }

    public PortalAccount? FindPortalAccountByPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return Query("SELECT [ClientID], [FirstName], [LastName], [Phone], [PortalPinHash], [WhatsApp] FROM [Clients] WHERE [IsActive]=True",
            r => new PortalAccount { ClientId = Number(r, 0), FirstName = Text(r, 1), LastName = Text(r, 2), Phone = Text(r, 3), PinHash = Text(r, 4), WhatsApp = Text(r, 5) })
            .FirstOrDefault(c => new string(c.Phone.Where(char.IsDigit).ToArray()) == digits || new string(c.WhatsApp.Where(char.IsDigit).ToArray()) == digits);
    }

    public void ResetPortalPin(int clientId, string pinHash)
    {
        using var connection = Open();
        Execute(connection, null, "UPDATE [Clients] SET [PortalPinHash]=? WHERE [ClientID]=?", pinHash, clientId);
    }

    public DashboardViewModel GetDashboard()
    {
        var today = DateTime.Today;
        var orderSummaries = GetOrderSummaries();
        var monthlyPayments = Query(
            "SELECT [PaymentAt], [Amount] FROM [Payments] WHERE [PaymentAt]>=?",
            r => (At: Date(r, 0), Amount: Money(r, 1)), today.AddDays(-29));
        var vm = new DashboardViewModel
        {
            ClientCount = Query("SELECT COUNT(*) FROM [Clients] WHERE [IsActive]=True", r => Number(r, 0)).FirstOrDefault(),
            ActiveOrders = Query("SELECT COUNT(*) FROM [Orders] WHERE [OrderStatus]<>'terminée'", r => Number(r, 0)).FirstOrDefault(),
            ReadyItems = Query("SELECT COUNT(*) FROM [OrderItems] WHERE [ItemStatus]='prêt'", r => Number(r, 0)).FirstOrDefault(),
            TodayPayments = monthlyPayments.Where(payment => payment.At.Date == today).Sum(payment => payment.Amount),
            RevenueLast30Days = monthlyPayments.Sum(payment => payment.Amount),
            TillBalance = GetTillBalance(),
            RecentOrders = orderSummaries.Take(7).ToList(),
            Tariffs = GetTariffs(true),
            UnreadClientMessages = Query("SELECT COUNT(*) FROM [ClientMessages] WHERE [SenderRole]='client' AND [IsRead]=False", r => Number(r, 0)).FirstOrDefault(),
            LowStockItems = Query("SELECT COUNT(*) FROM [InventoryItems] WHERE [IsActive]=True AND [QuantityOnHand]<=[ReorderAt]", r => Number(r, 0)).FirstOrDefault(),
            OverduePickupCount = Query("SELECT COUNT(*) FROM [OrderItems] WHERE [ItemStatus]<>'retiré' AND [ExpectedPickupAt]<?", r => Number(r, 0), today).FirstOrDefault(),
            OverduePickups = GetOverduePickups(today)
        };
        vm.DailyRevenue = Enumerable.Range(0, 7).Select(offset =>
        {
            var day = today.AddDays(offset - 6);
            return new DailyRevenuePoint { Day = day, Amount = monthlyPayments.Where(payment => payment.At.Date == day).Sum(payment => payment.Amount) };
        }).ToList();
        vm.ItemStages = Query("SELECT [ItemStatus], COUNT(*) FROM [OrderItems] GROUP BY [ItemStatus]",
            r => new ItemStageCount { Status = Text(r, 0), Count = Number(r, 1) })
            .OrderBy(stage => Array.IndexOf(new[] { "déposé", "en traitement", "prêt", "retiré" }, stage.Status)).ToList();
        vm.Outstanding = orderSummaries.Sum(o => o.Remaining);
        return vm;
    }

    private List<OverduePickup> GetOverduePickups(DateTime today) => Query(
        "SELECT o.[OrderID], o.[TicketNo], c.[FirstName], c.[LastName], t.[CategoryName], i.[Description], i.[ExpectedPickupAt] " +
        "FROM (([OrderItems] AS i INNER JOIN [Orders] AS o ON i.[OrderID]=o.[OrderID]) " +
        "INNER JOIN [Clients] AS c ON o.[ClientID]=c.[ClientID]) " +
        "INNER JOIN [TariffCategories] AS t ON i.[CategoryID]=t.[CategoryID] " +
        "WHERE i.[ItemStatus]<>'retiré' AND i.[ExpectedPickupAt]<? ORDER BY i.[ExpectedPickupAt], o.[TicketNo]",
        r => new OverduePickup
        {
            OrderId = Number(r, 0),
            TicketNo = Text(r, 1),
            ClientName = (Text(r, 2) + " " + Text(r, 3)).Trim(),
            CategoryName = Text(r, 4),
            Description = Text(r, 5),
            ExpectedPickupAt = Date(r, 6)
        }, today).Take(5).ToList();

    public List<OrderSummary> GetOrderSummaries(int? clientId = null, bool includeReadyItems = true)
    {
        var sql = "SELECT o.[OrderID], o.[ClientID], o.[TicketNo], o.[CreatedAt], o.[OrderStatus], c.[FirstName], c.[LastName], o.[DiscountAmount] " +
                  "FROM [Orders] AS o INNER JOIN [Clients] AS c ON o.[ClientID]=c.[ClientID] " +
                  (clientId.HasValue ? "WHERE o.[ClientID]=? " : "") + "ORDER BY o.[CreatedAt] DESC";
        var orders = Query(sql, r => new OrderSummary
        {
            OrderId = Number(r, 0), ClientId = Number(r, 1), TicketNo = Text(r, 2), CreatedAt = Date(r, 3),
            OrderStatus = Text(r, 4), ClientName = (Text(r, 5) + " " + Text(r, 6)).Trim(), DiscountAmount = Money(r, 7)
        }, clientId.HasValue ? [clientId.Value] : []);

        if (orders.Count == 0) return orders;

        var orderScope = clientId.HasValue
            ? " WHERE [OrderID] IN (SELECT [OrderID] FROM [Orders] WHERE [ClientID]=?)"
            : "";
        object?[] aggregateArgs = clientId.HasValue ? [clientId.Value] : [];
        var itemTotals = Query("SELECT [OrderID], SUM([UnitPrice] + [ExpressFee]) FROM [OrderItems]" + orderScope + " GROUP BY [OrderID]",
            r => (OrderId: Number(r, 0), Total: Money(r, 1)), aggregateArgs).ToDictionary(row => row.OrderId, row => row.Total);
        var payments = Query("SELECT [OrderID], SUM([Amount]) FROM [Payments]" + orderScope + " GROUP BY [OrderID]",
            r => (OrderId: Number(r, 0), Amount: Money(r, 1)), aggregateArgs).ToDictionary(row => row.OrderId, row => row.Amount);
        var readyItems = new Dictionary<int, int>();
        if (includeReadyItems)
        {
            readyItems = Query("SELECT [OrderID], COUNT(*) FROM [OrderItems]" +
                               (clientId.HasValue ? " WHERE [ItemStatus]='prêt' AND [OrderID] IN (SELECT [OrderID] FROM [Orders] WHERE [ClientID]=?)" : " WHERE [ItemStatus]='prêt'") +
                               " GROUP BY [OrderID]",
                r => (OrderId: Number(r, 0), Count: Number(r, 1)), aggregateArgs).ToDictionary(row => row.OrderId, row => row.Count);
        }

        foreach (var order in orders)
        {
            order.Total = Math.Max(0, itemTotals.GetValueOrDefault(order.OrderId) - order.DiscountAmount);
            order.Paid = payments.GetValueOrDefault(order.OrderId);
            order.ReadyItems = readyItems.GetValueOrDefault(order.OrderId);
        }
        return orders;
    }

    public List<OrderSummary> SearchOrders(string? search)
    {
        var rows = GetOrderSummaries();
        if (string.IsNullOrWhiteSpace(search)) return rows;
        var term = search.Trim();
        return rows.Where(o => o.TicketNo.Contains(term, StringComparison.OrdinalIgnoreCase) || o.ClientName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public int CreateOrder(NewOrderInput input, int adminId)
    {
        if (input.Items.Count == 0) throw new InvalidOperationException("Ajoutez au moins un article au dépôt.");
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var ticket = Ticket();
        var discount = Math.Max(0, Math.Round(input.DiscountAmount, 0, MidpointRounding.AwayFromZero));
        var orderId = InsertAndGetId(connection, transaction,
            "INSERT INTO [Orders] ([TicketNo], [ClientID], [CreatedAt], [OrderStatus], [Notes], [DiscountAmount]) VALUES (?, ?, ?, ?, ?, ?)",
            ticket, input.ClientId, DateTime.Now, "ouverte", input.Notes?.Trim() ?? "", discount);
        foreach (var line in input.Items)
        {
            if (line.CategoryId <= 0) continue;
            var tariff = QueryWithConnection(connection, transaction,
                "SELECT [CategoryName], [BasePrice], [ExpressPercent] FROM [TariffCategories] WHERE [CategoryID]=? AND [IsActive]=True",
                r => new TariffCategory { CategoryName = Text(r, 0), BasePrice = Money(r, 1), ExpressPercent = Money(r, 2) }, line.CategoryId).FirstOrDefault();
            if (tariff is null) continue;
            var express = string.Equals(line.Service, "express", StringComparison.OrdinalIgnoreCase);
            var fee = express ? Math.Round(tariff.BasePrice * tariff.ExpressPercent / 100m, 0, MidpointRounding.AwayFromZero) : 0;
            DateTime? expected = DateTime.TryParse(line.ExpectedPickup, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt) ? dt : null;
            var qty = Math.Clamp(line.Quantity, 1, 100);
            for (var q = 0; q < qty; q++)
            {
                var desc = line.Description?.Trim() ?? "";
                if (qty > 1) desc = string.IsNullOrEmpty(desc) ? $"Pièce {q + 1}/{qty}" : $"{desc} ({q + 1}/{qty})";
                var itemId = InsertAndGetId(connection, transaction,
                    "INSERT INTO [OrderItems] ([OrderID], [CategoryID], [Description], [ConditionNotes], [UnitPrice], [ExpressFee], [IsExpress], [DepositAt], [ExpectedPickupAt], [ItemStatus]) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                    orderId, line.CategoryId, desc, line.ConditionNotes?.Trim() ?? "", tariff.BasePrice, fee, express, DateTime.Now, expected, "déposé");
                AddEvent(connection, transaction, input.ClientId, orderId, itemId, adminId, "article déposé", $"{tariff.CategoryName} — statut : déposé.");
            }
        }
        if (Scalar<int>(connection, transaction, "SELECT COUNT(*) FROM [OrderItems] WHERE [OrderID]=?", orderId) == 0)
            throw new InvalidOperationException("Aucun article valide n’a été ajouté au dépôt.");
        AddEvent(connection, transaction, input.ClientId, orderId, null, adminId, "ticket créé", $"Ticket {ticket} créé.");

        if (input.InitialPaymentAmount > 0)
        {
            var (total, _) = GetOrderAmounts(connection, transaction, orderId);
            var payAmount = Math.Min(total, Math.Round(input.InitialPaymentAmount, 0, MidpointRounding.AwayFromZero));
            if (payAmount > 0)
            {
                var method = string.IsNullOrWhiteSpace(input.InitialPaymentMethod) ? "Espèces" : input.InitialPaymentMethod.Trim();
                var receiptNo = Receipt();
                var paymentId = InsertAndGetId(connection, transaction,
                    "INSERT INTO [Payments] ([OrderID], [ClientID], [Amount], [PaymentAt], [PaymentMethod], [Reference], [ReceiptNo], [RecordedBy]) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    orderId, input.ClientId, payAmount, DateTime.Now, method, "Acompte au dépôt", receiptNo, adminId);
                if (method == "Espèces")
                    Execute(connection, transaction, "INSERT INTO [CashMovements] ([PaymentID], [Direction], [Amount], [Description], [CreatedAt], [RecordedBy]) VALUES (?, ?, ?, ?, ?, ?)", paymentId, "entrée", payAmount, $"Acompte {receiptNo}", DateTime.Now, adminId);
                AddEvent(connection, transaction, input.ClientId, orderId, null, adminId, "paiement reçu", $"{payAmount:N0} FCFA (acompte) — {method}.");
            }
        }

        transaction.Commit();
        return orderId;
    }

    private static List<T> QueryWithConnection<T>(DbConnection connection, DbTransaction? transaction, string sql, Func<DbDataReader, T> map, params object?[] args)
    {
        using var command = Command(connection, transaction, sql, args);
        using var reader = command.ExecuteReader();
        var result = new List<T>();
        while (reader.Read()) result.Add(map(reader));
        return result;
    }

    public OrderDetailViewModel? GetOrderDetails(int id)
    {
        var order = Query("SELECT o.[OrderID], o.[ClientID], o.[TicketNo], o.[CreatedAt], o.[OrderStatus], o.[Notes], c.[FirstName], c.[LastName], c.[Phone], c.[WhatsApp], c.[Email], c.[CreatedAt], o.[DiscountAmount] " +
                          "FROM [Orders] AS o INNER JOIN [Clients] AS c ON o.[ClientID]=c.[ClientID] WHERE o.[OrderID]=?",
            r => new OrderDetailViewModel
            {
                Order = new OrderSummary { OrderId = Number(r, 0), ClientId = Number(r, 1), TicketNo = Text(r, 2), CreatedAt = Date(r, 3), OrderStatus = Text(r, 4), ClientName = (Text(r, 6) + " " + Text(r, 7)).Trim(), DiscountAmount = Money(r, 12) },
                Notes = Text(r, 5),
                Client = new ClientRecord { ClientId = Number(r, 1), FirstName = Text(r, 6), LastName = Text(r, 7), Phone = Text(r, 8), WhatsApp = Text(r, 9), Email = Text(r, 10), CreatedAt = Date(r, 11) }
            }, id).FirstOrDefault();
        if (order is null) return null;
        (order.Order.Total, order.Order.Paid) = GetOrderAmounts(id);
        order.Order.ReadyItems = Query("SELECT COUNT(*) FROM [OrderItems] WHERE [OrderID]=? AND [ItemStatus]='prêt'", r => Number(r, 0), id).FirstOrDefault();
        order.Items = Query("SELECT i.[ItemID], c.[CategoryName], i.[Description], i.[UnitPrice], i.[ExpressFee], i.[IsExpress], i.[ItemStatus], i.[DepositAt], i.[ExpectedPickupAt], i.[WithdrawnAt], i.[ConditionNotes], i.[StorageLocation] " +
                            "FROM [OrderItems] AS i INNER JOIN [TariffCategories] AS c ON i.[CategoryID]=c.[CategoryID] WHERE i.[OrderID]=? ORDER BY i.[ItemID]",
            r => new OrderItemViewModel { ItemId = Number(r, 0), CategoryName = Text(r, 1), Description = Text(r, 2), UnitPrice = Money(r, 3), ExpressFee = Money(r, 4), IsExpress = Flag(r, 5), ItemStatus = Text(r, 6), DepositAt = Date(r, 7), ExpectedPickupAt = MaybeDate(r, 8), WithdrawnAt = MaybeDate(r, 9), ConditionNotes = Text(r, 10), StorageLocation = Text(r, 11) }, id);
        order.Payments = Query("SELECT [PaymentID], [ReceiptNo], [Amount], [PaymentAt], [PaymentMethod], [Reference] FROM [Payments] WHERE [OrderID]=? ORDER BY [PaymentAt] DESC",
            r => new PaymentViewModel { PaymentId = Number(r, 0), ReceiptNo = Text(r, 1), Amount = Money(r, 2), PaymentAt = Date(r, 3), PaymentMethod = Text(r, 4), Reference = Text(r, 5) }, id);
        order.CashBalance = GetTillBalance();
        return order;
    }

    public void UpdateItem(int orderId, int itemId, UpdateItemInput input, int adminId)
    {
        var allowed = new[] { "déposé", "en traitement", "prêt", "retiré" };
        if (!allowed.Contains(input.ItemStatus)) throw new InvalidOperationException("Statut non valide.");
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var clientId = Scalar<int>(connection, transaction, "SELECT [ClientID] FROM [Orders] WHERE [OrderID]=?", orderId);
        if (clientId == 0) throw new InvalidOperationException("Ticket introuvable.");
        DateTime? withdrawnAt = input.ItemStatus == "retiré" ? DateTime.Now : null;
        var updated = Execute(connection, transaction,
            "UPDATE [OrderItems] SET [ItemStatus]=?, [ExpectedPickupAt]=?, [WithdrawnAt]=?, [StorageLocation]=? WHERE [ItemID]=? AND [OrderID]=?",
            input.ItemStatus, input.ExpectedPickupAt, withdrawnAt, input.StorageLocation?.Trim() ?? "", itemId, orderId);
        if (updated != 1) throw new InvalidOperationException("Article introuvable dans ce ticket.");
        AddEvent(connection, transaction, clientId, orderId, itemId, adminId, "statut modifié", $"Article marqué « {input.ItemStatus} ».");
        var remaining = Scalar<int>(connection, transaction, "SELECT COUNT(*) FROM [OrderItems] WHERE [OrderID]=? AND [ItemStatus]<>'retiré'", orderId);
        Execute(connection, transaction, "UPDATE [Orders] SET [OrderStatus]=? WHERE [OrderID]=?", remaining == 0 ? "terminée" : "ouverte", orderId);
        transaction.Commit();
    }

    public void UpdateAllOrderItemsStatus(int orderId, string status, string? storageLocation, int adminId)
    {
        var allowed = new[] { "déposé", "en traitement", "prêt", "retiré" };
        if (!allowed.Contains(status)) throw new InvalidOperationException("Statut non valide.");
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var clientId = Scalar<int>(connection, transaction, "SELECT [ClientID] FROM [Orders] WHERE [OrderID]=?", orderId);
        if (clientId == 0) throw new InvalidOperationException("Ticket introuvable.");
        DateTime? withdrawnAt = status == "retiré" ? DateTime.Now : null;
        if (!string.IsNullOrWhiteSpace(storageLocation))
        {
            Execute(connection, transaction,
                "UPDATE [OrderItems] SET [ItemStatus]=?, [WithdrawnAt]=?, [StorageLocation]=? WHERE [OrderID]=? AND [ItemStatus]<>'retiré'",
                status, withdrawnAt, storageLocation.Trim(), orderId);
        }
        else
        {
            Execute(connection, transaction,
                "UPDATE [OrderItems] SET [ItemStatus]=?, [WithdrawnAt]=? WHERE [OrderID]=? AND [ItemStatus]<>'retiré'",
                status, withdrawnAt, orderId);
        }
        AddEvent(connection, transaction, clientId, orderId, null, adminId, "lot modifié", $"Tous les articles restants marqués « {status} ».");
        var remaining = Scalar<int>(connection, transaction, "SELECT COUNT(*) FROM [OrderItems] WHERE [OrderID]=? AND [ItemStatus]<>'retiré'", orderId);
        Execute(connection, transaction, "UPDATE [Orders] SET [OrderStatus]=? WHERE [OrderID]=?", remaining == 0 ? "terminée" : "ouverte", orderId);
        transaction.Commit();
    }

    public int RecordPayment(int orderId, PaymentInput input, int adminId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var clientId = Scalar<int>(connection, transaction, "SELECT [ClientID] FROM [Orders] WHERE [OrderID]=?", orderId);
        if (clientId == 0) throw new InvalidOperationException("Ticket introuvable.");
        var (total, paid) = GetOrderAmounts(connection, transaction, orderId);
        var amount = Math.Round(input.Amount, 0, MidpointRounding.AwayFromZero);
        if (amount <= 0 || amount > total - paid) throw new InvalidOperationException("Le règlement doit être positif et ne peut pas dépasser le montant restant.");
        var receiptNo = Receipt();
        var paymentId = InsertAndGetId(connection, transaction,
            "INSERT INTO [Payments] ([OrderID], [ClientID], [Amount], [PaymentAt], [PaymentMethod], [Reference], [ReceiptNo], [RecordedBy]) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
            orderId, clientId, amount, DateTime.Now, input.PaymentMethod, input.Reference?.Trim() ?? "", receiptNo, adminId);
        if (input.PaymentMethod == "Espèces")
            Execute(connection, transaction, "INSERT INTO [CashMovements] ([PaymentID], [Direction], [Amount], [Description], [CreatedAt], [RecordedBy]) VALUES (?, ?, ?, ?, ?, ?)", paymentId, "entrée", amount, $"Règlement {receiptNo}", DateTime.Now, adminId);
        AddEvent(connection, transaction, clientId, orderId, null, adminId, "paiement reçu", $"{amount:N0} FCFA — {input.PaymentMethod}.");
        transaction.Commit();
        return paymentId;
    }

    public void AddCashMovement(CashMovementInput input, int adminId)
    {
        if (input.Direction is not ("entrée" or "sortie")) throw new InvalidOperationException("Mouvement de caisse invalide.");
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction,
            "INSERT INTO [CashMovements] ([Direction], [Amount], [Description], [CreatedAt], [RecordedBy]) VALUES (?, ?, ?, ?, ?)",
            input.Direction, Math.Round(input.Amount, 0, MidpointRounding.AwayFromZero), input.Description.Trim(), DateTime.Now, adminId);
        transaction.Commit();
    }

    public decimal GetTillBalance()
    {
        var movements = Query("SELECT [Direction], [Amount] FROM [CashMovements]", r => (Direction: Text(r, 0), Amount: Money(r, 1)));
        return movements.Sum(x => x.Direction == "entrée" ? x.Amount : -x.Amount);
    }

    public decimal GetClientBalance(int clientId) => GetOrderSummaries(clientId, includeReadyItems: false).Sum(o => o.Remaining);

    public decimal GetOutstanding() => GetOrderSummaries(includeReadyItems: false).Sum(o => o.Remaining);

    public (decimal Total, decimal Paid) GetOrderAmounts(int orderId)
    {
        using var connection = Open();
        return GetOrderAmounts(connection, null, orderId);
    }

    private static (decimal Total, decimal Paid) GetOrderAmounts(DbConnection connection, DbTransaction? transaction, int orderId)
    {
        var itemRows = QueryWithConnection(connection, transaction, "SELECT [UnitPrice], [ExpressFee] FROM [OrderItems] WHERE [OrderID]=?", r => Money(r, 0) + Money(r, 1), orderId);
        var discount = Scalar<decimal>(connection, transaction, "SELECT [DiscountAmount] FROM [Orders] WHERE [OrderID]=?", orderId);
        var paid = Scalar<decimal>(connection, transaction, "SELECT SUM([Amount]) FROM [Payments] WHERE [OrderID]=?", orderId);
        return (Math.Max(0, itemRows.Sum() - discount), paid);
    }

    public DailyClosingReport GetDailyClosingReport(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);
        var payments = Query(
            "SELECT [Amount], [PaymentMethod] FROM [Payments] WHERE [PaymentAt]>=? AND [PaymentAt]<?",
            r => (Amount: Money(r, 0), Method: Text(r, 1)), dayStart, dayEnd);
        var movements = Query(
            "SELECT [Direction], [Amount], [PaymentID] FROM [CashMovements] WHERE [CreatedAt]>=? AND [CreatedAt]<?",
            r => (Direction: Text(r, 0), Amount: Money(r, 1), HasPayment: !r.IsDBNull(2)), dayStart, dayEnd);

        var cash = payments.Where(p => p.Method == "Espèces").Sum(p => p.Amount);
        var mobile = payments.Where(p => p.Method is "MTN Money" or "Orange Money").Sum(p => p.Amount);
        var other = payments.Where(p => p.Method is not "Espèces" and not "MTN Money" and not "Orange Money").Sum(p => p.Amount);

        return new DailyClosingReport
        {
            Date = dayStart,
            PaymentCount = payments.Count,
            CashPayments = cash,
            MobileMoneyPayments = mobile,
            CardAndTransferPayments = other,
            TotalPayments = cash + mobile + other,
            ManualCashIn = movements.Where(m => !m.HasPayment && m.Direction == "entrée").Sum(m => m.Amount),
            ManualCashOut = movements.Where(m => !m.HasPayment && m.Direction == "sortie").Sum(m => m.Amount),
            TheoreticalTillBalance = GetTillBalance()
        };
    }

    public string GetSetting(string key, string fallback = "") =>
        Query("SELECT [SettingValue] FROM [AppSettings] WHERE [SettingKey]=?", r => Text(r, 0), key).FirstOrDefault() ?? fallback;

    public void SaveSetting(string key, string value)
    {
        using var connection = Open();
        if (Scalar<int>(connection, null, "SELECT COUNT(*) FROM [AppSettings] WHERE [SettingKey]=?", key) == 0)
            Execute(connection, null, "INSERT INTO [AppSettings] ([SettingKey], [SettingValue]) VALUES (?, ?)", key, value);
        else
            Execute(connection, null, "UPDATE [AppSettings] SET [SettingValue]=? WHERE [SettingKey]=?", value, key);
    }

    public EmailSettingsInput GetEmailSettings()
    {
        var settings = Query("SELECT [SettingKey], [SettingValue] FROM [AppSettings]",
            r => (Key: Text(r, 0), Value: Text(r, 1)))
            .ToDictionary(row => row.Key, row => row.Value, StringComparer.OrdinalIgnoreCase);
        string Read(string key, string fallback = "") => settings.GetValueOrDefault(key) ?? fallback;

        return new EmailSettingsInput
        {
            Host = Read("Email:Smtp:Host"),
            Port = int.TryParse(Read("Email:Smtp:Port", "587"), out var port) && port is > 0 and <= 65535 ? port : 587,
            EnableSsl = !string.Equals(Read("Email:Smtp:EnableSsl", "true"), "false", StringComparison.OrdinalIgnoreCase),
            User = Read("Email:Smtp:User"),
            Password = Read("Email:Smtp:Password"),
            FromAddress = Read("Email:Smtp:FromAddress"),
            FromName = Read("Email:Smtp:FromName", "Fresh & Care — Pressing"),
            AdditionalEmails = Read("Email:AdditionalEmails")
        };
    }

    public void SaveEmailSettings(EmailSettingsInput input)
    {
        var values = new Dictionary<string, string>
        {
            ["Email:Smtp:Host"] = input.Host?.Trim() ?? "",
            ["Email:Smtp:Port"] = input.Port.ToString(CultureInfo.InvariantCulture),
            ["Email:Smtp:EnableSsl"] = input.EnableSsl ? "true" : "false",
            ["Email:Smtp:User"] = input.User?.Trim() ?? "",
            ["Email:Smtp:FromAddress"] = input.FromAddress?.Trim() ?? "",
            ["Email:Smtp:FromName"] = string.IsNullOrWhiteSpace(input.FromName) ? "Fresh & Care — Pressing" : input.FromName.Trim(),
            ["Email:AdditionalEmails"] = input.AdditionalEmails?.Trim() ?? ""
        };
        if (input.ClearPassword)
            values["Email:Smtp:Password"] = "";
        else if (!string.IsNullOrWhiteSpace(input.Password))
            values["Email:Smtp:Password"] = input.Password;

        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        foreach (var (key, value) in values)
        {
            var exists = Scalar<int>(connection, transaction, "SELECT COUNT(*) FROM [AppSettings] WHERE [SettingKey]=?", key) > 0;
            if (exists)
                Execute(connection, transaction, "UPDATE [AppSettings] SET [SettingValue]=? WHERE [SettingKey]=?", value, key);
            else
                Execute(connection, transaction, "INSERT INTO [AppSettings] ([SettingKey], [SettingValue]) VALUES (?, ?)", key, value);
        }
        transaction.Commit();
    }

    private decimal ScalarDecimal(string sql, params object?[] args)
    {
        using var connection = Open();
        return Scalar<decimal>(connection, null, sql, args);
    }

    public List<ActivityHistoryEntry> GetClientHistory(int clientId) => Query(
        "SELECT [EventType], [Details], [EventAt] FROM [ActivityLog] WHERE [ClientID]=? ORDER BY [EventAt] DESC",
        r => new ActivityHistoryEntry { EventType = Text(r, 0), Details = Text(r, 1), EventAt = Date(r, 2) }, clientId);

    public List<ReadyOrderNotice> GetReadyOrderNotices()
    {
        var notices = Query(
            "SELECT o.[OrderID], c.[ClientID], o.[TicketNo], c.[FirstName], c.[LastName], c.[Phone], c.[WhatsApp], c.[Email], COUNT(i.[OrderID]), MIN(i.[ExpectedPickupAt]) " +
            "FROM (([OrderItems] AS i INNER JOIN [Orders] AS o ON i.[OrderID]=o.[OrderID]) INNER JOIN [Clients] AS c ON o.[ClientID]=c.[ClientID]) " +
            "WHERE i.[ItemStatus]='prêt' " +
            "GROUP BY o.[OrderID], c.[ClientID], o.[TicketNo], c.[FirstName], c.[LastName], c.[Phone], c.[WhatsApp], c.[Email]",
            r => new ReadyOrderNotice
            {
                OrderId = Number(r, 0),
                ClientId = Number(r, 1),
                TicketNo = Text(r, 2),
                ClientName = (Text(r, 3) + " " + Text(r, 4)).Trim(),
                Phone = Text(r, 5),
                WhatsApp = Text(r, 6),
                Email = Text(r, 7),
                ReadyItems = Number(r, 8),
                NextPickup = MaybeDate(r, 9)
            });
        return notices.OrderByDescending(notice => notice.NextPickup ?? DateTime.MaxValue).ToList();
    }

    public int CreateCommunicationLog(int clientId, int orderId, int adminId, string recipient, string subject, string body)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var id = InsertAndGetId(connection, transaction,
            "INSERT INTO [CommunicationMessages] ([ClientID], [OrderID], [AdminUserID], [Channel], [Recipient], [Subject], [MessageBody], [Status], [CreatedAt]) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
            clientId, orderId, adminId, "e-mail", recipient, subject, body, "en cours", DateTime.Now);
        transaction.Commit();
        return id;
    }

    public void UpdateCommunicationLog(int id, string status, string error = "")
    {
        using var connection = Open();
        Execute(connection, null,
            "UPDATE [CommunicationMessages] SET [Status]=?, [ErrorDetails]=?, [SentAt]=? WHERE [CommunicationMessageID]=?",
            status, error, status == "envoyé" ? DateTime.Now : null, id);
    }

    public List<CommunicationHistoryEntry> GetCommunicationHistory(int take = 30) => Query(
        "SELECT m.[CommunicationMessageID], m.[Channel], m.[Recipient], m.[Subject], m.[Status], m.[ErrorDetails], m.[CreatedAt], o.[TicketNo] FROM [CommunicationMessages] AS m LEFT JOIN [Orders] AS o ON m.[OrderID]=o.[OrderID] ORDER BY m.[CreatedAt] DESC",
        r => new CommunicationHistoryEntry
        {
            Id = Number(r, 0), Channel = Text(r, 1), Recipient = Text(r, 2), Subject = Text(r, 3),
            Status = Text(r, 4), Error = Text(r, 5), CreatedAt = Date(r, 6), TicketNo = Text(r, 7)
        }).Take(Math.Clamp(take, 1, 100)).ToList();

    public List<ClientConversationThread> GetMessageThreads(string? search = null, bool unreadOnly = false)
    {
        var messages = Query(
            "SELECT m.[MessageID], m.[ClientID], c.[FirstName], c.[LastName], c.[Phone], c.[Email], m.[OrderID], m.[SenderRole], m.[Subject], m.[Body], m.[SentAt], m.[IsRead] " +
            "FROM [ClientMessages] AS m INNER JOIN [Clients] AS c ON m.[ClientID]=c.[ClientID] ORDER BY m.[SentAt] DESC, m.[MessageID] DESC",
            MapClientMessage);
        var matchingClientIds = string.IsNullOrWhiteSpace(search)
            ? null
            : messages.Where(message =>
                    message.ClientName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    message.Phone.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    message.Email.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    message.Subject.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    message.Body.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(message => message.ClientId)
                .ToHashSet();

        return messages.GroupBy(message => message.ClientId).Select(group =>
        {
            var latest = group.First();
            return new ClientConversationThread
            {
                ClientId = latest.ClientId,
                ClientName = latest.ClientName,
                Phone = latest.Phone,
                Email = latest.Email,
                LastSubject = latest.Subject,
                LastBody = latest.Body,
                LastMessageAt = latest.SentAt,
                UnreadMessages = group.Count(message => message.SenderRole == "client" && !message.IsRead)
            };
        })
            .Where(thread => (!unreadOnly || thread.UnreadMessages > 0) &&
                             (matchingClientIds is null || matchingClientIds.Contains(thread.ClientId)))
            .OrderByDescending(thread => thread.LastMessageAt)
            .ToList();
    }

    public List<ClientConversationMessage> GetClientMessages(int clientId) => Query(
        "SELECT m.[MessageID], m.[ClientID], c.[FirstName], c.[LastName], c.[Phone], c.[Email], m.[OrderID], m.[SenderRole], m.[Subject], m.[Body], m.[SentAt], m.[IsRead] " +
        "FROM [ClientMessages] AS m INNER JOIN [Clients] AS c ON m.[ClientID]=c.[ClientID] WHERE m.[ClientID]=? ORDER BY m.[SentAt], m.[MessageID]",
        MapClientMessage, clientId);

    public int GetUnreadMessageCount(int clientId, string recipientRole)
    {
        var incomingRole = recipientRole == "client" ? "staff" : "client";
        return Query("SELECT COUNT(*) FROM [ClientMessages] WHERE [ClientID]=? AND [SenderRole]=? AND [IsRead]=False",
            r => Number(r, 0), clientId, incomingRole).FirstOrDefault();
    }

    public void MarkConversationRead(int clientId, string readerRole)
    {
        var incomingRole = readerRole == "client" ? "staff" : "client";
        using var connection = Open();
        Execute(connection, null,
            "UPDATE [ClientMessages] SET [IsRead]=True WHERE [ClientID]=? AND [SenderRole]=? AND [IsRead]=False",
            clientId, incomingRole);
    }

    public void SendClientMessage(int clientId, string senderRole, int senderUserId, ClientMessageInput input)
    {
        if (senderRole is not ("staff" or "client")) throw new InvalidOperationException("Rôle d’expéditeur non valide.");
        var subject = string.IsNullOrWhiteSpace(input.Subject) ? "Échange avec Fresh & Care" : input.Subject.Trim();
        var body = input.Body.Trim();
        if (body.Length == 0) throw new InvalidOperationException("Le message ne peut pas être vide.");

        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        if (Scalar<int>(connection, transaction, "SELECT COUNT(*) FROM [Clients] WHERE [ClientID]=? AND [IsActive]=True", clientId) == 0)
            throw new InvalidOperationException("Client introuvable.");
        InsertAndGetId(connection, transaction,
            "INSERT INTO [ClientMessages] ([ClientID], [SenderRole], [SenderUserID], [Subject], [Body], [SentAt], [IsRead]) VALUES (?, ?, ?, ?, ?, ?, ?)",
            clientId, senderRole, senderRole == "staff" ? senderUserId : clientId, subject, body, DateTime.Now, false);
        AddEvent(connection, transaction, clientId, null, null, senderRole == "staff" ? senderUserId : null,
            senderRole == "staff" ? "message envoyé au client" : "message envoyé au pressing", subject);
        transaction.Commit();
    }

    private static ClientConversationMessage MapClientMessage(DbDataReader reader)
    {
        var role = Text(reader, 7);
        var clientName = $"{Text(reader, 2)} {Text(reader, 3)}".Trim();
        return new ClientConversationMessage
        {
            MessageId = Number(reader, 0),
            ClientId = Number(reader, 1),
            ClientName = clientName,
            Phone = Text(reader, 4),
            Email = Text(reader, 5),
            SenderName = role == "staff" ? "Fresh & Care" : clientName,
            OrderId = reader.IsDBNull(6) ? null : Number(reader, 6),
            SenderRole = role,
            Subject = Text(reader, 8),
            Body = Text(reader, 9),
            SentAt = Date(reader, 10),
            IsRead = Flag(reader, 11)
        };
    }

    public InventoryViewModel GetInventory()
    {
        var items = Query(
            "SELECT [InventoryItemID], [Name], [Unit], [QuantityOnHand], [ReorderAt], [UnitCost], [UpdatedAt], [IsActive] FROM [InventoryItems] WHERE [IsActive]=True ORDER BY [Name]",
            r => new InventoryItem
            {
                InventoryItemId = Number(r, 0), Name = Text(r, 1), Unit = Text(r, 2),
                QuantityOnHand = Money(r, 3), ReorderAt = Money(r, 4), UnitCost = Money(r, 5),
                UpdatedAt = Date(r, 6), IsActive = Flag(r, 7)
            });
        return new InventoryViewModel { Items = items, StockValue = items.Sum(item => item.StockValue), LowStockCount = items.Count(item => item.IsLow) };
    }

    public int CreateInventoryItem(InventoryItemInput input, int adminId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var quantity = Math.Round(input.InitialQuantity, 3, MidpointRounding.AwayFromZero);
        var itemId = InsertAndGetId(connection, transaction,
            "INSERT INTO [InventoryItems] ([Name], [Unit], [QuantityOnHand], [ReorderAt], [UnitCost], [UpdatedAt], [IsActive]) VALUES (?, ?, ?, ?, ?, ?, ?)",
            input.Name.Trim(), input.Unit.Trim(), quantity, Math.Round(input.ReorderAt, 3), input.UnitCost, DateTime.Now, true);
        if (quantity > 0)
            Execute(connection, transaction,
                "INSERT INTO [InventoryTransactions] ([InventoryItemID], [Direction], [Quantity], [UnitCost], [Reason], [AdminUserID], [CreatedAt]) VALUES (?, ?, ?, ?, ?, ?, ?)",
                itemId, "entrée", quantity, input.UnitCost, "Stock initial", adminId, DateTime.Now);
        transaction.Commit();
        return itemId;
    }

    public void RecordInventoryMovement(int itemId, InventoryMovementInput input, int adminId)
    {
        if (input.Direction is not ("entrée" or "sortie")) throw new InvalidOperationException("Type de mouvement de stock invalide.");
        var quantity = Math.Round(input.Quantity, 3, MidpointRounding.AwayFromZero);
        if (quantity <= 0) throw new InvalidOperationException("La quantité doit être supérieure à zéro.");

        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var item = QueryWithConnection(connection, transaction,
            "SELECT [QuantityOnHand], [UnitCost], [IsActive] FROM [InventoryItems] WHERE [InventoryItemID]=?",
            r => (Quantity: Money(r, 0), UnitCost: Money(r, 1), IsActive: Flag(r, 2)), itemId).FirstOrDefault();
        if (!item.IsActive) throw new InvalidOperationException("Consommable introuvable ou archivé.");

        var newQuantity = Math.Round(item.Quantity + (input.Direction == "entrée" ? quantity : -quantity), 3, MidpointRounding.AwayFromZero);
        if (newQuantity < 0) throw new InvalidOperationException("La sortie dépasse le stock disponible.");
        var newCost = input.Direction == "entrée" && input.UnitCost.HasValue ? input.UnitCost.Value : item.UnitCost;
        Execute(connection, transaction,
            "UPDATE [InventoryItems] SET [QuantityOnHand]=?, [UnitCost]=?, [UpdatedAt]=? WHERE [InventoryItemID]=?",
            newQuantity, newCost, DateTime.Now, itemId);
        Execute(connection, transaction,
            "INSERT INTO [InventoryTransactions] ([InventoryItemID], [Direction], [Quantity], [UnitCost], [Reason], [AdminUserID], [CreatedAt]) VALUES (?, ?, ?, ?, ?, ?, ?)",
            itemId, input.Direction, quantity, input.UnitCost ?? item.UnitCost, input.Reason.Trim(), adminId, DateTime.Now);
        transaction.Commit();
    }

    public PaymentViewModel? GetPayment(int paymentId) => Query(
        "SELECT [PaymentID], [ReceiptNo], [Amount], [PaymentAt], [PaymentMethod], [Reference] FROM [Payments] WHERE [PaymentID]=?",
        r => new PaymentViewModel { PaymentId = Number(r, 0), ReceiptNo = Text(r, 1), Amount = Money(r, 2), PaymentAt = Date(r, 3), PaymentMethod = Text(r, 4), Reference = Text(r, 5) }, paymentId).FirstOrDefault();

    public int PaymentOrderId(int paymentId) => Query("SELECT [OrderID] FROM [Payments] WHERE [PaymentID]=?", r => Number(r, 0), paymentId).FirstOrDefault();

    private static void AddEvent(DbConnection connection, DbTransaction transaction, int? clientId, int? orderId, int? itemId, int? adminId, string type, string details) =>
        Execute(connection, transaction,
            "INSERT INTO [ActivityLog] ([ClientID], [OrderID], [ItemID], [AdminUserID], [EventType], [Details], [EventAt]) VALUES (?, ?, ?, ?, ?, ?, ?)",
            clientId, orderId, itemId, adminId, type, details, DateTime.Now);
}

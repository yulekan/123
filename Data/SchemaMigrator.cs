using System.Data.SQLite;

namespace PPEAccounting.Data
{
    /// <summary>
    /// Версионирование схемы БД. Версия хранится в PRAGMA user_version, поэтому
    /// обновление приложения не требует ручного вмешательства в файл базы.
    /// </summary>
    public static class SchemaMigrator
    {
        public const int CurrentVersion = 4;

        public static void Migrate(Database database, bool seedDemoData)
        {
            database.InTransaction((connection, transaction) =>
            {
                var version = SqlValue.ToInt(connection.Scalar(transaction, "PRAGMA user_version"));

                if (version < 1)
                {
                    CreateSchema(connection, transaction);

                    // В прежней версии ограничения уникальности не было, поэтому дубли норм
                    // могли накопиться. Схлопываем их до создания уникального индекса.
                    DeduplicateNorms(connection, transaction);
                    CreateIndexes(connection, transaction);
                }

                // Версия 2: учётные записи, журнал действий, подтверждение получения СИЗ.
                // Выполняется до наполнения демо-данными: сид уже использует новые колонки.
                if (version < 2)
                    UpgradeToV2(connection, transaction);

                // Версия 3: пароль в зашифрованном виде (историческая миграция).
                if (version < 3)
                    UpgradeToV3(connection, transaction);

                // Версия 4: отказ от учётных записей и ролей — вход в систему не требуется.
                if (version < 4)
                    UpgradeToV4(connection, transaction);

                if (version < 1 && seedDemoData)
                    SeedDemoData(connection, transaction);

                // Версия — константа, параметризовать PRAGMA нельзя.
                connection.Exec(transaction, "PRAGMA user_version = " + CurrentVersion);
            });
        }

        /// <summary>
        /// Добавляет учётные записи и журнал действий. Выполняется поверх базы версии 1,
        /// поэтому данные сотрудников, норм и выдач не затрагиваются.
        /// </summary>
        private static void UpgradeToV2(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            connection.Exec(transaction, @"
CREATE TABLE IF NOT EXISTS User (
    IdUser       INTEGER PRIMARY KEY AUTOINCREMENT,
    Login        TEXT    NOT NULL UNIQUE,
    PasswordHash TEXT    NOT NULL,
    PasswordSalt TEXT    NOT NULL,
    Role         TEXT    NOT NULL CHECK (Role IN ('Специалист', 'Работник')),
    IdEmployee   INTEGER NULL UNIQUE REFERENCES Employee(IdEmployee) ON DELETE CASCADE,
    IsActive     INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
    CreatedAt    TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS AuditLog (
    IdAudit   INTEGER PRIMARY KEY AUTOINCREMENT,
    UserName  TEXT NOT NULL,
    Role      TEXT NOT NULL,
    Action    TEXT NOT NULL,
    Details   TEXT,
    CreatedAt TEXT NOT NULL
);");

            if (!ColumnExists(connection, transaction, "PPEIssue", "IdIssuer"))
            {
                connection.Exec(transaction,
                    "ALTER TABLE PPEIssue ADD COLUMN IdIssuer INTEGER NULL REFERENCES User(IdUser)");
            }

            if (!ColumnExists(connection, transaction, "PPEIssue", "ReceivedDate"))
                connection.Exec(transaction, "ALTER TABLE PPEIssue ADD COLUMN ReceivedDate TEXT NULL");

            if (!ColumnExists(connection, transaction, "PPEItem", "MinStock"))
                connection.Exec(transaction, "ALTER TABLE PPEItem ADD COLUMN MinStock INTEGER NOT NULL DEFAULT 0");

            connection.Exec(transaction, @"
CREATE INDEX IF NOT EXISTS IX_PPEIssue_Issuer ON PPEIssue (IdIssuer);
CREATE INDEX IF NOT EXISTS IX_User_Employee ON User (IdEmployee);");
        }

        /// <summary>
        /// Версия 3: служебное поле PasswordView в таблице User.
        /// Добавляется только колонка; в версии 4 таблица User удаляется целиком,
        /// поэтому в актуальной схеме этого поля нет.
        /// </summary>
        private static void UpgradeToV3(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            if (!ColumnExists(connection, transaction, "User", "PasswordView"))
                connection.Exec(transaction, "ALTER TABLE User ADD COLUMN PasswordView TEXT NULL");
        }

        /// <summary>
        /// Версия 4: приложение работает без входа в систему, поэтому учётные записи,
        /// роли и пароли удалены. Журнал выдачи больше не хранит ссылку на пользователя,
        /// а журнал действий — логин и роль.
        /// </summary>
        private static void UpgradeToV4(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            if (ColumnExists(connection, transaction, "PPEIssue", "IdIssuer"))
            {
                connection.Exec(transaction, @"
CREATE TABLE PPEIssue_new (
    IdIssue      INTEGER PRIMARY KEY AUTOINCREMENT,
    IdEmployee   INTEGER NOT NULL REFERENCES Employee(IdEmployee) ON DELETE RESTRICT,
    IdPPEItem    INTEGER NOT NULL REFERENCES PPEItem(IdPPEItem) ON DELETE RESTRICT,
    IdNorm       INTEGER REFERENCES IssueNorm(IdNorm) ON DELETE RESTRICT,
    IssueDate    TEXT    NOT NULL,
    Qty          INTEGER NOT NULL CHECK (Qty > 0),
    ReturnDate   TEXT,
    Status       TEXT    NOT NULL DEFAULT 'Выдана',
    ReceivedDate TEXT
);

INSERT INTO PPEIssue_new(IdIssue, IdEmployee, IdPPEItem, IdNorm, IssueDate, Qty, ReturnDate, Status, ReceivedDate)
SELECT IdIssue, IdEmployee, IdPPEItem, IdNorm, IssueDate, Qty, ReturnDate, Status, ReceivedDate FROM PPEIssue;

DROP TABLE PPEIssue;
ALTER TABLE PPEIssue_new RENAME TO PPEIssue;");
            }

            connection.Exec(transaction, "DROP TABLE IF EXISTS User");

            if (ColumnExists(connection, transaction, "AuditLog", "UserName"))
            {
                connection.Exec(transaction, @"
CREATE TABLE AuditLog_new (
    IdAudit   INTEGER PRIMARY KEY AUTOINCREMENT,
    Action    TEXT NOT NULL,
    Details   TEXT,
    CreatedAt TEXT NOT NULL
);

INSERT INTO AuditLog_new(IdAudit, Action, Details, CreatedAt)
SELECT IdAudit, Action, Details, CreatedAt FROM AuditLog;

DROP TABLE AuditLog;
ALTER TABLE AuditLog_new RENAME TO AuditLog;");
            }

            // Индексы журнала выдачи пересоздаются: старая таблица удалена.
            CreateIndexes(connection, transaction);
        }

        private static bool ColumnExists(SQLiteConnection connection, SQLiteTransaction transaction,
            string table, string column)
        {
            using (var command = new SQLiteCommand("PRAGMA table_info(" + table + ")", connection, transaction))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), column, System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static void CreateSchema(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            connection.Exec(transaction, @"
CREATE TABLE IF NOT EXISTS Department (
    IdDepartment INTEGER PRIMARY KEY AUTOINCREMENT,
    Name         TEXT    NOT NULL,
    Description  TEXT
);

CREATE TABLE IF NOT EXISTS Position (
    IdPosition   INTEGER PRIMARY KEY AUTOINCREMENT,
    Name         TEXT    NOT NULL,
    IdDepartment INTEGER NOT NULL REFERENCES Department(IdDepartment) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Employee (
    IdEmployee INTEGER PRIMARY KEY AUTOINCREMENT,
    FullName   TEXT    NOT NULL,
    IdPosition INTEGER NOT NULL REFERENCES Position(IdPosition) ON DELETE RESTRICT,
    HireDate   TEXT    NOT NULL,
    IsActive   INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1))
);

CREATE TABLE IF NOT EXISTS Supplier (
    IdSupplier INTEGER PRIMARY KEY AUTOINCREMENT,
    Name       TEXT    NOT NULL,
    INN        TEXT,
    Phone      TEXT,
    Address    TEXT
);

CREATE TABLE IF NOT EXISTS PPEItem (
    IdPPEItem  INTEGER PRIMARY KEY AUTOINCREMENT,
    Name       TEXT    NOT NULL,
    Unit       TEXT    NOT NULL DEFAULT 'шт',
    StockQty   INTEGER NOT NULL DEFAULT 0 CHECK (StockQty >= 0),
    IdSupplier INTEGER REFERENCES Supplier(IdSupplier) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS IssueNorm (
    IdNorm           INTEGER PRIMARY KEY AUTOINCREMENT,
    IdPPEItem        INTEGER NOT NULL REFERENCES PPEItem(IdPPEItem) ON DELETE RESTRICT,
    IdPosition       INTEGER NOT NULL REFERENCES Position(IdPosition) ON DELETE RESTRICT,
    QtyPerYear       INTEGER NOT NULL CHECK (QtyPerYear > 0),
    WearPeriodMonths INTEGER NOT NULL CHECK (WearPeriodMonths > 0)
);

CREATE TABLE IF NOT EXISTS PPEIssue (
    IdIssue    INTEGER PRIMARY KEY AUTOINCREMENT,
    IdEmployee INTEGER NOT NULL REFERENCES Employee(IdEmployee) ON DELETE RESTRICT,
    IdPPEItem  INTEGER NOT NULL REFERENCES PPEItem(IdPPEItem) ON DELETE RESTRICT,
    IdNorm     INTEGER REFERENCES IssueNorm(IdNorm) ON DELETE RESTRICT,
    IssueDate  TEXT    NOT NULL,
    Qty        INTEGER NOT NULL CHECK (Qty > 0),
    ReturnDate TEXT,
    Status     TEXT    NOT NULL DEFAULT 'Выдана'
);

CREATE TABLE IF NOT EXISTS Supply (
    IdSupply   INTEGER PRIMARY KEY AUTOINCREMENT,
    IdSupplier INTEGER NOT NULL REFERENCES Supplier(IdSupplier) ON DELETE RESTRICT,
    IdPPEItem  INTEGER NOT NULL REFERENCES PPEItem(IdPPEItem) ON DELETE RESTRICT,
    SupplyDate TEXT    NOT NULL,
    Qty        INTEGER NOT NULL CHECK (Qty > 0),
    Price      REAL    NOT NULL CHECK (Price >= 0)
);");
        }

        private static void DeduplicateNorms(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var duplicates = SqlValue.ToInt(connection.Scalar(transaction, @"
SELECT COUNT(*) FROM (
    SELECT IdPPEItem, IdPosition FROM IssueNorm GROUP BY IdPPEItem, IdPosition HAVING COUNT(*) > 1
)"));

            if (duplicates == 0)
                return;

            // Сначала переносим ссылки выдач на «старшую» норму, иначе удаление упрётся в внешний ключ.
            connection.Exec(transaction, @"
UPDATE PPEIssue
SET IdNorm = (
    SELECT MIN(n2.IdNorm)
    FROM IssueNorm n1
    JOIN IssueNorm n2 ON n2.IdPPEItem = n1.IdPPEItem AND n2.IdPosition = n1.IdPosition
    WHERE n1.IdNorm = PPEIssue.IdNorm)
WHERE IdNorm IS NOT NULL");

            connection.Exec(transaction, @"
DELETE FROM IssueNorm
WHERE IdNorm NOT IN (SELECT MIN(IdNorm) FROM IssueNorm GROUP BY IdPPEItem, IdPosition)");
        }

        private static void CreateIndexes(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            connection.Exec(transaction, @"
CREATE UNIQUE INDEX IF NOT EXISTS UX_IssueNorm_Item_Position ON IssueNorm (IdPPEItem, IdPosition);
CREATE INDEX IF NOT EXISTS IX_PPEIssue_Employee ON PPEIssue (IdEmployee, IssueDate);
CREATE INDEX IF NOT EXISTS IX_PPEIssue_Item ON PPEIssue (IdPPEItem);
CREATE INDEX IF NOT EXISTS IX_PPEIssue_Date ON PPEIssue (IssueDate);
CREATE INDEX IF NOT EXISTS IX_Employee_Position ON Employee (IdPosition);
CREATE INDEX IF NOT EXISTS IX_Position_Department ON Position (IdDepartment);
CREATE INDEX IF NOT EXISTS IX_Supply_Item ON Supply (IdPPEItem);");
        }

        private static void SeedDemoData(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            // Сид выполняется только для пустой базы: если таблицы уже заполнены
            // (например, мигрирует база прежней версии) — ничего не добавляем.
            var existing = SqlValue.ToInt(connection.Scalar(transaction, "SELECT COUNT(*) FROM Department"));
            if (existing > 0)
                return;

            connection.Exec(transaction, @"
INSERT INTO Department(Name, Description) VALUES ('Убойный цех', 'Первичная переработка');
INSERT INTO Department(Name, Description) VALUES ('Колбасный цех', 'Производство колбас');
INSERT INTO Department(Name, Description) VALUES ('Холодильник', 'Хранение сырья');
INSERT INTO Department(Name, Description) VALUES ('Санитарная служба', 'Санитарная обработка');

INSERT INTO Position(Name, IdDepartment) SELECT 'Обвальщик', IdDepartment FROM Department WHERE Name = 'Убойный цех';
INSERT INTO Position(Name, IdDepartment) SELECT 'Изготовитель колбас', IdDepartment FROM Department WHERE Name = 'Колбасный цех';
INSERT INTO Position(Name, IdDepartment) SELECT 'Грузчик', IdDepartment FROM Department WHERE Name = 'Холодильник';
INSERT INTO Position(Name, IdDepartment) SELECT 'Уборщик', IdDepartment FROM Department WHERE Name = 'Санитарная служба';

INSERT INTO Employee(FullName, IdPosition, HireDate, IsActive)
SELECT 'Иванов Иван Иванович', IdPosition, '2023-03-01', 1 FROM Position WHERE Name = 'Обвальщик';
INSERT INTO Employee(FullName, IdPosition, HireDate, IsActive)
SELECT 'Петров Пётр Петрович', IdPosition, '2022-09-15', 1 FROM Position WHERE Name = 'Изготовитель колбас';
INSERT INTO Employee(FullName, IdPosition, HireDate, IsActive)
SELECT 'Сидоров Сидор Сидорович', IdPosition, '2024-01-10', 1 FROM Position WHERE Name = 'Грузчик';
INSERT INTO Employee(FullName, IdPosition, HireDate, IsActive)
SELECT 'Козлова Анна Сергеевна', IdPosition, '2023-06-20', 1 FROM Position WHERE Name = 'Уборщик';

INSERT INTO Supplier(Name) VALUES ('ООО «СпецСнаб»');
INSERT INTO Supplier(Name) VALUES ('ЧП «Защита-Сервис»');

INSERT INTO PPEItem(Name, Unit, StockQty, MinStock, IdSupplier)
SELECT 'Халат х/б', 'шт', 50, 10, IdSupplier FROM Supplier WHERE Name = 'ООО «СпецСнаб»';
INSERT INTO PPEItem(Name, Unit, StockQty, MinStock, IdSupplier)
SELECT 'Перчатки резиновые', 'пара', 120, 30, IdSupplier FROM Supplier WHERE Name = 'ООО «СпецСнаб»';
INSERT INTO PPEItem(Name, Unit, StockQty, MinStock, IdSupplier)
SELECT 'Сапоги резиновые', 'пара', 30, 5, IdSupplier FROM Supplier WHERE Name = 'ЧП «Защита-Сервис»';
INSERT INTO PPEItem(Name, Unit, StockQty, MinStock, IdSupplier)
SELECT 'Каска защитная', 'шт', 20, 5, IdSupplier FROM Supplier WHERE Name = 'ЧП «Защита-Сервис»';
INSERT INTO PPEItem(Name, Unit, StockQty, MinStock, IdSupplier)
SELECT 'Фартук прорезиненный', 'шт', 40, 10, IdSupplier FROM Supplier WHERE Name = 'ООО «СпецСнаб»';

INSERT INTO IssueNorm(IdPPEItem, IdPosition, QtyPerYear, WearPeriodMonths)
SELECT i.IdPPEItem, p.IdPosition, 2, 12 FROM PPEItem i, Position p
WHERE i.Name = 'Халат х/б' AND p.Name = 'Обвальщик';
INSERT INTO IssueNorm(IdPPEItem, IdPosition, QtyPerYear, WearPeriodMonths)
SELECT i.IdPPEItem, p.IdPosition, 12, 1 FROM PPEItem i, Position p
WHERE i.Name = 'Перчатки резиновые' AND p.Name = 'Обвальщик';
INSERT INTO IssueNorm(IdPPEItem, IdPosition, QtyPerYear, WearPeriodMonths)
SELECT i.IdPPEItem, p.IdPosition, 1, 24 FROM PPEItem i, Position p
WHERE i.Name = 'Сапоги резиновые' AND p.Name = 'Грузчик';

-- Нормы для остальных должностей: демонстрационные данные должны покрывать
-- все разделы отчётов, иначе проверять соблюдение норм не на чем.
INSERT INTO IssueNorm(IdPPEItem, IdPosition, QtyPerYear, WearPeriodMonths)
SELECT i.IdPPEItem, p.IdPosition, 2, 12 FROM PPEItem i, Position p
WHERE i.Name = 'Халат х/б' AND p.Name IN ('Изготовитель колбас', 'Уборщик');
INSERT INTO IssueNorm(IdPPEItem, IdPosition, QtyPerYear, WearPeriodMonths)
SELECT i.IdPPEItem, p.IdPosition, 12, 1 FROM PPEItem i, Position p
WHERE i.Name = 'Перчатки резиновые' AND p.Name IN ('Изготовитель колбас', 'Грузчик');
INSERT INTO IssueNorm(IdPPEItem, IdPosition, QtyPerYear, WearPeriodMonths)
SELECT i.IdPPEItem, p.IdPosition, 1, 24 FROM PPEItem i, Position p
WHERE i.Name = 'Сапоги резиновые' AND p.Name = 'Обвальщик';
INSERT INTO IssueNorm(IdPPEItem, IdPosition, QtyPerYear, WearPeriodMonths)
SELECT i.IdPPEItem, p.IdPosition, 1, 36 FROM PPEItem i, Position p
WHERE i.Name = 'Каска защитная';
INSERT INTO IssueNorm(IdPPEItem, IdPosition, QtyPerYear, WearPeriodMonths)
SELECT i.IdPPEItem, p.IdPosition, 2, 12 FROM PPEItem i, Position p
WHERE i.Name = 'Фартук прорезиненный' AND p.Name IN ('Обвальщик', 'Изготовитель колбас');");
        }
    }
}

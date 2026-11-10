USE RaceDayApiDb;

DECLARE @evSchema SYSNAME, @evTable SYSNAME, @usSchema SYSNAME, @usTable SYSNAME, @sql NVARCHAR(MAX);

SELECT TOP 1 @evSchema = TABLE_SCHEMA, @evTable = TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME IN ('Event', 'Events');

SELECT TOP 1 @usSchema = TABLE_SCHEMA, @usTable = TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME IN ('User', 'Users', 'AppUser', 'AppUsers');

-- Shows the tables it found (none of these should be NULL)
SELECT @evSchema AS EventSchema, @evTable AS EventTable, @usSchema AS UserSchema, @usTable AS UserTable;

IF @evTable IS NOT NULL AND COL_LENGTH(QUOTENAME(@evSchema) + '.' + QUOTENAME(@evTable), 'BannerUrl') IS NULL
BEGIN
    SET @sql = N'ALTER TABLE ' + QUOTENAME(@evSchema) + N'.' + QUOTENAME(@evTable) + N' ADD BannerUrl NVARCHAR(500) NULL;';
    EXEC sp_executesql @sql;
END

IF @usTable IS NOT NULL AND COL_LENGTH(QUOTENAME(@usSchema) + '.' + QUOTENAME(@usTable), 'ProfilePictureUrl') IS NULL
BEGIN
    SET @sql = N'ALTER TABLE ' + QUOTENAME(@usSchema) + N'.' + QUOTENAME(@usTable) + N' ADD ProfilePictureUrl NVARCHAR(500) NULL;';
    EXEC sp_executesql @sql;
END

-- Verification: this must list BOTH columns
SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME IN ('BannerUrl', 'ProfilePictureUrl');
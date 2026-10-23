-- =============================================
-- Notes API - SQL Server schema
-- Safe to re-run: creates what's missing, and upgrades an
-- existing Users table by adding the PasswordHash column.
-- =============================================

IF DB_ID(N'NotesDb') IS NULL
    CREATE DATABASE NotesDb;
GO

USE NotesDb;
GO

-- ---------- Users ----------
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id           INT IDENTITY(1,1) NOT NULL,
        UserName     NVARCHAR(100)     NOT NULL,
        Nickname     NVARCHAR(100)     NULL,
        PasswordHash NVARCHAR(256)     NOT NULL,   -- ASP.NET Core Identity PBKDF2 hash, never plain text

        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Users_UserName UNIQUE (UserName)
    );
END
ELSE IF COL_LENGTH(N'dbo.Users', N'PasswordHash') IS NULL
BEGIN
    -- Upgrade from the first version of the schema.
    -- Existing users get an empty hash and cannot log in until a password is set.
    ALTER TABLE dbo.Users
        ADD PasswordHash NVARCHAR(256) NOT NULL
            CONSTRAINT DF_Users_PasswordHash DEFAULT N'';
END
GO

-- ---------- Notes ----------
IF OBJECT_ID(N'dbo.Notes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notes
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        Note        NVARCHAR(MAX)     NOT NULL,
        UserId      INT               NOT NULL,
        [Timestamp] DATETIME2(3)      NOT NULL
            CONSTRAINT DF_Notes_Timestamp DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_Notes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Notes_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id)
            ON DELETE CASCADE          -- deleting a user deletes their notes
    );

    CREATE NONCLUSTERED INDEX IX_Notes_UserId_Timestamp
        ON dbo.Notes (UserId, [Timestamp] DESC);
END
GO

-- Users are created through POST /api/users so the password gets hashed by the API.

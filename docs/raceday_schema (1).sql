/* =====================================================================
   RaceDay Database Script
   Target: SQL Server (SSMS)
   Matches: docs/raceday_erd.png and docs/api_endpoint_plan.md

   Entities: Role, [User], Event, Category, Enrolment, Result
   Run this script on a clean database. It drops and recreates all
   RaceDay tables, then seeds sample data.
   ===================================================================== */

/* ---------------------------------------------------------------------
   0. Clean slate — drop tables in FK-safe (child-first) order
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Result', 'U') IS NOT NULL DROP TABLE dbo.Result;
IF OBJECT_ID('dbo.Enrolment', 'U') IS NOT NULL DROP TABLE dbo.Enrolment;
IF OBJECT_ID('dbo.Category', 'U') IS NOT NULL DROP TABLE dbo.Category;
IF OBJECT_ID('dbo.Event', 'U') IS NOT NULL DROP TABLE dbo.Event;
IF OBJECT_ID('dbo.[User]', 'U') IS NOT NULL DROP TABLE dbo.[User];
IF OBJECT_ID('dbo.Role', 'U') IS NOT NULL DROP TABLE dbo.Role;
GO

/* ---------------------------------------------------------------------
   1. Role
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Role (
    RoleId      INT IDENTITY(1,1) PRIMARY KEY,
    RoleName    NVARCHAR(20) NOT NULL UNIQUE
        CONSTRAINT CK_Role_RoleName CHECK (RoleName IN ('Organiser', 'Participant'))
);
GO

/* ---------------------------------------------------------------------
   2. [User]  (User is a reserved word in SQL Server, hence brackets)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.[User] (
    UserId          INT IDENTITY(1,1) PRIMARY KEY,
    RoleId          INT NOT NULL,
    FullName        NVARCHAR(100) NOT NULL,
    Email           NVARCHAR(150) NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(255) NOT NULL,
    PhoneNumber     NVARCHAR(20)  NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_User_Role FOREIGN KEY (RoleId) REFERENCES dbo.Role(RoleId)
);
GO

/* ---------------------------------------------------------------------
   3. Event  (owned by an Organiser, i.e. a User)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Event (
    EventId         INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserId     INT NOT NULL,
    Name            NVARCHAR(150) NOT NULL,
    EventDate       DATE NOT NULL,
    Location        NVARCHAR(200) NOT NULL,
    Description     NVARCHAR(MAX) NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Published'
        CONSTRAINT CK_Event_Status CHECK (Status IN ('Draft', 'Published', 'Cancelled', 'Completed')),
    CONSTRAINT FK_Event_Organiser FOREIGN KEY (OrganiserId) REFERENCES dbo.[User](UserId)
);
GO

/* ---------------------------------------------------------------------
   4. Category  (belongs to an Event)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Category (
    CategoryId      INT IDENTITY(1,1) PRIMARY KEY,
    EventId         INT NOT NULL,
    Name            NVARCHAR(100) NOT NULL,
    DistanceKm      DECIMAL(5,2) NOT NULL,
    MaxParticipants INT NOT NULL DEFAULT 100,
    Price           DECIMAL(8,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_Category_Event FOREIGN KEY (EventId) REFERENCES dbo.Event(EventId),
    CONSTRAINT UQ_Category_EventName UNIQUE (EventId, Name)
);
GO

/* ---------------------------------------------------------------------
   5. Enrolment  (a Participant enrolling in a Category)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Enrolment (
    EnrolmentId     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantId   INT NOT NULL,
    CategoryId      INT NOT NULL,
    EnrolmentDate   DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    BibNumber       INT NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Confirmed'
        CONSTRAINT CK_Enrolment_Status CHECK (Status IN ('Confirmed', 'Cancelled')),
    CONSTRAINT FK_Enrolment_Participant FOREIGN KEY (ParticipantId) REFERENCES dbo.[User](UserId),
    CONSTRAINT FK_Enrolment_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Category(CategoryId),
    CONSTRAINT UQ_Enrolment_ParticipantCategory UNIQUE (ParticipantId, CategoryId),
    CONSTRAINT UQ_Enrolment_CategoryBib UNIQUE (CategoryId, BibNumber)
);
GO

/* ---------------------------------------------------------------------
   6. Result  (one optional result per Enrolment)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Result (
    ResultId        INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentId     INT NOT NULL UNIQUE,
    FinishTime      TIME NULL,
    Position        INT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Finished'
        CONSTRAINT CK_Result_Status CHECK (Status IN ('Finished', 'DNF', 'DQ')),
    CONSTRAINT FK_Result_Enrolment FOREIGN KEY (EnrolmentId) REFERENCES dbo.Enrolment(EnrolmentId)
);
GO

/* =====================================================================
   SEED DATA
   ===================================================================== */

/* ---- Roles ---------------------------------------------------------- */
INSERT INTO dbo.Role (RoleName) VALUES ('Organiser'), ('Participant');
GO

/* ---- Users: 2 Organisers, 2 Participants ----------------------------- */
INSERT INTO dbo.[User] (RoleId, FullName, Email, PasswordHash, PhoneNumber)
VALUES
    ((SELECT RoleId FROM dbo.Role WHERE RoleName = 'Organiser'),   'John Smith',   'john.smith@raceday.com',  'HASHED_PASSWORD_1', '0821234567'),
    ((SELECT RoleId FROM dbo.Role WHERE RoleName = 'Organiser'),   'Sarah Lee',    'sarah.lee@raceday.com',   'HASHED_PASSWORD_2', '0827654321'),
    ((SELECT RoleId FROM dbo.Role WHERE RoleName = 'Participant'), 'Mike Johnson', 'mike.johnson@example.com','HASHED_PASSWORD_3', '0731122334'),
    ((SELECT RoleId FROM dbo.Role WHERE RoleName = 'Participant'), 'Emma Brown',   'emma.brown@example.com',  'HASHED_PASSWORD_4', '0739988776');
GO

/* ---- Events: 3, spread across the two Organisers ---------------------- */
INSERT INTO dbo.Event (OrganiserId, Name, EventDate, Location, Description, Status)
VALUES
    ((SELECT UserId FROM dbo.[User] WHERE Email = 'john.smith@raceday.com'),
        'City Marathon 2026', '2026-11-08', 'Johannesburg CBD',
        'An annual road race through the heart of the city, with routes for every ability.', 'Published'),
    ((SELECT UserId FROM dbo.[User] WHERE Email = 'sarah.lee@raceday.com'),
        'Trail Run Challenge', '2026-09-20', 'Magaliesberg Trail Park',
        'An off-road trail event across scenic mountain terrain.', 'Published'),
    ((SELECT UserId FROM dbo.[User] WHERE Email = 'john.smith@raceday.com'),
        'Fun Run for Charity', '2026-10-05', 'Benoni Lake',
        'A family-friendly fun run raising funds for local charities.', 'Published');
GO

/* ---- Categories: at least one per Event (here, several each) --------- */
INSERT INTO dbo.Category (EventId, Name, DistanceKm, MaxParticipants, Price)
VALUES
    ((SELECT EventId FROM dbo.Event WHERE Name = 'City Marathon 2026'),  '5km',           5.00,  500, 150.00),
    ((SELECT EventId FROM dbo.Event WHERE Name = 'City Marathon 2026'),  '10km',         10.00,  500, 200.00),
    ((SELECT EventId FROM dbo.Event WHERE Name = 'City Marathon 2026'),  'Half Marathon',21.10,  300, 350.00),

    ((SELECT EventId FROM dbo.Event WHERE Name = 'Trail Run Challenge'), '10km Trail',   10.00,  200, 180.00),
    ((SELECT EventId FROM dbo.Event WHERE Name = 'Trail Run Challenge'), '21km Trail',   21.00,  150, 300.00),

    ((SELECT EventId FROM dbo.Event WHERE Name = 'Fun Run for Charity'), '5km Fun Run',   5.00,  400, 100.00),
    ((SELECT EventId FROM dbo.Event WHERE Name = 'Fun Run for Charity'), '2km Kids Run',  2.00,  200,  50.00);
GO

/* ---- Enrolments: sample Participant sign-ups -------------------------- */
INSERT INTO dbo.Enrolment (ParticipantId, CategoryId, BibNumber, Status)
VALUES
    ((SELECT UserId FROM dbo.[User] WHERE Email = 'mike.johnson@example.com'),
        (SELECT CategoryId FROM dbo.Category WHERE Name = '5km' AND EventId = (SELECT EventId FROM dbo.Event WHERE Name = 'City Marathon 2026')),
        1001, 'Confirmed'),
    ((SELECT UserId FROM dbo.[User] WHERE Email = 'emma.brown@example.com'),
        (SELECT CategoryId FROM dbo.Category WHERE Name = '10km' AND EventId = (SELECT EventId FROM dbo.Event WHERE Name = 'City Marathon 2026')),
        1002, 'Confirmed'),
    ((SELECT UserId FROM dbo.[User] WHERE Email = 'mike.johnson@example.com'),
        (SELECT CategoryId FROM dbo.Category WHERE Name = '10km Trail' AND EventId = (SELECT EventId FROM dbo.Event WHERE Name = 'Trail Run Challenge')),
        2001, 'Confirmed'),
    ((SELECT UserId FROM dbo.[User] WHERE Email = 'emma.brown@example.com'),
        (SELECT CategoryId FROM dbo.Category WHERE Name = '5km Fun Run' AND EventId = (SELECT EventId FROM dbo.Event WHERE Name = 'Fun Run for Charity')),
        3001, 'Confirmed');
GO

/* ---- Results: sample finish data for a couple of enrolments ----------- */
INSERT INTO dbo.Result (EnrolmentId, FinishTime, Position, Status)
VALUES
    ((SELECT EnrolmentId FROM dbo.Enrolment WHERE BibNumber = 1001), '00:24:37', 1, 'Finished'),
    ((SELECT EnrolmentId FROM dbo.Enrolment WHERE BibNumber = 1002), '00:52:14', 1, 'Finished');
GO

/* =====================================================================
   VERIFICATION — display the contents of every table after seeding.
   Optional: lets you visually confirm the data looks correct in SSMS.
   ===================================================================== */
SELECT * FROM dbo.Role;
SELECT * FROM dbo.[User];
SELECT * FROM dbo.Event;
SELECT * FROM dbo.Category;
SELECT * FROM dbo.Enrolment;
SELECT * FROM dbo.Result;
GO

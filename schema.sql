IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'RaceDayDB')
BEGIN
    CREATE DATABASE RaceDayDB;
END
GO

USE RaceDayDB;
GO

DROP TABLE IF EXISTS Results;
DROP TABLE IF EXISTS Enrolments;
DROP TABLE IF EXISTS Categories;
DROP TABLE IF EXISTS Events;
DROP TABLE IF EXISTS Users;
DROP TABLE IF EXISTS Roles;
GO

CREATE TABLE Roles (
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL UNIQUE
);
GO

CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    RoleId INT NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(150) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);
GO

CREATE TABLE Events (
    EventId INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserId INT NOT NULL,
    Title NVARCHAR(150) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    EventDate DATETIME NOT NULL,
    Location NVARCHAR(150) NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserId) REFERENCES Users(UserId)
);
GO

CREATE TABLE Categories (
    CategoryId INT IDENTITY(1,1) PRIMARY KEY,
    EventId INT NOT NULL,
    CategoryName NVARCHAR(100) NOT NULL,
    DistanceKM DECIMAL(5,2) NOT NULL CHECK (DistanceKM > 0),
    Fee DECIMAL(10,2) NOT NULL DEFAULT 0.00 CHECK (Fee >= 0),
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId) REFERENCES Events(EventId) ON DELETE CASCADE
);
GO

CREATE TABLE Enrolments (
    EnrolmentId INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantId INT NOT NULL,
    CategoryId INT NOT NULL,
    EnrolmentDate DATETIME DEFAULT GETDATE(),
    PaymentStatus NVARCHAR(30) DEFAULT 'Pending' CHECK (PaymentStatus IN ('Pending', 'Paid', 'Cancelled')),
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantId) REFERENCES Users(UserId),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    CONSTRAINT UQ_Participant_Category UNIQUE (ParticipantId, CategoryId)
);
GO

CREATE TABLE Results (
    ResultId INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentId INT NOT NULL UNIQUE,
    FinishTimeSeconds INT NOT NULL CHECK (FinishTimeSeconds > 0),
    Position INT NOT NULL CHECK (Position > 0),
    RecordedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES Enrolments(EnrolmentId)
);
GO

INSERT INTO Roles (RoleName) VALUES ('Organiser'), ('Participant');

INSERT INTO Users (RoleId, FullName, Email, PasswordHash) VALUES 
(1, 'Jayson Matshitela', 'jayson.organiser@raceday.co.za', 'hashedpassword123'),
(1, 'Lerato Khumalo', 'lerato.organiser@raceday.co.za', 'hashedpassword456'),
(2, 'Kabelo Sithole', 'kabelo.participant@raceday.co.za', 'hashedpassword789'),
(2, 'Nomvula Dlamini', 'nomvula.participant@raceday.co.za', 'hashedpassword012');

INSERT INTO Events (OrganiserId, Title, Description, EventDate, Location) VALUES 
(1, 'Joburg City Marathon 2026', 'Annual road marathon through Johannesburg central.', '2026-10-15 06:00:00', 'Johannesburg'),
(1, 'Durban Coastal Trail Run', 'Scenic coastal trail running event along the beach front.', '2026-11-20 07:00:00', 'Durban'),
(2, 'Cape Town Cycle & Run Classic', 'Multi-discipline endurance event across Table Mountain routes.', '2026-12-05 05:30:00', 'Cape Town');

INSERT INTO Categories (EventId, CategoryName, DistanceKM, Fee) VALUES 
(1, 'Full Marathon', 42.20, 350.00),
(1, 'Half Marathon', 21.10, 250.00),
(2, 'Trail 10K', 10.00, 150.00),
(2, 'Trail 5K Fun Run', 5.00, 80.00),
(3, 'Ultra Distance', 50.00, 500.00);

INSERT INTO Enrolments (ParticipantId, CategoryId, PaymentStatus) VALUES 
(3, 1, 'Paid'),
(3, 3, 'Paid'),
(4, 2, 'Paid'),
(4, 4, 'Pending');

INSERT INTO Results (EnrolmentId, FinishTimeSeconds, Position) VALUES 
(1, 12600, 1),
(3, 5400, 2);
GO
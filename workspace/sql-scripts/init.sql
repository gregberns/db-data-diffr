-- Create new database
IF NOT EXISTS (SELECT name
FROM MASTER.DBO.SYSDATABASES
WHERE name = N'testdb')
CREATE DATABASE testdb;
GO

-- Switch to that database
USE testdb;
GO

-- ==========================================
IF NOT EXISTS (SELECT *
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo'
    AND TABLE_NAME = 'Logs')
BEGIN
    CREATE TABLE Logs
    (
        LogId int NOT NULL IDENTITY
        ,message varchar(255)
        ,date_added varchar(255)
    );
END

TRUNCATE TABLE Logs

INSERT INTO Logs
    (message, date_added)
VALUES
    ("Database failed" ,"2020-09-20")
    ,("Application fault" ,"2020-09-21")

-- ==========================================
IF NOT EXISTS (SELECT *
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo'
    AND TABLE_NAME = 'Persons')
BEGIN
    CREATE TABLE Persons
    (
        PersonId int NOT NULL IDENTITY PRIMARY KEY
        ,LastName varchar(255)
        ,FirstName varchar(255)
        ,Address1 varchar(255)
        ,City varchar(255)
        ,Country varchar(255)
        ,EmailAddr varchar(255)
        ,PhoneNumber varchar(255)
        ,Gender varchar(255)
    );
END

TRUNCATE TABLE Persons

INSERT INTO Persons
    (LastName, FirstName, Address1, City, Country, EmailAddr, PhoneNumber, Gender)
VALUES
    ("Berns" ,"Greg" ,"123 Main" ,"Mesa" ,"United States" ,"gb@email.com" ,"738-348-2032" ,"male")
    ,("Doe" ,"Jane" ,"234 Temple" ,"Detroit" ,"United States" ,"jd@email.com" ,"345-324-6456"  ,"female")

-- ==========================================
IF NOT EXISTS (SELECT *
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo'
    AND TABLE_NAME = 'Jobs')
BEGIN
    CREATE TABLE Jobs
    (
        JobId int NOT NULL IDENTITY PRIMARY KEY
        ,Name varchar(255)
        ,date_added datetime
    );
END

TRUNCATE TABLE Jobs

INSERT INTO Jobs
    (Name, date_added)
VALUES
    ('Fire fighter' ,'2020-10-01')
    ,('Police officer' ,'2020-08-01')
-- ==========================================

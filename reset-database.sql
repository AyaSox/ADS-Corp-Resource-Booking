-- Reset database and reseed data

-- Clear existing data (in correct order to handle foreign keys)
DELETE FROM [Notifications];
DELETE FROM [Bookings];
DELETE FROM [Resources];
DELETE FROM [AspNetUserRoles];
DELETE FROM [AspNetUserLogins];
DELETE FROM [AspNetUserClaims];
DELETE FROM [AspNetUserTokens];
DELETE FROM [AspNetUsers];
DELETE FROM [AspNetRoles];

-- Reset identity counters
DBCC CHECKIDENT ('Resources', RESEED, 0);
DBCC CHECKIDENT ('Bookings', RESEED, 0);
DBCC CHECKIDENT ('Notifications', RESEED, 0);

PRINT 'Database cleared successfully. Please restart the application to reseed data.';
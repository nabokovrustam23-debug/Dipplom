CREATE TABLE "Branches" (
    "BranchId" INTEGER NOT NULL CONSTRAINT "PK_Branches" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Address" TEXT NOT NULL,
    "Latitude" REAL NULL,
    "Longitude" REAL NULL,
    "Phone" TEXT NULL,
    "ImageUrl" TEXT NULL,
    "OpeningTime" TEXT NOT NULL,
    "ClosingTime" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT "CK_Branches_Hours" CHECK (ClosingTime > OpeningTime),
    CONSTRAINT "CK_Branches_IsActive" CHECK (IsActive IN (0,1))
);


CREATE TABLE "Persona" (
    "PersonaId" INTEGER NOT NULL CONSTRAINT "PK_Persona" PRIMARY KEY AUTOINCREMENT,
    "LastName" TEXT NOT NULL,
    "FirstName" TEXT NOT NULL,
    "MiddleName" TEXT NULL,
    "Phone" TEXT NOT NULL,
    "Email" TEXT NULL,
    "BirthDate" TEXT NULL,
    "Gender" TEXT NULL,
    CONSTRAINT "CK_Persona_Gender" CHECK (Gender IS NULL OR Gender IN ('М','Ж'))
);


CREATE TABLE "Roles" (
    "RoleId" INTEGER NOT NULL CONSTRAINT "PK_Roles" PRIMARY KEY AUTOINCREMENT,
    "Code" TEXT NOT NULL,
    "Name" TEXT NOT NULL
);


CREATE TABLE "Services" (
    "ServiceId" INTEGER NOT NULL CONSTRAINT "PK_Services" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "DurationMinutes" INTEGER NOT NULL,
    "Price" NUMERIC NOT NULL,
    "ImageUrl" TEXT NULL,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT "CK_Services_Duration" CHECK (DurationMinutes > 0),
    CONSTRAINT "CK_Services_IsActive" CHECK (IsActive IN (0,1)),
    CONSTRAINT "CK_Services_Price" CHECK (Price >= 0)
);


CREATE TABLE "Clients" (
    "ClientId" INTEGER NOT NULL CONSTRAINT "PK_Clients" PRIMARY KEY AUTOINCREMENT,
    "PersonaId" INTEGER NOT NULL,
    "Source" TEXT NULL,
    "Notes" TEXT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT ((datetime('now'))),
    CONSTRAINT "FK_Clients_Persona_PersonaId" FOREIGN KEY ("PersonaId") REFERENCES "Persona" ("PersonaId") ON DELETE RESTRICT
);


CREATE TABLE "ConsentLog" (
    "ConsentId" INTEGER NOT NULL CONSTRAINT "PK_ConsentLog" PRIMARY KEY AUTOINCREMENT,
    "PersonaId" INTEGER NOT NULL,
    "ConsentType" TEXT NOT NULL,
    "AcceptedAt" TEXT NOT NULL DEFAULT ((datetime('now'))),
    "IpAddress" TEXT NULL,
    "UserAgent" TEXT NULL,
    CONSTRAINT "CK_ConsentLog_Type" CHECK (ConsentType IN ('PersonalData','Marketing')),
    CONSTRAINT "FK_ConsentLog_Persona_PersonaId" FOREIGN KEY ("PersonaId") REFERENCES "Persona" ("PersonaId") ON DELETE RESTRICT
);


CREATE TABLE "Masters" (
    "MasterId" INTEGER NOT NULL CONSTRAINT "PK_Masters" PRIMARY KEY AUTOINCREMENT,
    "PersonaId" INTEGER NOT NULL,
    "BranchId" INTEGER NOT NULL,
    "Position" TEXT NOT NULL,
    "HireDate" TEXT NOT NULL,
    "AvatarPath" TEXT NULL,
    "Bio" TEXT NULL,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT "CK_Masters_IsActive" CHECK (IsActive IN (0,1)),
    CONSTRAINT "FK_Masters_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("BranchId") ON DELETE RESTRICT,
    CONSTRAINT "FK_Masters_Persona_PersonaId" FOREIGN KEY ("PersonaId") REFERENCES "Persona" ("PersonaId") ON DELETE RESTRICT
);


CREATE TABLE "Users" (
    "UserId" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
    "PersonaId" INTEGER NOT NULL,
    "RoleId" INTEGER NOT NULL,
    "BranchId" INTEGER NULL,
    "Login" TEXT NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "PasswordSalt" TEXT NOT NULL,
    "PasswordIterations" INTEGER NOT NULL DEFAULT 100000,
    "IsEmailConfirmed" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "CreatedAt" TEXT NOT NULL DEFAULT ((datetime('now'))),
    "LastLoginAt" TEXT NULL,
    CONSTRAINT "CK_Users_IsActive" CHECK (IsActive IN (0,1)),
    CONSTRAINT "CK_Users_IsEmailConfirmed" CHECK (IsEmailConfirmed IN (0,1)),
    CONSTRAINT "FK_Users_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("BranchId") ON DELETE RESTRICT,
    CONSTRAINT "FK_Users_Persona_PersonaId" FOREIGN KEY ("PersonaId") REFERENCES "Persona" ("PersonaId") ON DELETE RESTRICT,
    CONSTRAINT "FK_Users_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("RoleId") ON DELETE RESTRICT
);


CREATE TABLE "Bookings" (
    "BookingId" INTEGER NOT NULL CONSTRAINT "PK_Bookings" PRIMARY KEY AUTOINCREMENT,
    "ClientId" INTEGER NOT NULL,
    "MasterId" INTEGER NOT NULL,
    "ServiceId" INTEGER NOT NULL,
    "BranchId" INTEGER NOT NULL,
    "StartDateTime" TEXT NOT NULL,
    "DurationMinutes" INTEGER NOT NULL,
    "PriceSnapshot" NUMERIC NOT NULL,
    "LoyaltyDiscountPercent" NUMERIC NOT NULL DEFAULT '0.0',
    "LoyaltyDiscountReason" TEXT NOT NULL DEFAULT 'None',
    "Status" TEXT NOT NULL DEFAULT 'Created',
    "Source" TEXT NOT NULL DEFAULT 'Online',
    "CancelReason" TEXT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT ((datetime('now'))),
    "UpdatedAt" TEXT NOT NULL DEFAULT ((datetime('now'))),
    CONSTRAINT "CK_Bookings_Duration" CHECK (DurationMinutes > 0),
    CONSTRAINT "CK_Bookings_LoyaltyDiscount" CHECK (LoyaltyDiscountPercent >= 0 AND LoyaltyDiscountPercent <= 100),
    CONSTRAINT "CK_Bookings_Price" CHECK (PriceSnapshot >= 0),
    CONSTRAINT "CK_Bookings_Source" CHECK (Source IN ('Online','Admin','Lead')),
    CONSTRAINT "CK_Bookings_Status" CHECK (Status IN ('Created','Confirmed','Cancelled','Completed','NoShow')),
    CONSTRAINT "FK_Bookings_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("BranchId") ON DELETE RESTRICT,
    CONSTRAINT "FK_Bookings_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("ClientId") ON DELETE RESTRICT,
    CONSTRAINT "FK_Bookings_Masters_MasterId" FOREIGN KEY ("MasterId") REFERENCES "Masters" ("MasterId") ON DELETE RESTRICT,
    CONSTRAINT "FK_Bookings_Services_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES "Services" ("ServiceId") ON DELETE RESTRICT
);


CREATE TABLE "MasterService" (
    "MasterId" INTEGER NOT NULL,
    "ServiceId" INTEGER NOT NULL,
    CONSTRAINT "PK_MasterService" PRIMARY KEY ("MasterId", "ServiceId"),
    CONSTRAINT "FK_MasterService_Masters_MasterId" FOREIGN KEY ("MasterId") REFERENCES "Masters" ("MasterId") ON DELETE CASCADE,
    CONSTRAINT "FK_MasterService_Services_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES "Services" ("ServiceId") ON DELETE CASCADE
);


CREATE TABLE "WorkSchedules" (
    "WorkScheduleId" INTEGER NOT NULL CONSTRAINT "PK_WorkSchedules" PRIMARY KEY AUTOINCREMENT,
    "MasterId" INTEGER NOT NULL,
    "BranchId" INTEGER NOT NULL,
    "WorkDate" TEXT NOT NULL,
    "StartTime" TEXT NOT NULL,
    "EndTime" TEXT NOT NULL,
    "ScheduleType" TEXT NOT NULL,
    CONSTRAINT "CK_WorkSchedules_Times" CHECK (EndTime > StartTime),
    CONSTRAINT "CK_WorkSchedules_Type" CHECK (ScheduleType IN ('Work','Lunch','DayOff','Vacation','SickLeave')),
    CONSTRAINT "FK_WorkSchedules_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("BranchId") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkSchedules_Masters_MasterId" FOREIGN KEY ("MasterId") REFERENCES "Masters" ("MasterId") ON DELETE RESTRICT
);


CREATE TABLE "AuditLog" (
    "AuditId" INTEGER NOT NULL CONSTRAINT "PK_AuditLog" PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NULL,
    "Action" TEXT NOT NULL,
    "EntityType" TEXT NOT NULL,
    "EntityId" INTEGER NULL,
    "Details" TEXT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT ((datetime('now'))),
    "IpAddress" TEXT NULL,
    CONSTRAINT "FK_AuditLog_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE SET NULL
);


CREATE TABLE "UserSessions" (
    "SessionId" INTEGER NOT NULL CONSTRAINT "PK_UserSessions" PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NOT NULL,
    "Token" TEXT NOT NULL,
    "ExpiresAt" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT ((datetime('now'))),
    "RevokedAt" TEXT NULL,
    "UserAgent" TEXT NULL,
    "IpAddress" TEXT NULL,
    CONSTRAINT "FK_UserSessions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE
);


CREATE TABLE "UserTokens" (
    "TokenId" INTEGER NOT NULL CONSTRAINT "PK_UserTokens" PRIMARY KEY AUTOINCREMENT,
    "UserId" INTEGER NOT NULL,
    "Purpose" TEXT NOT NULL,
    "Token" TEXT NOT NULL,
    "ExpiresAt" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT ((datetime('now'))),
    "ConsumedAt" TEXT NULL,
    CONSTRAINT "CK_UserTokens_Purpose" CHECK (Purpose IN ('PasswordReset')),
    CONSTRAINT "FK_UserTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE
);


CREATE TABLE "Leads" (
    "LeadId" INTEGER NOT NULL CONSTRAINT "PK_Leads" PRIMARY KEY AUTOINCREMENT,
    "PersonaId" INTEGER NULL,
    "RawName" TEXT NOT NULL,
    "RawPhone" TEXT NOT NULL,
    "PreferredBranchId" INTEGER NULL,
    "Comment" TEXT NULL,
    "Status" TEXT NOT NULL DEFAULT 'New',
    "CreatedBookingId" INTEGER NULL,
    "ProcessedByUserId" INTEGER NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT ((datetime('now'))),
    "ProcessedAt" TEXT NULL,
    CONSTRAINT "CK_Leads_Status" CHECK (Status IN ('New','InProgress','Done','Rejected')),
    CONSTRAINT "FK_Leads_Bookings_CreatedBookingId" FOREIGN KEY ("CreatedBookingId") REFERENCES "Bookings" ("BookingId") ON DELETE SET NULL,
    CONSTRAINT "FK_Leads_Branches_PreferredBranchId" FOREIGN KEY ("PreferredBranchId") REFERENCES "Branches" ("BranchId") ON DELETE SET NULL,
    CONSTRAINT "FK_Leads_Persona_PersonaId" FOREIGN KEY ("PersonaId") REFERENCES "Persona" ("PersonaId") ON DELETE SET NULL,
    CONSTRAINT "FK_Leads_Users_ProcessedByUserId" FOREIGN KEY ("ProcessedByUserId") REFERENCES "Users" ("UserId") ON DELETE SET NULL
);


CREATE TABLE "Visits" (
    "VisitId" INTEGER NOT NULL CONSTRAINT "PK_Visits" PRIMARY KEY AUTOINCREMENT,
    "BookingId" INTEGER NOT NULL,
    "TotalAmount" NUMERIC NOT NULL,
    "MasterNotes" TEXT NULL,
    "CompletedAt" TEXT NOT NULL DEFAULT ((datetime('now'))),
    CONSTRAINT "CK_Visits_Total" CHECK (TotalAmount >= 0),
    CONSTRAINT "FK_Visits_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("BookingId") ON DELETE RESTRICT
);


INSERT INTO "Branches" ("BranchId", "Address", "ClosingTime", "ImageUrl", "IsActive", "Latitude", "Longitude", "Name", "OpeningTime", "Phone")
VALUES (1, 'Краснодар, ул. Красная, 32', '22:00:00', NULL, 1, NULL, NULL, 'Тихий час — Центр', '10:00:00', '+7 (861) 200-10-10');
SELECT changes();

INSERT INTO "Branches" ("BranchId", "Address", "ClosingTime", "ImageUrl", "IsActive", "Latitude", "Longitude", "Name", "OpeningTime", "Phone")
VALUES (2, 'Краснодар, ул. Тургенева, 138', '21:00:00', NULL, 1, NULL, NULL, 'Тихий час — Фестивальный', '09:00:00', '+7 (861) 200-10-11');
SELECT changes();



INSERT INTO "Roles" ("RoleId", "Code", "Name")
VALUES (1, 'Owner', 'Владелец сети');
SELECT changes();

INSERT INTO "Roles" ("RoleId", "Code", "Name")
VALUES (2, 'Admin', 'Администратор филиала');
SELECT changes();

INSERT INTO "Roles" ("RoleId", "Code", "Name")
VALUES (3, 'Master', 'Мастер');
SELECT changes();

INSERT INTO "Roles" ("RoleId", "Code", "Name")
VALUES (4, 'Client', 'Клиент');
SELECT changes();



INSERT INTO "Services" ("ServiceId", "Description", "DurationMinutes", "ImageUrl", "IsActive", "Name", "Price")
VALUES (1, 'Классическая мужская стрижка ножницами и машинкой.', 60, NULL, 1, 'Мужская стрижка', '1500.0');
SELECT changes();

INSERT INTO "Services" ("ServiceId", "Description", "DurationMinutes", "ImageUrl", "IsActive", "Name", "Price")
VALUES (2, 'Короткая стрижка одной длиной.', 30, NULL, 1, 'Стрижка машинкой', '800.0');
SELECT changes();

INSERT INTO "Services" ("ServiceId", "Description", "DurationMinutes", "ImageUrl", "IsActive", "Name", "Price")
VALUES (3, 'Классическое бритьё с горячим полотенцем.', 45, NULL, 1, 'Бритьё опасной бритвой', '1200.0');
SELECT changes();

INSERT INTO "Services" ("ServiceId", "Description", "DurationMinutes", "ImageUrl", "IsActive", "Name", "Price")
VALUES (4, 'Моделирование контура и подравнивание бороды.', 30, NULL, 1, 'Стрижка бороды', '700.0');
SELECT changes();

INSERT INTO "Services" ("ServiceId", "Description", "DurationMinutes", "ImageUrl", "IsActive", "Name", "Price")
VALUES (5, 'Тонирование седины в бороде.', 30, NULL, 1, 'Камуфляж бороды', '900.0');
SELECT changes();



CREATE INDEX "IX_AuditLog_Created" ON "AuditLog" ("CreatedAt");


CREATE INDEX "IX_AuditLog_UserId" ON "AuditLog" ("UserId");


CREATE INDEX "IX_Bookings_Branch_Start" ON "Bookings" ("BranchId", "StartDateTime");


CREATE INDEX "IX_Bookings_Client" ON "Bookings" ("ClientId");


CREATE UNIQUE INDEX "IX_Bookings_Master_Start" ON "Bookings" ("MasterId", "StartDateTime") WHERE Status IN ('Created','Confirmed');


CREATE INDEX "IX_Bookings_ServiceId" ON "Bookings" ("ServiceId");


CREATE UNIQUE INDEX "IX_Clients_PersonaId" ON "Clients" ("PersonaId");


CREATE INDEX "IX_ConsentLog_PersonaId" ON "ConsentLog" ("PersonaId");


CREATE INDEX "IX_Leads_CreatedBookingId" ON "Leads" ("CreatedBookingId");


CREATE INDEX "IX_Leads_PersonaId" ON "Leads" ("PersonaId");


CREATE INDEX "IX_Leads_PreferredBranchId" ON "Leads" ("PreferredBranchId");


CREATE INDEX "IX_Leads_ProcessedByUserId" ON "Leads" ("ProcessedByUserId");


CREATE INDEX "IX_Leads_Status_Created" ON "Leads" ("Status", "CreatedAt");


CREATE INDEX "IX_Masters_Branch" ON "Masters" ("BranchId");


CREATE UNIQUE INDEX "IX_Masters_PersonaId" ON "Masters" ("PersonaId");


CREATE INDEX "IX_MasterService_ServiceId" ON "MasterService" ("ServiceId");


CREATE UNIQUE INDEX "UX_Persona_Phone" ON "Persona" ("Phone");


CREATE UNIQUE INDEX "IX_Roles_Code" ON "Roles" ("Code");


CREATE INDEX "IX_Users_Branch" ON "Users" ("BranchId");


CREATE UNIQUE INDEX "IX_Users_Login" ON "Users" ("Login");


CREATE UNIQUE INDEX "IX_Users_PersonaId" ON "Users" ("PersonaId");


CREATE INDEX "IX_Users_RoleId" ON "Users" ("RoleId");


CREATE UNIQUE INDEX "IX_UserSessions_Token" ON "UserSessions" ("Token");


CREATE INDEX "IX_UserSessions_User" ON "UserSessions" ("UserId");


CREATE UNIQUE INDEX "IX_UserTokens_Token" ON "UserTokens" ("Token");


CREATE INDEX "IX_UserTokens_User_Purpose" ON "UserTokens" ("UserId", "Purpose");


CREATE UNIQUE INDEX "IX_Visits_BookingId" ON "Visits" ("BookingId");


CREATE INDEX "IX_WorkSchedules_BranchId" ON "WorkSchedules" ("BranchId");


CREATE INDEX "IX_WorkSchedules_Master_Date" ON "WorkSchedules" ("MasterId", "WorkDate");


CREATE UNIQUE INDEX "UQ_WorkSchedules_Slot" ON "WorkSchedules" ("MasterId", "WorkDate", "StartTime");



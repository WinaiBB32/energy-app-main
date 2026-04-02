-- ============================================================
-- สร้างตาราง ElectricityBills (บิลค่าไฟฟ้า กฟน.)
-- ============================================================
CREATE TABLE IF NOT EXISTS "ElectricityBills" (
    "Id"                uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    "DocReceiveNumber"  text        NOT NULL DEFAULT '',
    "DocNumber"         text        NOT NULL DEFAULT '',
    "BuildingId"        uuid        NULL,
    "BillingCycle"      timestamp   NULL,
    "PeaUnitUsed"       numeric     NOT NULL DEFAULT 0,
    "PeaAmount"         numeric     NOT NULL DEFAULT 0,
    "FtRate"            numeric     NOT NULL DEFAULT 0,
    "Note"              text        NOT NULL DEFAULT '',
    "RecordedBy"        text        NOT NULL DEFAULT '',
    "DepartmentId"      text        NOT NULL DEFAULT '',
    "CreatedAt"         timestamp   NOT NULL DEFAULT NOW()
);

-- ============================================================
-- สร้างตาราง SolarProductions (ข้อมูลการผลิต Solar)
-- ============================================================
CREATE TABLE IF NOT EXISTS "SolarProductions" (
    "Id"                uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    "BuildingId"        uuid        NULL,
    "RecordDate"        timestamp   NULL,
    "SolarUnitProduced" numeric     NOT NULL DEFAULT 0,
    "ProductionWh"      numeric     NOT NULL DEFAULT 0,
    "ToBatteryWh"       numeric     NOT NULL DEFAULT 0,
    "ToGridWh"          numeric     NOT NULL DEFAULT 0,
    "ToHomeWh"          numeric     NOT NULL DEFAULT 0,
    "ConsumptionWh"     numeric     NOT NULL DEFAULT 0,
    "FromBatteryWh"     numeric     NOT NULL DEFAULT 0,
    "FromGridWh"        numeric     NOT NULL DEFAULT 0,
    "FromSolarWh"       numeric     NOT NULL DEFAULT 0,
    "Note"              text        NOT NULL DEFAULT '',
    "RecordedBy"        text        NOT NULL DEFAULT '',
    "DepartmentId"      text        NOT NULL DEFAULT '',
    "CreatedAt"         timestamp   NOT NULL DEFAULT NOW()
);

-- ============================================================
-- ย้ายข้อมูลเก่าจาก ElectricityRecords (ถ้ามี)
-- ============================================================

-- ย้าย PEA_BILL → ElectricityBills
INSERT INTO "ElectricityBills"
    ("Id","DocReceiveNumber","DocNumber","BuildingId","BillingCycle",
     "PeaUnitUsed","PeaAmount","FtRate","Note","RecordedBy","DepartmentId","CreatedAt")
SELECT
    "Id","DocReceiveNumber","DocNumber","BuildingId","BillingCycle",
    "PeaUnitUsed","PeaAmount","FtRate","Note","RecordedBy","DepartmentId","CreatedAt"
FROM "ElectricityRecords"
WHERE "Type" = 'PEA_BILL'
ON CONFLICT ("Id") DO NOTHING;

-- ย้าย SOLAR_PRODUCTION → SolarProductions
INSERT INTO "SolarProductions"
    ("Id","BuildingId","RecordDate","SolarUnitProduced",
     "ProductionWh","ToBatteryWh","ToGridWh","ToHomeWh",
     "ConsumptionWh","FromBatteryWh","FromGridWh","FromSolarWh",
     "Note","RecordedBy","DepartmentId","CreatedAt")
SELECT
    "Id","BuildingId","RecordDate","SolarUnitProduced",
    "ProductionWh","ToBatteryWh","ToGridWh","ToHomeWh",
    "ConsumptionWh","FromBatteryWh","FromGridWh","FromSolarWh",
    "Note","RecordedBy","DepartmentId","CreatedAt"
FROM "ElectricityRecords"
WHERE "Type" = 'SOLAR_PRODUCTION'
ON CONFLICT ("Id") DO NOTHING;

-- ============================================================
-- บันทึก migration ใน EF history
-- ============================================================
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260402000002_CreateElectricityBillsAndSolarProductions', '8.0.0')
ON CONFLICT DO NOTHING;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'extensions') THEN
        CREATE SCHEMA extensions;
    END IF;
END $EF$;

CREATE EXTENSION IF NOT EXISTS postgis SCHEMA extensions;

CREATE TABLE "Territories" (
    "Id" uuid NOT NULL,
    "OwnerUserId" text NOT NULL,
    "Boundary" geometry(Polygon,4326) NOT NULL,
    "AreaSquareMeters" double precision NOT NULL,
    "ShieldExpiresAt" timestamp with time zone,
    "Status" text NOT NULL,
    "MergedIntoTerritoryId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Territories" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Territories_AspNetUsers_OwnerUserId" FOREIGN KEY ("OwnerUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_Territories_Boundary" ON "Territories" USING gist ("Boundary");

CREATE INDEX "IX_Territories_OwnerUserId" ON "Territories" ("OwnerUserId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261025080940_AddTerritories', '8.0.31');

COMMIT;


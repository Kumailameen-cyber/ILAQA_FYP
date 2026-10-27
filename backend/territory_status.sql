START TRANSACTION;

ALTER TABLE "Territories" ALTER COLUMN "Status" TYPE character varying(20);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261025085304_SetTerritoryStatusMaxLength', '8.0.31');

COMMIT;


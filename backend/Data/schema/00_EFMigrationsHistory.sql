-- ============================================================
-- __EFMigrationsHistory.sql
-- EF Core 迁移历史表（自动管理，不要手动改）
-- ============================================================

CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
"MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

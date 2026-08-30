-- ============================================================
-- init.sql - 总入口
-- 用途：生产环境初始化数据库时执行此文件
-- 用法：psql -h <host> -U <user> -d <db> -f init.sql
--
-- 本文件由 build 脚本自动生成，请勿手动改
-- ============================================================

\i 00_EFMigrationsHistory.sql
\i 01_summaries.sql
\i 02_search_history.sql

#!/usr/bin/env python3
"""
拆分 schema.sql 到 schema/ 目录的独立表文件
- 每个 CREATE TABLE 块一个文件
- 自动生成 init.sql 使用 psql include 引用

用法:
  python split-schema.py
"""
import re
import sys
from pathlib import Path

CREATE_TABLE_RE = re.compile(
    r'CREATE TABLE(?:\s+IF NOT EXISTS)?\s+["\']?(\w+)["\']?\s*\((.*?)\);',
    re.DOTALL | re.IGNORECASE,
)

CREATE_INDEX_RE = re.compile(
    r'CREATE\s+(?:UNIQUE\s+)?INDEX\s+(?:IF NOT EXISTS\s+)?["\']?(\w+)["\']?\s+ON\s+["\']?(\w+)["\']?\s*\(([^)]+)\);',
    re.IGNORECASE,
)

# 表名 → 文件名映射（EF Migrations History 太长，去掉双下划线）
TABLE_FILENAMES = {
    "__EFMigrationsHistory": "EFMigrationsHistory",
    "summaries": "summaries",
    "search_history": "search_history",
}

# 表中文注释（用于 schema 文件头部）
TABLE_COMMENTS = {
    "__EFMigrationsHistory": "EF Core 迁移历史表（自动管理，不要手动改）",
    "summaries": "财报摘要缓存表（TTL 24h）",
    "search_history": "搜索历史表（按 resolved_stock_code 去重）",
}


def split_table_block(table_name: str, body: str) -> str:
    """生成单个表的 schema 文件"""
    comment = TABLE_COMMENTS.get(table_name, "")
    header = f"-- ============================================================\n-- {table_name}.sql\n"
    if comment:
        header += f"-- {comment}\n"
    header += "-- ============================================================\n\n"
    return f"{header}CREATE TABLE IF NOT EXISTS {table_name} (\n{body}\n);\n"


def main(schema_sql_path: str = None, schema_dir: str = None) -> int:
    # Tools/split-schema.py → backend/
    base = Path(__file__).resolve().parent.parent
    if schema_sql_path is None:
        schema_sql_path = base / "Data" / "schema.sql"
    else:
        schema_sql_path = Path(schema_sql_path)
    if schema_dir is None:
        schema_dir = base / "Data" / "schema"
    else:
        schema_dir = Path(schema_dir)

    schema_dir.mkdir(parents=True, exist_ok=True)

    sql = schema_sql_path.read_text(encoding="utf-8-sig")

    # 收集所有 CREATE TABLE 块
    tables = {}
    for m in CREATE_TABLE_RE.finditer(sql):
        table_name = m.group(1)
        body = m.group(2).strip()
        tables[table_name] = body

    # 收集所有 CREATE INDEX 块，按表名归类
    indexes_by_table = {}
    for m in CREATE_INDEX_RE.finditer(sql):
        idx_name, table_name, columns = m.group(1), m.group(2), m.group(3).strip()
        indexes_by_table.setdefault(table_name, []).append((idx_name, columns))

    # 写每个表文件
    order = ["__EFMigrationsHistory", "summaries", "search_history"]
    written = []
    for i, table_name in enumerate(order):
        if table_name not in tables:
            continue
        body = tables[table_name]
        content = split_table_block(table_name, body)
        if table_name in indexes_by_table:
            content += "\n-- 索引\n"
            for idx_name, columns in indexes_by_table[table_name]:
                content += f'CREATE INDEX IF NOT EXISTS {idx_name} ON {table_name} ({columns});\n'
        file_name = TABLE_FILENAMES.get(table_name, table_name)
        out = schema_dir / f"{i:02d}_{file_name}.sql"
        out.write_text(content, encoding="utf-8")
        written.append(out.name)

    # 写 init.sql
    init_lines = [
        "-- ============================================================",
        "-- init.sql - 总入口",
        "-- 用途：生产环境初始化数据库时执行此文件",
        "-- 用法：psql -h <host> -U <user> -d <db> -f init.sql",
        "--",
        "-- 本文件由 build 脚本自动生成，请勿手动改",
        "-- ============================================================",
        "",
    ]
    for f in written:
        init_lines.append(f"\\i {f}")
    init_lines.append("")
    (schema_dir / "init.sql").write_text("\n".join(init_lines), encoding="utf-8")

    print(f"[OK] 拆分完成，写入 {len(written)} 个文件到 {schema_dir}/")
    for f in written:
        print(f"  - {f}")
    print(f"  - init.sql")
    return 0


if __name__ == "__main__":
    args = sys.argv[1:]
    schema_sql = args[0] if len(args) > 0 else None
    schema_out = args[1] if len(args) > 1 else None
    sys.exit(main(schema_sql, schema_out))

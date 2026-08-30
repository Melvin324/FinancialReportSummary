SELECT "Id", query, resolved_stock_code, resolved_company_name, searched_at
FROM search_history
ORDER BY "Id" DESC
LIMIT 10;

@echo off
set PGPASSWORD=admin
psql -h localhost -p 5432 -U postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'RestoreDB';"
dropdb -h localhost -p 5432 -U postgres --if-exists RestoreDB

@echo off
set PGPASSWORD=admin
createdb -h localhost -p 5432 -U postgres RestoreDB
pg_restore -h localhost -p 5432 -U postgres -d RestoreDB "C:\Users\yasemin\Desktop\projeler\DBClone\DBClone\bin\Debug\net5.0\db_backup.dump"

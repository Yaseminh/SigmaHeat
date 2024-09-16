@echo off
set PGPASSWORD=admin
pg_dump -h localhost -p 5432 -U postgres -d fortest -F c -b -v -f "C:\Users\yasemin\Desktop\projeler\DBClone\DBClone\bin\Debug\net5.0\db_backup.dump"

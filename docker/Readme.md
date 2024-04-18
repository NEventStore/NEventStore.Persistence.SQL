# Docker

Docker must be configured to use LCOW in Windows OS.

Run the following scripts to start and stop all the Database engines:

```
DockerComposeUp.ps1
DockerComposeDown.ps1
```

## Docker Environment Variables

NEventStore.MsSql="Server=localhost,50001;Database=NEventStore;User Id=sa;Password=Password1;TrustServerCertificate=True;"
NEventStore.MongoDB="mongodb://localhost:50002/NEventStore"
NEventStore.MySql="Server=localhost;Port=50003;Database=NEventStore;Uid=sa;Pwd=Password1;AutoEnlist=false;"
NEventStore.PostgreSql="Server=localhost;Port=50004;Database=NEventStore;Uid=sa;Pwd=Password1;Enlist=false;"

## To be tested

NEventStore.Oracle="Data Source=[host]:1521/XE;User Id=system;Password=NEventStore;Persist Security Info=True;"
# Database Connection Guide

This document explains how to set up and connect to the local SQL Server database (`localdb`) which is already configured for this development environment.

## 1. Verify LocalDB is Installed

You can check if LocalDB is installed and running on your machine by opening a terminal and running:

```bash
sqllocaldb info
```

You should see an output that includes `MSSQLLocalDB`.

## 2. Check the Configuration

The application is already configured to use the LocalDB instance. You can verify this in the following files:

**`WebDevProject/appsettings.json`**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=Db;Trusted_Connection=true;MultipleActiveResultSets=true;"
  }
}
```

**`WebDevProject/Program.cs`**:
```csharp
// The provider is already set to SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
```
*(No changes are needed here if you are using LocalDB).*

## 3. Apply Database Migrations

To create the database and required tables, you need to apply Entity Framework Core migrations.

1. **Install the EF Core CLI Tool** (if you haven't already globally installed it):
   ```bash
   dotnet tool install --global dotnet-ef
   ```

2. **Create the Initial Migration**:
   Run this from the root of the project to generate the migration files:
   ```bash
   dotnet ef migrations add InitialCreate --project WebDevProject
   ```

3. **Update the Database**:
   Apply the migration to the LocalDB instance:
   ```bash
   dotnet ef database update --project WebDevProject
   ```

After these steps, the local database will be fully set up and ready to use!

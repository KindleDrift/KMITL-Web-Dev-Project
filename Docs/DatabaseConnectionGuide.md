# Database Connection Guide

This document explains how to configure the application to connect to a real database. Currently, it is configured to use a local SQL Server (`localdb`) instance for development (and it was an `InMemory` database prior to that).

## 1. Update the Connection String

The application reads its database connection string from the `appsettings.json` file. You need to update the `DefaultConnection` to point to your real database instance.

Open `WebDevProject/appsettings.json` and replace the existing connection string:

```json
{
  "ConnectionStrings": {
    // Replace the string below with your real/production database connection string
    "DefaultConnection": "Server=YOUR_SERVER_ADDRESS;Database=YOUR_DATABASE_NAME;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  },
  // ... other settings
}
```

*Note: The exact format of the connection string depends on your database provider (e.g., SQL Server, MySQL, PostgreSQL).*

## 2. Check the Database Provider in `Program.cs`

If your real database is a **Microsoft SQL Server** (or Azure SQL Server), you **do not** need to change any code. The application is already configured to use SQL Server in `WebDevProject/Program.cs`:

```csharp
// WebDevProject/Program.cs (Around Line 16)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
```

### Changing to a Different Database Provider (e.g., PostgreSQL, MySQL, SQLite)

If your real database is **not** SQL Server, you must update the database provider to match your system:

1. **Install the required NuGet package** via the terminal directly into the project:
   - **PostgreSQL:** `dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL`
   - **MySQL:** `dotnet add package Pomelo.EntityFrameworkCore.MySql`
   - **SQLite:** `dotnet add package Microsoft.EntityFrameworkCore.Sqlite`
   
2. **Modify `Program.cs`** to use the new provider (make sure to replace `options.UseSqlServer(connectionString)`):

   **For PostgreSQL:**
   ```csharp
   builder.Services.AddDbContext<ApplicationDbContext>(options =>
       options.UseNpgsql(connectionString));
   ```

   **For MySQL:**
   ```csharp
   var serverVersion = new MySqlServerVersion(new Version(8, 0, 31)); // Adjust to your MySQL version
   builder.Services.AddDbContext<ApplicationDbContext>(options =>
       options.UseMySql(connectionString, serverVersion));
   ```

   **For SQLite:**
   ```csharp
   builder.Services.AddDbContext<ApplicationDbContext>(options =>
       options.UseSqlite(connectionString));
   ```

## 3. Apply Database Migrations

Once your application is pointing to the real database, you need to verify that all the tables have been created correctly. If you are starting fresh with a real SQL Server database, apply the Entity Framework core migrations to populate the DB schema.

Open your terminal, navigate to the `WebDevProject` directory, and run:

```bash
dotnet ef database update
```

*(Note: If you changed your database provider entirely, for example from SQL Server to PostgreSQL, you will need to delete the existing `Migrations` folder in the project, create a new initial migration using `dotnet ef migrations add InitialCreate`, and then run `database update`.)*

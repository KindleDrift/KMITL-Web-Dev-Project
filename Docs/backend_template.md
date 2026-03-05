# Backend

## 1. Overview
This project backend is built with ASP.NET Core MVC.

- Controllers handle requests and return views.
- Models hold data for forms and identity users.
- Database access is provided by Entity Framework Core.
- The app currently uses `UseInMemoryDatabase("InMemoryDb")` in development.

## 2. MVC in This Project
Current controller examples:

- `AccountController` for sign in, sign up, onboarding, sign out.
- `ProfileController` for profile pages.
- `BoardController` for board pages.
- `HomeController` for default pages.

Simple MVC request flow:

1. Browser requests `/Account/SignIn`.
2. `AccountController.SignIn()` (GET) returns the Razor view.
3. User submits form to `AccountController.SignIn(SignInViewModel model)` (POST).
4. Controller validates model and returns either redirect or same view with errors.

## 3. Dependency Registration
In `Program.cs`, services are registered in the DI container:

```csharp
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
	options.UseInMemoryDatabase("InMemoryDb"));

builder.Services.AddIdentity<Users, IdentityRole>()
	.AddEntityFrameworkStores<ApplicationDbContext>()
	.AddDefaultTokenProviders();
```

This means controllers can receive `UserManager<Users>`, `SignInManager<Users>`, and other services through constructor injection.

## 4. Database Usage (In-Memory)
### 4.1 Current state
`ApplicationDbContext` inherits from `IdentityDbContext<Users>`, so identity tables are managed by EF Core.

```csharp
public class ApplicationDbContext : IdentityDbContext<Users>
{
	public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
		: base(options)
	{
	}
}
```

### 4.2 Seeding data
`DbSeeder.SeedAsync(app.Services)` runs at startup.

It currently:

- Ensures database is created.
- Creates roles (`Admin`, `User`) if missing.
- Creates default admin user if missing.

Because this is an in-memory database, data is not persisted permanently between app restarts.

## 5. Working with Controllers + Database
### 5.1 Read current user (already used in project)
```csharp
[HttpGet]
public async Task<IActionResult> Index()
{
	var user = await _userManager.GetUserAsync(User);
	if (user == null)
	{
		return RedirectToAction("SignIn", "Account");
	}

	return View(user);
}
```

### 5.2 Update user data (simple pattern)
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UpdateBio(string bio)
{
	var user = await _userManager.GetUserAsync(User);
	if (user == null)
	{
		return RedirectToAction("SignIn", "Account");
	}

	user.Bio = bio;
	await _userManager.UpdateAsync(user);

	return RedirectToAction("Index");
}
```

## 6. Optional: Add Your Own Entity Later
If you later add app-specific data (for example board posts), you can:

1. Add model class in `Models/`.
2. Add `DbSet<T>` in `ApplicationDbContext`.
3. Inject `ApplicationDbContext` into a controller and query with EF Core.

Example shape:
```csharp
public DbSet<BoardPost> BoardPosts => Set<BoardPost>();
```

Keep it simple first: start with one model, one controller, one view page.

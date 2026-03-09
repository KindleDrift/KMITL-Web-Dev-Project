# WebDevProject Documentation

This is a high-level documentation of the ASP.NET Core MVC project (**KMITL-Web-Dev-Project / KindleDrift**), describing the primary purpose of the components across the standard MVC architecture.

## 1. Controllers (WebDevProject/Controllers)
Controllers handle incoming HTTP requests, process user inputs, interact with the services or database, and return the appropriate Views.

* **AccountController.cs**: Manages user authentication. Handles sign-up, sign-in, sign-out, onboarding flow, and password changes.
* **AdminController.cs**: Handles administrative tasks. Deals with managing boards from an admin perspective, with access protected by custom `AdminOnly` authorization policies.
* **BoardController.cs**: The core feature controller. Handles the creation, reading, updating, and deleting of "Boards" (posts or events). Manages board applications, participant lists, and board life-cycles.
* **HomeController.cs**: Serves the landing/home pages and general informational routes (e.g., error pages).
* **NotificationsController.cs**: Provides views and data for user notifications (e.g., when a board status changes or someone applies).
* **ProfileController.cs**: Handles viewing and editing user profiles, uploading profile images, and showing user-specific data.

## 2. Models & ViewModels (WebDevProject/Models)
Models represent the data structures and database entities, while ViewModels are used to pass specific data shapes between Controllers and Views.

* **Entities**: `Board.cs` (Represents a post/event), `Users.cs` (Inherits from `IdentityUser` for custom user fields), `Notification.cs` (Stores user notifications).
* **ViewModels**: These are strongly typed models strictly for transferring data to/from views safely:
  * *Account/Profile*: `SignInViewModel.cs`, `SignUpViewModel.cs`, `OnboardingViewModel.cs`, `ChangePasswordViewModel.cs`, `EditProfileViewModel.cs`, `UserProfileViewModel.cs`.
  * *Board*: `BoardCreateViewModel.cs`, `BoardDetailsViewModel.cs`, `BoardIndexViewModel.cs`.
  * *Other*: `ErrorViewModel.cs`.

## 3. Services (WebDevProject/Services)
Services contain business logic and ensure "Separation of Concerns" (so complex logic doesn't bloat Controllers or Views). 

* **BoardService.cs**: Manages the core operations for creating, editing, and closing boards.
* **BoardMembershipService.cs**: Manages user interactions with boards, such as joining, leaving, approving, or rejecting participants.
* **BoardDisplayService.cs**: A presentation utility handling board presentation logic (e.g., calculating statuses, visible participant limits, open spots).
* **NotificationsService.cs**: Manages the creation and retrieval of user notifications in the database.
* **NotificationFormattingService.cs**: A specialized service to format how notifications look visually (badges, text formats, relative times) without cluttering the UI code.
* **ProfileImageService.cs**: Handles image file uploading, processing, saving, and URL generation for user profile pictures.

## 4. Database & Data Access (WebDevProject/Data)
* **ApplicationDbContext.cs**: The core Entity Framework Core context linking C# models to the SQL database tables.
* **DbSeeder.cs**: A utility that populates the database with initial/dummy data on application startup (useful for development and testing).

## 5. Views (WebDevProject/Views)
The UI components represented as Razor pages (`.cshtml`).

* **Account, Admin, Board, Home, Notifications, Profile**: Folders mapping to their respective Controllers. They contain the HTML templates for the pages.
* **Shared**: Contains shared layouts (`_Layout.cshtml`), partial views (like navigation bars), and common components used across multiple pages.

## 6. Filters & Helpers
* **RequireOnboardingFilter.cs** (WebDevProject/Filters): Action filter that intercepts requests and redirects users to an "Onboarding" page if they haven't completed their initial account setup.
* **TimeZoneHelper.cs** (WebDevProject/Helpers): Utility class to safely convert UTC database times to correct local time zones for display.

## 7. Static Assets (WebDevProject/wwwroot)
The public-facing static files served directly to the browser.
* **css/**: Stylesheets for the application.
* **js/**: Client-side JavaScript logic.
* **images/**: Static application images.
* **uploads/**: A directory commonly used for user-uploaded content (e.g., profile pictures dynamically stored here).

## 8. Configuration Setup (Program.cs & appsettings.json)
* **Program.cs**: The entry point. Handles dependency injection (registering scoped Services), configures Identity (passwords/authentication rules), sets cookie routing, and maps MVC routes.
* **appsettings.json**: Stores environment variables like the database Connection String.

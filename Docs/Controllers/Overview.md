# Controllers Documentation

Controllers in ASP.NET Core MVC are responsible for handling incoming HTTP requests, executing the appropriate application logic (often handled by Services), and returning responses (typically HTML Views or JSON).

This document explains what each controller in `WebDevProject/Controllers/` controls and what dependencies it requires.

---

## 1. AccountController (`AccountController.cs`)
Controls user authentication, registration, and initial account setup ("Onboarding").

* **What it controls**: 
  * `SignIn` / `SignOut`: Logging users in and out.
  * `SignUp`: Registering new accounts and validating uniqueness of emails/usernames.
  * `Onboarding`: Forcing users to complete their profile (bio, gender, profile picture, DOB) before accessing the app.
  * Async AJAX endpoints to check if display names/emails exist in real-time.
* **Dependencies Required**:
  * `SignInManager<Users>`, `UserManager<Users>`, `RoleManager<IdentityRole>` (Core Identity services for authentication management).
  * `ProfileImageService` (To handle image uploads during onboarding).

## 2. AdminController (`AdminController.cs`)
Controls the administrative dashboard and privileged actions. All actions are restricted by the `[Authorize(Policy = "AdminOnly")]` attribute.

* **What it controls**:
  * `Users`: Viewing all users, editing user profiles as an admin, banning, and unbanning accounts.
  * `Boards`: Viewing all boards, editing board configurations without restriction, and changing board statuses (or completely cancelling them).
* **Dependencies Required**:
  * `ApplicationDbContext` (Direct database access).
  * `UserManager<Users>` (User management).
  * `NotificationsService` (To notify users if an admin cancels their board or changes their application status).
  * `BoardService` & `ProfileImageService` (For handling file uploads and board logical updates).

## 3. BoardController (`BoardController.cs`)
Controls the core "events/posts" feature of the application. It dictates the life-cycle of a `Board` and how users interact with it. Required to be logged in and onboarded.

* **What it controls**:
  * `Index`: Displaying lists of active boards, participating boards, and joined boards.
  * `Search` / `SearchBoards`: Dynamic searching and filtering of boards (allows anonymous access).
  * `Create` / `Edit` / `Cancel` / `Archive`: The lifecycle of a Board.
  * `Details`: Viewing a specific board page with participants list.
  * **Membership Actions**: `Apply`, `CancelMyApplication`, `ApproveApplicant`, `DenyApplicant`, `DenyParticipant`, `AddExternalParticipant`.
* **Dependencies Required**:
  * `ApplicationDbContext`
  * `BoardService` (Managing board creations, tags, status updates).
  * `BoardMembershipService` (Complex logic for joining, applying, approving, and rejecting users).

## 4. HomeController (`HomeController.cs`)
Controls the static and default routes of the app.

* **What it controls**:
  * `Index`: The landing page when a user hits the base URL `/`.
  * `About` / `Privacy`: Informational pages.
  * `Error`: Centralized error handling and 404/500 page rendering. Returns a custom 404 for unauthenticated admin route access.
* **Dependencies Required**: None beyond the default Controller base.

## 5. NotificationsController (`NotificationsController.cs`)
Controls the user's notification inbox and real-time alert interactions.

* **What it controls**:
  * `Index`: Displaying the user's notification history.
  * `MarkAsRead`, `MarkAllAsRead`, `Delete`, `DeleteAll`: Actions to manage notification state.
  * `GetUnreadCount`, `GetRecentNotifications`: AJAX endpoints to populate the navigation bar badge and dropdown dynamically.
* **Dependencies Required**:
  * `NotificationsService` (To read/write notification entities).

## 6. ProfileController (`ProfileController.cs`)
Controls viewing, managing, and editing user profiles.

* **What it controls**:
  * `Index` / `Details`: Viewing a specific user's public profile, bio, created boards, and joined boards.
  * `Edit`: Updating personal bio, birth date, gender, and changing the profile picture.
  * `ChangePassword`: Updating the account password.
* **Dependencies Required**:
  * `UserManager<Users>`, `SignInManager<Users>`.
  * `ProfileImageService` (To handle replacing the old profile picture).

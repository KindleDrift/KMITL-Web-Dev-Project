# Services Documentation

The service layer contains the application's core business logic, decoupled from Razor Views and Controllers, improving reusability, modularity, and testability.

These services reside in the `WebDevProject/Services/` directory.

---

## 1. BoardService (`BoardService.cs`)
Handles fundamental board data operations, calculations, and static file management related to boards.

* **What it does**:
  * Saves board header images to the `uploads/boards/` path, ensuring basic validation (size, allowed extensions).
  * Generates the Search API's XML outputs for dynamic client-side consumption.
  * Validates and normalizes tags (applying maximum lengths, checking allowed characters, converting to Title Case), and binds them to boards.
  * Manages edge cases in board updates, such as converting `BoardJoinPolicy` from "Application" to "First Come First Serve" (auto-approving pending applicants if there's space).
  * Automatically recalibrates the board's status `Open`, `Full`, or `Closed` based on newly joined members and maximum capacities.
* **Requirements**: Depends on `ApplicationDbContext` and `IWebHostEnvironment` (for file saving).

## 2. BoardMembershipService (`BoardMembershipService.cs`)
Manages the complicated workflows concerning user associations (participants/applicants) and boards. All methods return a structured `BoardWorkflowResult` indicating Success, Error, Forbid, or NotFound states.

* **What it does**:
  * `ApplyAsync`: Ensures users can apply to boards given the current board state (deadlines, full states, join policies) and generates the request notification to the author.
  * `ApproveApplicantAsync` / `DenyApplicantAsync`: Allows an author to accept or reject an applicant, automatically creating the corresponding accept/reject notifications.
  * `AddExternalParticipantAsync` / `RemoveExternalParticipantAsync`: Handles users adding participants who do not have an actual website account (e.g., bringing a friend).
  * `CancelBoardAsync`: When an author manually cancels a board, this service strips all current participants out, denies all applicants, sets the board status, and provides the IDs to notify everyone affected.
* **Requirements**: Depends on `ApplicationDbContext`, `NotificationsService`, and `BoardService`.

## 3. BoardDisplayService (`BoardDisplayService.cs`)
A lightweight, presentation-focused service that prevents Views from repeating embedded C# calculation logic.

* **What it does**:
  * Calculates CSS badge classes dynamically based on the current board status (e.g., making "Open but past deadline" red).
  * Calculates human-readable board statuses.
  * Calculates how many spots are actually left (`MaxParticipants` minus `VisibleParticipants` minus `ExternalParticipants`).

## 4. NotificationsService (`NotificationsService.cs`)
A data-access service for reading, writing, and deleting `Notification` entities.

* **What it does**:
  * `CreateNotificationAsync` / `CreateNotificationsForMultipleUsersAsync`: Logs new notifications into the DB.
  * `MarkAsReadAsync` / `MarkAllAsReadAsync`: Updates read states.
  * `GetUserNotificationsAsync`: Retrieves a paginated list of notification history.

## 5. NotificationFormattingService (`NotificationFormattingService.cs`)
A presentation-focused service that strictly handles formatting Notification details for the UI.

* **What it does**:
  * Maps a notification's `DateTime` to a relative "Time Ago" string (e.g., "5m ago", "2h ago").
  * Assigns HTML badge component colors (`badge-info`, `badge-danger`) based on `NotificationType`.
  * Generates HTML hyperlink strings inside notification descriptions dynamically using C# Delegates.

## 6. ProfileImageService (`ProfileImageService.cs`)
Handles all user profile picture uploads.

* **What it does**:
  * Validates image format and ensures they are under 5MB.
  * Generates unique filenames (`{userId}_{GUID}.extension`) to avoid naming collisions and caches.
  * Deletes old profile pictures off the disk to save space when a user uploads a new one.
* **Requirements**: Depends heavily on `IWebHostEnvironment`.

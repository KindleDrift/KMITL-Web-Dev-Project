# Entity Models Documentation

This document explains the primary Entity models representing database tables in the application, including their properties and requirements. These entities reside in `WebDevProject/Models/`.

---

## 1. Board (`Board.cs`)
Represents an event or activity post created by a user.

### Properties:
* **Id**: `int` - Primary key for the board.
* **Title**: `string` - (**Required**, Max: 120 chars) The title of the board.
* **ImageUrl**: `string?` - (Max: 2048 chars) Optional URL pointing to a header/cover image for the board.
* **Tags**: `ICollection<Tag>` - List of tags categorized to the board.
* **Description**: `string` - (**Required**, Max: 2000 chars) Detailed description of the board/event.
* **AuthorId**: `string` - (**Required**) Foreign key identifying the user who created it.
* **Author**: `Users?` - Navigation property to the author's user entity.
* **MaxParticipants**: `int` - (Range: 1 to 1000, Default: 1) Maximum participants allowed.
* **Location**: `string` - (**Required**, Max: 200 chars) The whereabouts of the event.
* **EventDate**: `DateTime` - (**Required**) The scheduled date of the event.
* **Deadline**: `DateTime` - (**Required**) The deadline for joining or applying to the board.
* **CreatedAt**: `DateTime` - UTC timestamp when the board was created.
* **NotifyAuthorOnFull**: `bool` - Flag indicating whether the author should be notified when `MaxParticipants` is reached.
* **CurrentStatus**: `BoardStatus` - (Default: `Open`) Enum indicating board state (`Open`, `Full`, `Closed`, `Cancelled`, `Archived`).
* **GroupManagementOption**: `GroupManagement` - (Default: `CloseOnFull`) How to handle board limits (`CloseOnFull`, `AllowOverbooking`, `KeepOpenWhenFull`).
* **JoinPolicy**: `BoardJoinPolicy` - (Default: `Application`) How users join (`Application` or `FirstComeFirstServe`).

### Relational Properties (Collections):
* **Participants**: Users who joined successfully (`BoardParticipant`).
* **ExternalParticipants**: Manually added participants not tied to an account (`BoardExternalParticipant`).
* **Applicants**: Users applying to join (`BoardApplicant`).
* **DeniedUsers**: Users rejected from joining (`BoardDenied`).

---

## 2. Users (`Users.cs`)
Extends ASP.NET Core Identity's default `IdentityUser` to add custom application-specific fields for users.

### Custom Properties:
* **DisplayName**: `string` - (**Required**) The user's public display name.
* **NormalizedDisplayName**: `string` - (**Required**) Upper-case variant of display name for searching.
* **HasCompletedOnboarding**: `bool` - (Default: `false`) Indicates if the account finished the initial setup flow.
* **DateOfBirth**: `DateTime?` - Optional Date of birth.
* **UserGender**: `Gender?` - Enum (`Male`, `Female`, `Other`). Optional biological/preferred gender.
* **ProfilePictureUrl**: `string?` - The path to the uploaded profile image.
* **Bio**: `string?` - A short user biography.
* **CreatedAt**: `DateTime` - (**Required**) When the account was registered.

### Relational Properties (Collections):
* **AuthoredBoards**: Boards this user created.
* **BoardParticipations**: Boards this user successfully joined.
* **BoardApplications**: Boards this user applied to.
* **BoardDenials**: Boards this user was denied from.

---

## 3. Notification (`Notification.cs`)
Represents an alert sent to users regarding board updates or applications.

### Properties:
* **Id**: `int` - Primary Key.
* **Title**: `string?` - Notification title.
* **Description**: `string?` - Detailed notification message.
* **CreatedDate**: `DateTime` - (**Required**) UTC timestamp of alert.
* **Type**: `NotificationType` - Enum specifying the event (`NewRequest`, `IsAccepted`, `IsRejected`, `AdminAction`, `BoardFull`).
* **UserId**: `string` - (**Required**) ID of the user receiving the notification.
* **User**: `Users?` - Navigation property to receipt user.
* **IsRead**: `bool` - (Default: `false`) Whether the user has seen it.
* **BoardId**: `int?` - Associated Board ID if applicable.
* **Board**: `Board?` - Associated Board navigation property.
* **RelatedUserId**: `string?` - ID of another user triggering the alert (e.g., an applicant's ID).
* **RelatedUser**: `Users?` - Navigation property for internal routing.

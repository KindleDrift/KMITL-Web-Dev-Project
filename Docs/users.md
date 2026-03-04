# Users Model

The `Users` entity extends ASP.NET Identity's `IdentityUser` and adds profile/onboarding fields used by this project.

## Current Model Shape

### Inherited from `IdentityUser`
| Attribute | Type | Required | Unique | Notes |
|---|---|---|---|---|
| Id | string | Yes | Yes (PK) | Identity primary key. |
| UserName | string? | Yes (in this app's flow) | Yes (Identity username uniqueness) | Set to the same value as `Email` during signup. |
| NormalizedUserName | string? | Yes (in this app's flow) | Yes | Stored uppercase for lookups. |
| Email | string? | Yes (in this app's flow) | Yes (app-level identity validation) | `options.User.RequireUniqueEmail = true`. |
| NormalizedEmail | string? | Yes (in this app's flow) | Treated as unique by validation flow | Stored uppercase for lookups. |

### Custom properties in `Users`
| Attribute | Type | Required | Unique | Notes |
|---|---|---|---|---|
| DisplayName | string | Yes | Yes (application check) | User-facing handle. Checked in signup by `NormalizedDisplayName`. |
| NormalizedDisplayName | string | Yes | Yes (application check) | Uppercase value used for case-insensitive duplicate checks. |
| HasCompletedOnboarding | bool | Yes (default `false`) | No | Tracks onboarding completion state. |
| DateOfBirth | DateTime? | No | No | Optional onboarding profile data. |
| UserGender | `Users.Gender`? | No | No | Optional onboarding profile data (`Male`, `Female`, `Other`). |
| ProfilePictureUrl | string? | No | No | Optional URL path to uploaded profile image. |

#### Enum `Users.Gender`
Gender is defined as a nullable enum with values:
- `Male`
- `Female`
- `Other`

#### Extra notes on `DisplayName` and `NormalizedDisplayName`
`DisplayName` is the unique user-facing handle. In the current implementation, it (and the related profile properties above) are set during signup, and `NormalizedDisplayName` is computed as the uppercase version of `DisplayName` so the application can enforce case-insensitive uniqueness at creation time. There is currently no user-facing profile edit endpoint/UI in this codebase, so end users cannot change their `DisplayName` or other profile fields after signup unless additional functionality is added.

## Behavior and Constraints in Current Code

- Signup creates a `Users` object and sets:
  - `UserName = Email`
  - `NormalizedUserName = Email.ToUpper()`
  - `Email = Email`
  - `NormalizedEmail = Email.ToUpper()`
- Identity is configured with `RequireUniqueEmail = true`.
- Display name uniqueness is enforced in controller logic by checking `NormalizedDisplayName` before `CreateAsync`.

## Why `UserName` is intentionally the same as `Email`

This project uses email as the single login identifier, and setting `UserName` to email is intentional for consistency with ASP.NET Identity internals.

1. **Identity sign-in APIs are username-first by default**  
	`PasswordSignInAsync(model.Email, ...)` treats the first argument as *username*. Mapping username to email keeps login behavior correct without custom user-store logic.

2. **One canonical credential avoids split identity bugs**  
	If `UserName` and `Email` were different fields with different values, account lookup, duplicate validation, and login UX can drift. Keeping them equal makes account identity deterministic.

3. **Built-in normalization/index paths work as intended**  
	Identity relies heavily on normalized username/email lookups. Writing both normalized values at creation time (`ToUpper()`) keeps searches and validations fast and predictable.

4. **Cleaner duplicate-error handling in this codebase**  
	Because username and email represent the same value, duplicate account checks naturally converge on one identifier (the email), and the controller can suppress redundant duplicate-username messaging.

5. **Simpler authentication UX**  
	Users only need to remember one login identifier (email), while `DisplayName` remains a separate public profile handle.

## Related Code Sources

The documentation above reflects the current code in:
- `Models/Users.cs`
- `Controllers/AccountController.cs`
- `Program.cs`
# Frontend

## 1. Overview
This project uses ASP.NET Core MVC with Razor views.

Typical structure:
```
Views/
├── Account/
├── Board/
├── Home/
├── Profile/
└── Shared/
    ├── _Layout.cshtml
    ├── _LoginPartial.cshtml
    └── _ValidationScriptsPartial.cshtml
```

## 2. Shared Layout Usage
`_Layout.cshtml` is the common shell used by pages.

Simple page usage:
```cshtml
@{
    ViewData["Title"] = "Sign Up";
}

@section Styles {
    <link rel="stylesheet" href="~/css/signup.css" asp-append-version="true" />
}

<main>
    <h1>Create account</h1>
</main>

@section Scripts {
    <script src="~/js/signup.js" asp-append-version="true"></script>
}
```

## 3. Razor Sample Usage
### 3.1 Model binding in a view
```cshtml
@model WebDevProject.Models.SignInViewModel

<h2>@ViewData["Title"]</h2>
<p>Sign in with your account.</p>
```

### 3.2 Condition and loop
```cshtml
@if (User.Identity?.IsAuthenticated ?? false)
{
    <p>Welcome back!</p>
}

@foreach (var item in new[] { "Create", "Search", "Profile" })
{
    <span>@item</span>
}
```

### 3.3 Link generation with tag helpers
```cshtml
<a asp-controller="Account" asp-action="SignIn">Sign In</a>
<a asp-controller="Board" asp-action="Create">Create Post</a>
```

## 4. Form Sample Usage
Basic MVC form with validation:

```cshtml
@model WebDevProject.Models.SignUpViewModel

<form asp-controller="Account" asp-action="SignUp" method="post">
    @Html.AntiForgeryToken()

    <div asp-validation-summary="ModelOnly"></div>

    <label asp-for="DisplayName"></label>
    <input asp-for="DisplayName" />
    <span asp-validation-for="DisplayName"></span>

    <label asp-for="Email"></label>
    <input asp-for="Email" />
    <span asp-validation-for="Email"></span>

    <button type="submit">Register</button>
</form>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

## 5. Static Files
Static files are in `wwwroot`:
```
wwwroot/
├── css/
├── js/
├── images/
└── uploads/
```

Use `asp-append-version="true"` when referencing CSS/JS so browsers load updated files after changes.
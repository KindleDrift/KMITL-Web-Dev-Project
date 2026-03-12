
let nameCheckTimeout;
let emailCheckTimeout;

function checkDuplicateName() {
    clearTimeout(nameCheckTimeout);

    var name = document.getElementById("signup-name").value.trim();
    var nameError = document.getElementById("nameError");

    if (!name) {
        nameError.textContent = "";
        return;
    }

    nameCheckTimeout = setTimeout(async function () {
        try {
            const response = await fetch("/Account/CheckDisplayNameExist?displayname=" + encodeURIComponent(name));
            if (response.ok) {
                const isDuplicate = await response.json();
                if (isDuplicate) {
                    nameError.textContent = "This username is already taken.";
                } else {
                    nameError.textContent = "";
                }
            }
        } catch (error) {
            console.error("Error checking username:", error);
        }
    }, 500);
}

function checkDuplicateEmail() {
    clearTimeout(emailCheckTimeout);

    var email = document.getElementById("signup-email").value.trim();
    var emailError = document.getElementById("emailError");

    if (!email) {
        emailError.textContent = "";
        return;
    }

    emailCheckTimeout = setTimeout(async function () {
        try {
            const response = await fetch("/Account/CheckEmailExist?email=" + encodeURIComponent(email));
            if (response.ok) {
                const isDuplicate = await response.json();
                if (isDuplicate) {
                    emailError.textContent = "This email is already taken.";
                } else {
                    emailError.textContent = "";
                }
            }
        } catch (error) {
            console.error("Error checking email:", error);
        }
    }, 500);
}

function validatePassword() {
    var password = document.getElementById("password").value;
    var passwordError = document.getElementById("passwordError");
    var confirmPassword = document.getElementById("confirm-password").value;
    var confirmPasswordError = document.getElementById("confirmPasswordError");

    var errors = [];

    if (password.length < 8) {
        errors.push("at least 8 characters");
    }

    if (password.length > 100) {
        errors.push("maximum 100 characters");
    }

    if (!/[a-z]/.test(password)) {
        errors.push("one lowercase letter");
    }

    if (!/[A-Z]/.test(password)) {
        errors.push("one uppercase letter");
    }

    if (!/[0-9]/.test(password)) {
        errors.push("one number");
    }

    if (!/[^a-zA-Z0-9]/.test(password)) {
        errors.push("one special character");
    }

    if (errors.length > 0) {
        passwordError.textContent = "Password must contain " + errors.join(", ") + ".";
    } else {
        passwordError.textContent = "";
    }

    if (confirmPassword && password !== confirmPassword) {
        confirmPasswordError.textContent = "Passwords do not match.";
    } else {
        confirmPasswordError.textContent = "";
    }
}

function validateConfirmPassword() {
    var password = document.getElementById("password").value;
    var confirmPassword = document.getElementById("confirm-password").value;
    var confirmPasswordError = document.getElementById("confirmPasswordError");

    if (confirmPassword && password !== confirmPassword) {
        confirmPasswordError.textContent = "Passwords do not match.";
    } else {
        confirmPasswordError.textContent = "";
    }
}

document.addEventListener("DOMContentLoaded", function () {
    var nameInput = document.getElementById("signup-name");
    var emailInput = document.getElementById("signup-email");
    var passwordInput = document.getElementById("password");
    var confirmPasswordInput = document.getElementById("confirm-password");

    if (nameInput) {
        nameInput.addEventListener("keyup", checkDuplicateName);
    }

    if (emailInput) {
        emailInput.addEventListener("keyup", checkDuplicateEmail);
    }

    if (passwordInput) {
        passwordInput.addEventListener("keyup", validatePassword);
    }

    if (confirmPasswordInput) {
        confirmPasswordInput.addEventListener("keyup", function () {
            validatePassword();
            validateConfirmPassword();
        });
    }
});
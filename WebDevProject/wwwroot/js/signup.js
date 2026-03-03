// AJAX for signup auto dupe name and email check on keyup
function checkDuplicateName() {
    var name = document.getElementById("signup-name").value;
    var xhr = new XMLHttpRequest();
    xhr.open("POST", "/Account/CheckDisplayNameExist", true);
    xhr.setRequestHeader("Content-Type", "application/json");
    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4 && xhr.status === 200) {
            var response = JSON.parse(xhr.responseText);
            var nameError = document.getElementById("nameError");
            if (response.isDuplicate) {
                nameError.textContent = "This name is already taken.";
            } else {
                nameError.textContent = "";
            }
        }
    };
    xhr.send(JSON.stringify({ displayname: name }));
}

function checkDuplicateEmail() {
    var email = document.getElementById("signup-email").value;
    var xhr = new XMLHttpRequest();
    xhr.open("POST", "/Account/CheckEmailExist", true);
    xhr.setRequestHeader("Content-Type", "application/json");
    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4 && xhr.status === 200) {
            var response = JSON.parse(xhr.responseText);
            var emailError = document.getElementById("emailError");
            if (response.isDuplicate) {
                emailError.textContent = "This email is already taken.";
            } else {
                emailError.textContent = "";
            }
        }
    };
    xhr.send(JSON.stringify({ email: email }));
}
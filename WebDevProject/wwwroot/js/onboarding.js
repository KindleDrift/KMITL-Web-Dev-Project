document.getElementById('ProfileImage').addEventListener('change', function (event) {
    const file = event.target.files[0];
    if (file) {
        const reader = new FileReader();
        reader.onload = function (e) {
            document.getElementById('profile-pic-preview').src = e.target.result;
        };
        reader.readAsDataURL(file);
    }
});

document.getElementById('skip-onboarding-btn').addEventListener('click', function () {
    document.getElementById('SkipOnboarding').value = 'true';
    document.getElementById('signin-form').submit();
});

document.getElementById('signin-form').addEventListener('submit', function (e) {
    return true;
});
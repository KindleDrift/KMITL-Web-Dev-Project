document.addEventListener('DOMContentLoaded', function () {
    const fileInput = document.getElementById("imageInput");
    const btn = document.getElementById("changePicBtn");
    const preview = document.getElementById("preview");
    var picture

    btn.addEventListener("click", () => {
        fileInput.click();
    });

    fileInput.addEventListener("change", () => {
        const file = fileInput.files[0];
        if (file) {
            picture = URL.createObjectURL(file);
            preview.src = picture
        }
    });
});
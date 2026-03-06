document.addEventListener('DOMContentLoaded', function () {
    const tags = [];

    function createTagElement(tagText) {
        const tag = document.createElement('span');
        tag.className = 'tag';
        tag.innerHTML = `${tagText} <button type="button" class="btn-remove" style="font-size: 0.75rem;"></button>`;
        
        const removeBtn = tag.querySelector('button');
        removeBtn.addEventListener('click', function (e) {
            e.preventDefault();
            tags.splice(tags.indexOf(tagText), 1);
            tag.remove();
            updateTagsInput();
        });
        
        return tag;
    }

    function updateTagsInput() {
        // Create individual hidden inputs for each tag
        const existingInputs = document.querySelectorAll('input[name="Tags"]');
        existingInputs.forEach(input => input.remove());
        
        const boardForm = document.getElementById('boardForm');
        tags.forEach(tag => {
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = 'Tags';
            input.value = tag;
            boardForm.appendChild(input);
        });
    }

    const tagInput = document.getElementById('tagInput');
    const addTagButton = document.getElementById('addTagButton');
    const tagContainer = document.getElementById('tagContainer');

    if (tagInput && addTagButton) {
        // Add tag on button click
        addTagButton.addEventListener('click', function (e) {
            e.preventDefault();
            const tagText = tagInput.value.trim();
            if (tagText && !tags.includes(tagText)) {
                tags.push(tagText);
                tagContainer.appendChild(createTagElement(tagText));
                tagInput.value = '';
                updateTagsInput();
            }
        });

        // Add tag on Enter key
        tagInput.addEventListener('keypress', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                const tagText = this.value.trim();
                if (tagText && !tags.includes(tagText)) {
                    tags.push(tagText);
                    tagContainer.appendChild(createTagElement(tagText));
                    this.value = '';
                    updateTagsInput();
                }
            }
        });
    }

    // Form submission validation
    const boardForm = document.getElementById('boardForm');
    if (boardForm) {
        boardForm.addEventListener('submit', function (e) {
            // Ensure group management option is selected
            const groupOption = document.querySelector('input[name="GroupManagementOption"]:checked');
            if (!groupOption) {
                e.preventDefault();
                alert('Please select a group management option');
            }
        });
    }
});
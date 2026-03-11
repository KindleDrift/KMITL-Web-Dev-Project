document.addEventListener('DOMContentLoaded', function () {
    const tags = [];

    function toDateTimeLocalValue(date) {
        const pad = (value) => String(value).padStart(2, '0');
        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
    }

    function applyClientTimezoneToForm() {
        const timezoneOffsetInput = document.getElementById('clientTimeZoneOffsetMinutes');
        if (timezoneOffsetInput) {
            timezoneOffsetInput.value = String(new Date().getTimezoneOffset());
        }

        const utcDateInputs = document.querySelectorAll('input[type="datetime-local"][data-utc-value]');
        utcDateInputs.forEach(input => {
            const utcValue = input.getAttribute('data-utc-value');
            if (!utcValue) {
                return;
            }

            const utcDate = new Date(`${utcValue}Z`);
            if (Number.isNaN(utcDate.getTime())) {
                return;
            }

            input.value = toDateTimeLocalValue(utcDate);
        });
    }

    applyClientTimezoneToForm();

    // Tag validation and formatting functions
    function isValidTag(tag) {
        if (tag.startsWith('-') || tag.endsWith('-')) {
            return false;
        }

        // if tag contain numbers
        if (/\d/.test(tag)) {
            return false;
        }

        // if tag contains only letters and single hyphens
        for (let i = 0; i < tag.length; i++) {
            const c = tag[i];
            
            // Allow letters
            if (/[a-zA-Z]/.test(c)) {
                continue;
            }

            // Allow single hyphen (not consecutive)
            if (c === '-') {
                if (i > 0 && tag[i - 1] === '-') {
                    return false;
                }
                continue;
            }

            // Any other character is invalid
            return false;
        }

        return true;
    }

    function formatTag(tag) {
        // Convert to lowercase first
        tag = tag.toLowerCase();

        // Capitalize first letter
        if (tag.length > 0) {
            tag = tag.charAt(0).toUpperCase() + tag.slice(1);
        }

        return tag;
    }

    function createTagElement(tagText = '') {
        const tag = document.createElement('span');
        tag.className = 'tag';
        tag.setAttribute('data-tag', tagText);
        tag.innerHTML = `${tagText} <button type="button" class="btn-remove" style="font-size: 0.75rem;">×</button>`;
        
        const removeBtn = tag.querySelector('button');
        removeBtn.addEventListener('click', function (e) {
            e.preventDefault();
            const index = tags.findIndex(t => t.toLowerCase() === tagText.toLowerCase());
            if (index > -1) {
                tags.splice(index, 1);
            }
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
        if (!boardForm) {
            return;
        }

        tags.forEach(tag => {
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = 'Tags';
            input.value = tag;
            boardForm.appendChild(input);
        });
    }

    function addTag(tagText) {
        tagText = tagText.trim();
        
        if (!tagText) {
            return;
        }

        // Validate tag
        if (!isValidTag(tagText)) {
            alert('Invalid tag! Tags must contain only letters and single hyphens (not at start or end, no numbers).');
            return;
        }

        // Format tag
        const formattedTag = formatTag(tagText);

        // Check if tag already exists (case-insensitive)
        if (tags.some(t => t.toLowerCase() === formattedTag.toLowerCase())) {
            alert('Tag already added!');
            return;
        }

        tags.push(formattedTag);
        tagContainer.appendChild(createTagElement(formattedTag));
        updateTagsInput();
    }

    const tagInput = document.getElementById('tagInput');
    const addTagButton = document.getElementById('addTagButton');
    const tagContainer = document.getElementById('tagContainer');

    if (tagContainer) {
        const existingTagElements = Array.from(tagContainer.querySelectorAll('.tag'));
        tagContainer.innerHTML = '';

        existingTagElements.forEach(tagElement => {
            const rawTag = tagElement.getAttribute('data-tag') || tagElement.textContent || '';
            const normalizedTag = formatTag(rawTag.trim());
            if (!normalizedTag) {
                return;
            }

            if (!tags.some(t => t.toLowerCase() === normalizedTag.toLowerCase())) {
                tags.push(normalizedTag);
                tagContainer.appendChild(createTagElement(normalizedTag));
            }
        });

        updateTagsInput();
    }

    if (tagInput && addTagButton) {
        // Add tag on button click
        addTagButton.addEventListener('click', function (e) {
            e.preventDefault();
            const tagText = tagInput.value;
            addTag(tagText);
            tagInput.value = '';
        });

        // Add tag on Enter key
        tagInput.addEventListener('keypress', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                const tagText = this.value;
                addTag(tagText);
                this.value = '';
            }
        });
    }

    // Form submission validation
    const boardForm = document.getElementById('boardForm');
    if (boardForm) {
        boardForm.addEventListener('submit', function (e) {
            const joinPolicyOption = document.querySelector('input[name="JoinPolicyOption"]:checked');
            if (!joinPolicyOption) {
                e.preventDefault();
                alert('Please select a join system');
                return;
            }

            // Ensure group management option is selected
            const groupOption = document.querySelector('input[name="GroupManagementOption"]:checked');
            if (!groupOption) {
                e.preventDefault();
                alert('Please select a group management option');
            }
        });
    }
});
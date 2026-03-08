document.addEventListener('DOMContentLoaded', function () {
    const searchNameInput = document.getElementById('searchName');
    const hideOptionButton = document.getElementById('hideOptionButton');
    const hidden_items = document.querySelectorAll(".hidden");
    const tagInput = document.getElementById('tagInput');
    const addTagButton = document.getElementById('addTagButton');
    const tagContainer = document.getElementById('tagContainer');
    const eventDateFromInput = document.getElementById('eventDateFrom');
    const eventDateToInput = document.getElementById('eventDateTo');
    const searchButton = document.getElementById('searchButton');
    const clearButton = document.getElementById('clearButton');
    const searchResults = document.getElementById('searchResults');
    const statusFilters = document.querySelectorAll('.status-filter');
    const policyFilters = document.querySelectorAll('.policy-filter');

    const selectedTags = [];

    // Hide/Show more search option
    hideOptionButton.addEventListener('click', function (e) {
        hidden_items.forEach(item => {
            item.classList.toggle("hidden");
        });
    });

    // Tag validation and formatting functions (reused from boardcreate.js)
    function isValidTag(tag) {
        if (tag.startsWith('-') || tag.endsWith('-')) {
            return false;
        }

        if (/\d/.test(tag)) {
            return false;
        }

        for (let i = 0; i < tag.length; i++) {
            const c = tag[i];
            
            if (/[a-zA-Z]/.test(c)) {
                continue;
            }

            if (c === '-') {
                if (i > 0 && tag[i - 1] === '-') {
                    return false;
                }
                continue;
            }

            return false;
        }

        return true;
    }

    function formatTag(tag) {
        tag = tag.toLowerCase();

        if (tag.length > 0) {
            tag = tag.charAt(0).toUpperCase() + tag.slice(1);
        }

        return tag;
    }

    function createTagElement(tagText) {
        const tag = document.createElement('span');
        tag.className = 'tag';
        tag.setAttribute('data-tag', tagText);
        tag.innerHTML = `${tagText} <button type="button" class="btn-remove" style="font-size: 0.75rem;">×</button>`;
        
        const removeBtn = tag.querySelector('button');
        removeBtn.addEventListener('click', function (e) {
            e.preventDefault();
            const index = selectedTags.findIndex(t => t.toLowerCase() === tagText.toLowerCase());
            if (index > -1) {
                selectedTags.splice(index, 1);
            }
            tag.remove();
        });
        
        return tag;
    }

    function addTag(tagText) {
        tagText = tagText.trim();
        
        if (!tagText) {
            return;
        }

        if (!isValidTag(tagText)) {
            alert('Invalid tag! Tags must contain only letters and single hyphens (not at start or end, no numbers).');
            return;
        }

        const formattedTag = formatTag(tagText);

        if (selectedTags.some(t => t.toLowerCase() === formattedTag.toLowerCase())) {
            alert('Tag already added!');
            return;
        }

        selectedTags.push(formattedTag);
        tagContainer.appendChild(createTagElement(formattedTag));
    }

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
            const tagText = tagInput.value;
            addTag(tagText);
            tagInput.value = '';
        }
    });

    // Get selected statuses
    function getSelectedStatuses() {
        const selected = [];
        statusFilters.forEach(checkbox => {
            if (checkbox.checked) {
                selected.push(checkbox.value);
            }
        });
        return selected.join(',');
    }

    // Get selected join policies
    function getSelectedPolicies() {
        const selected = [];
        policyFilters.forEach(checkbox => {
            if (checkbox.checked) {
                selected.push(checkbox.value);
            }
        });
        return selected.join(',');
    }

    // Helper function to get text content from XML element
    function getXmlText(element, tagName) {
        const node = element.getElementsByTagName(tagName)[0];
        return node ? node.textContent : '';
    }

    // Helper function to parse participant elements
    function parseParticipants(participantsElement) {
        const participants = [];
        if (participantsElement) {
            const participantNodes = participantsElement.getElementsByTagName('Participant');
            for (let i = 0; i < participantNodes.length; i++) {
                participants.push({
                    profilePictureUrl: getXmlText(participantNodes[i], 'ProfilePictureUrl'),
                    displayName: getXmlText(participantNodes[i], 'DisplayName')
                });
            }
        }
        return participants;
    }

    // Helper function to parse tag elements
    function parseTags(tagsElement) {
        const tags = [];
        if (tagsElement) {
            const tagNodes = tagsElement.getElementsByTagName('Tag');
            for (let i = 0; i < tagNodes.length; i++) {
                tags.push(tagNodes[i].textContent);
            }
        }
        return tags;
    }

    // Parse XML response and convert to board objects
    function parseXmlResponse(xmlDoc) {
        const boards = [];
        const boardElements = xmlDoc.getElementsByTagName('Board');
        
        for (let i = 0; i < boardElements.length; i++) {
            const boardElement = boardElements[i];
            
            const authorElement = boardElement.getElementsByTagName('Author')[0];
            const author = {
                displayName: getXmlText(authorElement, 'DisplayName'),
                profilePictureUrl: getXmlText(authorElement, 'ProfilePictureUrl')
            };
            
            const previewParticipantsElement = boardElement.getElementsByTagName('PreviewParticipants')[0];
            const previewParticipants = parseParticipants(previewParticipantsElement);
            
            const tagsElement = boardElement.getElementsByTagName('Tags')[0];
            const tags = parseTags(tagsElement);
            
            const board = {
                id: parseInt(getXmlText(boardElement, 'Id')),
                title: getXmlText(boardElement, 'Title'),
                description: getXmlText(boardElement, 'Description'),
                imageUrl: getXmlText(boardElement, 'ImageUrl'),
                status: getXmlText(boardElement, 'Status'),
                displayStatus: getXmlText(boardElement, 'DisplayStatus'),
                statusClass: getXmlText(boardElement, 'StatusClass'),
                eventDate: getXmlText(boardElement, 'EventDate'),
                eventTime: getXmlText(boardElement, 'EventTime'),
                deadline: getXmlText(boardElement, 'Deadline'),
                location: getXmlText(boardElement, 'Location'),
                tags: tags,
                joinPolicy: getXmlText(boardElement, 'JoinPolicy'),
                joinPolicyDisplay: getXmlText(boardElement, 'JoinPolicyDisplay'),
                currentParticipants: parseInt(getXmlText(boardElement, 'CurrentParticipants')),
                maxParticipants: parseInt(getXmlText(boardElement, 'MaxParticipants')),
                spotsLeft: parseInt(getXmlText(boardElement, 'SpotsLeft')),
                author: author,
                previewParticipants: previewParticipants,
                totalVisibleParticipants: parseInt(getXmlText(boardElement, 'TotalVisibleParticipants'))
            };
            
            boards.push(board);
        }
        
        return boards;
    }

    // Search function using XMLHttpRequest
    function performSearch() {
        const searchName = searchNameInput.value.trim();
        const tags = selectedTags.join(',');
        const eventDateFrom = eventDateFromInput.value;
        const eventDateTo = eventDateToInput.value;
        const statuses = getSelectedStatuses();
        const joinPolicies = getSelectedPolicies();

        // Build query parameters
        const params = new URLSearchParams();
        if (searchName) params.append('searchName', searchName);
        if (tags) params.append('tags', tags);
        if (eventDateFrom) params.append('eventDateFrom', eventDateFrom);
        if (eventDateTo) params.append('eventDateTo', eventDateTo);
        if (statuses) params.append('statuses', statuses);
        if (joinPolicies) params.append('joinPolicies', joinPolicies);

        // Show loading state
        searchResults.innerHTML = '<p class="loading-message">Searching...</p>';

        // Make XML HTTP request
        const xhr = new XMLHttpRequest();
        const url = `/Board/SearchBoards?${params.toString()}`;
        
        xhr.open('GET', url, true);
        xhr.setRequestHeader('Accept', 'application/xml');
        
        xhr.onload = function() {
            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    const xmlDoc = xhr.responseXML;
                    if (!xmlDoc) {
                        throw new Error('Invalid XML response');
                    }
                    const boards = parseXmlResponse(xmlDoc);
                    displayResults(boards);
                } catch (error) {
                    console.error('Error parsing XML:', error);
                    searchResults.innerHTML = '<p class="error-message">An error occurred while processing the search results. Please try again.</p>';
                }
            } else {
                console.error('Request failed with status:', xhr.status);
                searchResults.innerHTML = '<p class="error-message">An error occurred while searching. Please try again.</p>';
            }
        };
        
        xhr.onerror = function() {
            console.error('Network error occurred');
            searchResults.innerHTML = '<p class="error-message">A network error occurred. Please try again.</p>';
        };
        
        xhr.send();
    }

    // Display results using card design from Board/Index
    function displayResults(boards) {
        if (!boards || boards.length === 0) {
            searchResults.innerHTML = '<p>No boards found. Try adjusting your search criteria or <a href="/Board/Create">create a board</a>.</p>';
            return;
        }

        let html = '';
        boards.forEach(board => {
            const tagsHtml = board.tags.length > 0 
                ? board.tags.map(tag => `<span class="tag">${escapeHtml(tag)}</span>`).join('')
                : '<span>None</span>';

            const participantsHtml = board.previewParticipants.length > 0
                ? `<div class="participant-bubbles">
                    ${board.previewParticipants.map(p => 
                        `<img src="${p.profilePictureUrl || '/images/default-profile.png'}" 
                              alt="${escapeHtml(p.displayName)}" 
                              title="${escapeHtml(p.displayName)}"
                              onerror="this.src='/images/default-profile.png'" />`
                    ).join('')}
                   </div>
                   ${board.currentParticipants > board.previewParticipants.length 
                       ? `<span class="participant-more">+${board.currentParticipants - board.previewParticipants.length}</span>` 
                       : ''}`
                : '';

            html += `
                <div class="board-card">
                    <div class="board-card-layout">
                        <div class="board-image-wrap">
                            <img class="board-image" 
                                 src="${board.imageUrl || '/images/default-board.png'}" 
                                 alt="${escapeHtml(board.title)}"
                                 onerror="this.src='/images/default-board.png'" />
                            <div class="board-side-meta">
                                <div><strong>Event:</strong> ${board.eventDate}</div>
                                <div><strong>Time:</strong> ${board.eventTime}</div>
                                <div><strong>Location:</strong> ${escapeHtml(board.location)}</div>
                            </div>
                        </div>

                        <div class="board-card-content">
                            <h3 class="board-title">
                                <span class="status-dot ${board.statusClass}" title="${board.status}"></span>
                                ${escapeHtml(board.title)}
                            </h3>
                            
                            <div class="users-list">
                                <div class="author">
                                    By
                                    <img src="${board.author.profilePictureUrl || '/images/default-profile.png'}" 
                                         alt="${escapeHtml(board.author.displayName)}"
                                         onerror="this.src='/images/default-profile.png'" />
                                    ${escapeHtml(board.author.displayName)}
                                </div>
                                <div class="participants participant-row">
                                    <span>${board.currentParticipants} / ${board.maxParticipants} joined (${board.spotsLeft} spot(s) left)</span>
                                    ${participantsHtml}
                                </div>
                            </div>

                            <div class="board-meta">
                                <div><strong>Status:</strong> ${board.displayStatus}</div>
                                <div><strong>Join Policy:</strong> ${board.joinPolicyDisplay}</div>
                                <div><strong>Tags:</strong> ${tagsHtml}</div>
                                <div><strong>Join deadline:</strong> ${board.deadline}</div>
                            </div>

                            <p class="description">${escapeHtml(board.description)}</p>
                            <a class="board-link" href="/Board/Details/${board.id}">View Board Details</a>
                        </div>
                    </div>
                </div>
            `;
        });

        searchResults.innerHTML = html;
    }

    // Helper function to escape HTML
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Search on button click
    searchButton.addEventListener('click', performSearch);

    // Search on Enter key in search name input
    searchNameInput.addEventListener('keypress', function (e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            performSearch();
        }
    });

    // Clear all filters
    clearButton.addEventListener('click', function () {
        searchNameInput.value = '';
        tagInput.value = '';
        eventDateFromInput.value = '';
        eventDateToInput.value = '';
        selectedTags.length = 0;
        tagContainer.innerHTML = '';
        
        // Reset status filters to default (all Open)
        statusFilters.forEach(checkbox => {
            checkbox.checked = checkbox.value === 'Open';
        });
        
        // Reset policy filters to default (all checked)
        policyFilters.forEach(checkbox => {
            checkbox.checked = true;
        });
        
        searchResults.innerHTML = '<p class="loading-message">Use the filters above to search for boards.</p>';
    });

    // Perform initial search if there are URL parameters
    const urlParams = new URLSearchParams(window.location.search);
    if (urlParams.has('name') || urlParams.has('tags') || urlParams.has('eventDateFrom') || urlParams.has('eventDateTo')) {
        if (urlParams.has('name')) {
            searchNameInput.value = urlParams.get('name');
        }
        if (urlParams.has('tags')) {
            const tags = urlParams.get('tags').split(',');
            tags.forEach(tag => {
                if (tag.trim()) {
                    const formattedTag = formatTag(tag.trim());
                    selectedTags.push(formattedTag);
                    tagContainer.appendChild(createTagElement(formattedTag));
                }
            });
        }
        if (urlParams.has('eventDateFrom')) {
            eventDateFromInput.value = urlParams.get('eventDateFrom');
        }
        if (urlParams.has('eventDateTo')) {
            eventDateToInput.value = urlParams.get('eventDateTo');
        }
        performSearch();
    }
});

// Shared helpers for every page (loaded once, by the layout).
// Use them as App.esc(...), App.post(...), App.showMessage(...)

const App = (() => {

    //Safety
    // Turns text into safe HTML. Use it on EVERY value that comes from a user.
    const esc = value => String(value ?? '').replace(/[&<>"']/g,
        c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    // The anti-forgery token that Razor writes in the page with @Html.AntiForgeryToken()
    const token = () => $('input[name="__RequestVerificationToken"]').first().val();

    //Server calls
    // POST to an action. Always returns an object like { success, message }.
    async function post(url, data) {
        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token(),
                    'Content-Type': 'application/x-www-form-urlencoded'
                },
                body: new URLSearchParams(data)
            });
            return await response.json();
        } catch {
            return { success: false, message: 'Something went wrong. Please try again.' };
        }
    }

    //Messages
    let messageTimer;

    // Shows the green or red box (<div id="msg">) for 4 seconds
    function showMessage(text, ok = true) {
        const box = $('#msg');
        box.removeClass('d-none alert-success alert-danger')
            .addClass(ok ? 'alert-success' : 'alert-danger')
            .text(text);

        clearTimeout(messageTimer);
        messageTimer = setTimeout(() => box.addClass('d-none'), 4000);
    }

    //Text and dates
    const pad = n => String(n).padStart(2, '0');
    const firstLetter = text => String(text || '?').charAt(0).toUpperCase();

    // The server sends UTC dates. These show them in the visitor's own time zone.
    const formatDate = date => new Date(date).toLocaleDateString([], { dateStyle: 'medium' });
    const formatDateTime = date => new Date(date).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });

    // A Date as the value of an <input type="datetime-local">
    const toLocalInput = d =>
        `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;


    //Badges
    const STATUS_STYLE = {
        Open: 'bg-success-subtle text-success',
        Upcoming: 'bg-warning-subtle text-warning-emphasis',
        Closed: 'bg-secondary-subtle text-secondary'
    };

    const statusBadge = status =>
        `<span class="badge ${STATUS_STYLE[status] || STATUS_STYLE.Closed} rounded-pill px-3">${esc(status)}</span>`;


    //Photos
    const MAX_IMAGE_BYTES = 2 * 1024 * 1024;

    // Convenience check before uploading (the server checks again). Clears the input if too big.
    function checkImageSize(input) {
        const file = input.files[0];
        if (file && file.size > MAX_IMAGE_BYTES) {
            alert('The photo must be 2 MB or smaller.');
            input.value = '';
            return false;
        }
        return true;
    }

    //DataTables

    const TABLE_DOM = 'rt<"d-flex justify-content-between align-items-center flex-wrap gap-2 p-3"ip>';

    // For a column: draw the cell only when it is displayed. renderer(row, value) returns HTML.
    const display = renderer => (value, type, row) => type === 'display' ? renderer(row, value) : value;

    // The settings that every table of the app shares (server-side, no sorting, Previous/Next)
    function createTable(selector, { url, pageLength, columns, extraData, onData, drawCallback, dom, language }) {
        // Fires every time the server answers (before the rows are drawn)
        if (onData)
            $(selector).on('xhr.dt', (e, settings, json) => json && onData(json));

        const table = $(selector).DataTable({
            serverSide: true,
            processing: false,
            pageLength,
            lengthChange: false,
            autoWidth: false,
            pagingType: 'simple',
            ordering: false,
            dom: dom ?? TABLE_DOM,
            language: {
                zeroRecords: 'Nothing found.',
                info: 'Showing _START_ to _END_ of _TOTAL_',
                infoEmpty: 'Nothing to show',
                paginate: { previous: 'Previous', next: 'Next' },
                ...language
            },
            ajax: {
                url,
                type: 'POST',
                headers: { 'RequestVerificationToken': token() },
                data: d => ({
                    draw: d.draw,
                    start: d.start,
                    length: d.length,
                    search: $('#txtSearch').val(),
                    ...(extraData ? extraData() : {})
                }),
                error: xhr => showMessage(`Could not load the data (error ${xhr.status}).`, false)
            },
            columns,
            drawCallback
        });

        // The Search button and the Enter key reload the table, back to page 1
        $('#btnSearch').on('click', () => table.ajax.reload());
        $('#txtSearch').on('keydown', e => { if (e.key === 'Enter') table.ajax.reload(); });

        return table;
    }

    //Deleting a poll (the admin table and the user cards share it)

    // A delete button carries its data: class="poll-delete" data-id data-title data-votes
    function initPollDelete(tableSelector, table, deleteUrl) {
        const modal = new bootstrap.Modal(document.getElementById('deleteModal'));
        let pending = null;

        // The rows are drawn by AJAX, so the click listener is on the table
        $(tableSelector).on('click', '.poll-delete', function () {
            pending = { id: this.dataset.id, title: this.dataset.title, votes: Number(this.dataset.votes) };

            $('#dTitle').text(pending.title);
            $('#dNote').text(pending.votes > 0
                ? `This will also delete its ${pending.votes} vote(s) and its photos. This cannot be undone.`
                : 'This cannot be undone.');
            modal.show();
        });

        $('#dConfirm').on('click', async function () {
            const button = this;
            button.disabled = true;

            const result = await post(deleteUrl, { id: pending.id });

            modal.hide();
            showMessage(result.message, result.success);
            table.ajax.reload(null, false);      // refresh, staying on the same page
            button.disabled = false;
        });
    }

    // ---------- Icons and action buttons ----------

    const ICONS = {
        edit: '<svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 3a2.828 2.828 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z"/></svg>',
        trash: '<svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>'
    };

    // Edit and Delete as small round icon buttons. Nothing is drawn when the visitor can do neither.
    function pollActions(poll, editUrl, { canEdit, canDelete }) {
        if (!canEdit && !canDelete) return '';

        const id = Number(poll.id);
        const edit = canEdit
            ? `<a class="icon-btn" href="${editUrl}/${id}" title="Edit" aria-label="Edit">${ICONS.edit}</a>` : '';
        const remove = canDelete
            ? `<button type="button" class="icon-btn danger poll-delete" title="Delete" aria-label="Delete"
                       data-id="${id}" data-title="${esc(poll.title)}" data-votes="${Number(poll.votes)}">${ICONS.trash}</button>` : '';

        return `<div class="icon-actions">${edit}${remove}</div>`;
    }

    return {
        esc, token, post, showMessage,
        firstLetter, formatDate, formatDateTime, toLocalInput,
        statusBadge, checkImageSize,
        display, createTable, initPollDelete, pollActions
    };
})();

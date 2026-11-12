// The polls page for the admin: a management table (DataTables, server-side).
(() => {
    const { esc, formatDateTime, statusBadge, firstLetter, display } = App;
    const cfg = window.PollsPage;

    const PAGE_SIZE = 20;
    const DESCRIPTION_LENGTH = 80;

    const shorten = (text, max) => text.length > max ? text.substring(0, max) + '...' : text;

    function pollCell(poll) {
        const thumb = poll.imageUrl
            ? `<img class="poll-thumb" src="${esc(poll.imageUrl)}" alt="" />`
            : `<span class="poll-thumb poll-thumb-ph">${esc(firstLetter(poll.title))}</span>`;

        return `<div class="d-flex align-items-center gap-3">
            ${thumb}
            <div>
                <div class="fw-semibold">${esc(poll.title)}</div>
                <div class="text-muted small">${esc(shorten(poll.description || '', DESCRIPTION_LENGTH))}</div>
            </div>
        </div>`;
    }

    // The admin can delete ANY poll, but edit only the polls they created
    function actionsCell(poll) {
        const id = Number(poll.id);
        const edit = poll.isMine
            ? `<a class="btn btn-outline-secondary btn-sm rounded-pill px-3" href="${cfg.urls.edit}/${id}">Edit</a>` : '';

        return `<div class="d-inline-flex gap-1">
            ${edit}
            <a class="btn btn-outline-primary btn-sm rounded-pill px-3" href="${cfg.urls.details}/${id}">Details</a>
            <button type="button" class="btn btn-outline-danger btn-sm rounded-pill px-3 poll-delete"
                    data-id="${id}" data-title="${esc(poll.title)}" data-votes="${Number(poll.votes)}">Delete</button>
        </div>`;
    }

    const table = App.createTable('#pollsTable', {
        url: cfg.urls.list,
        pageLength: PAGE_SIZE,
        extraData: () => ({ type: cfg.type }),
        language: { emptyTable: 'No polls yet.', zeroRecords: 'No polls found.', infoEmpty: 'No polls' },
        onData: json => $('#totalChip').text(`${json.recordsTotal} polls`),
        columns: [
            { title: 'Poll', data: 'title', width: '34%', render: display(pollCell) },
            { title: 'Choices', data: 'choices', width: '9%', className: 'text-center' },
            { title: 'Votes', data: 'votes', width: '9%', className: 'text-center' },
            { title: 'Ends', data: 'endDate', width: '17%', render: display(p => esc(formatDateTime(p.endDate))) },
            { title: 'Status', data: 'status', width: '11%', className: 'text-center', render: display(p => statusBadge(p.status)) },
            { title: '', data: 'id', width: '20%', className: 'text-end', render: display(actionsCell) }
        ]
    });

    App.initPollDelete('#pollsTable', table, cfg.urls.remove);
})();
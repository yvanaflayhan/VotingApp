// The polls page for the admin: a management table (DataTables, server-side).
(() => {
    const { esc, formatDateTime, statusBadge, firstLetter, display, pollActions } = App;
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
                <div class="fw-semibold"><a class="row-link" href="${cfg.urls.details}/${Number(poll.id)}">${esc(poll.title)}</a></div>
                <div class="text-muted small">${esc(shorten(poll.description || '', DESCRIPTION_LENGTH))}</div>
            </div>
        </div>`;
    }


    const table = App.createTable('#pollsTable', {
        url: cfg.urls.list,
        pageLength: PAGE_SIZE,
        extraData: () => ({ type: cfg.type }),
        language: { emptyTable: 'No polls yet.', zeroRecords: 'No polls found.', infoEmpty: 'No polls' },
        onData: json => $('#totalChip').text(`${json.recordsTotal} polls`),
        columns: [
            { title: 'Poll', data: 'title', width: '40%', render: display(pollCell) },
            { title: 'Choices', data: 'choices', width: '9%', className: 'text-center' },
            { title: 'Votes', data: 'votes', width: '9%', className: 'text-center' },
            { title: 'Ends', data: 'endDate', width: '17%', render: display(p => esc(formatDateTime(p.endDate))) },
            { title: 'Status', data: 'status', width: '12%', className: 'text-center', render: display(p => statusBadge(p.status)) },
            {
                title: '', data: 'id', width: '13%', className: 'text-end',
                // The admin can delete ANY poll, but edit only the polls they created
                render: display(p => pollActions(p, cfg.urls.edit, { canEdit: p.isMine, canDelete: true }))
            }
        ]
    });

    App.initPollDelete('#pollsTable', table, cfg.urls.remove);
})();
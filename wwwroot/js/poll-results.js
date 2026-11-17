// The Results page: finished polls and their winners, one card per row (DataTables, server-side).
(() => {
    const { esc, formatDate, firstLetter, display } = App;
    const cfg = window.ResultsPage;

    const PAGE_SIZE = 4;
    const MAX_OPTIONS_SHOWN = 6;
    const CARDS_DOM = 'rt<"d-flex justify-content-between align-items-center flex-wrap gap-2 mt-4"ip>';

    let type = '';                    // '' = all, or 'Community' / 'Awards'

    //Pieces of a card

    const typeBadge = t =>
        `<span class="badge rounded-pill ${t === 'Awards' ? 'bg-warning-subtle text-warning-emphasis' : 'bg-primary-subtle text-primary'}">${esc(t)}</span>`;

    const thumb = c => c.imageUrl ? `<img class="opt-thumb" src="${esc(c.imageUrl)}" alt="" loading="lazy" />` : '';

    // The box with the winner (or the winners if it is a tie, or a note if nobody voted)
    function winnerHtml(poll) {
        const winners = poll.choices.filter(c => c.isWinner);

        if (!winners.length)
            return `<div class="result-winner none"><span class="result-trophy">&#128566;</span>
                        <div class="result-name">Nobody voted in this poll.</div></div>`;

        const tie = winners.length > 1;
        const names = winners.map(w => `<span class="result-name">${thumb(w)}${esc(w.text)}</span>`).join(' &amp; ');
        const best = winners[0];

        return `<div class="result-winner">
            <span class="result-trophy">&#127942;</span>
            <div>
                <div class="result-label">${tie ? `Tie between ${winners.length}` : 'Winner'}</div>
                ${names}
            </div>
            <div class="result-score"><strong>${Number(best.percent)}%</strong>${Number(best.votes)} votes</div>
        </div>`;
    }

    function choiceHtml(c) {
        return `<div class="opt-line ${c.isWinner ? 'is-winner' : ''}">
            <div class="opt-top">
                <span class="opt-name">${thumb(c)}<span class="opt-text">${esc(c.text)}</span></span>
                <span class="text-muted">${Number(c.votes)} · ${Number(c.percent)}%</span>
            </div>
            <div class="progress"><div class="progress-bar" style="width:0" data-w="${Number(c.percent)}"></div></div>
        </div>`;
    }

    function cardHtml(poll) {
        const url = `${cfg.urls.details}/${Number(poll.id)}`;
        const photo = poll.imageUrl
            ? `<img src="${esc(poll.imageUrl)}" alt="" loading="lazy" />`
            : `<span>${esc(firstLetter(poll.title))}</span>`;

        return `<div class="poll-card" data-id="${Number(poll.id)}">
            <a class="poll-photo ${poll.imageUrl ? '' : 'ph'}" href="${url}">${photo}</a>
            <div class="poll-content">
                <div class="poll-head">
                    <h5 class="poll-title"><a href="${url}">${esc(poll.title)}</a></h5>
                    <div class="flex-shrink-0">${typeBadge(poll.type)}</div>
                </div>
                ${winnerHtml(poll)}
                <div class="poll-options">${poll.choices.slice(0, MAX_OPTIONS_SHOWN).map(choiceHtml).join('')}</div>
                <div class="poll-bottom">
                    <div class="poll-meta">
                        <span>${poll.choices.length} choices</span>
                        <span>${Number(poll.totalVotes)} votes</span>
                        <span>Ended ${esc(formatDate(poll.endedAt))}</span>
                    </div>
                </div>
            </div>
        </div>`.trim();
    }

    // The bars start at 0 and grow to their value (same as poll-cards.js: we merge them in the cleanup)
    function growBars(scope) {
        requestAnimationFrame(() => requestAnimationFrame(() =>
            scope.querySelectorAll('.progress-bar[data-w]').forEach(bar => bar.style.width = bar.dataset.w + '%')));
    }

    //The table

    const table = App.createTable('#resultsTable', {
        url: cfg.urls.list,
        pageLength: PAGE_SIZE,
        dom: CARDS_DOM,
        extraData: () => ({ type }),
        language: {
            emptyTable: 'No finished polls yet.',
            zeroRecords: 'No results found.',
            infoEmpty: 'No results'
        },
        onData: json => $('#resultsChip').text(`${json.recordsTotal} finished polls`),
        columns: [{ title: '', data: 'id', render: display(cardHtml) }],
        drawCallback: () => growBars(document.getElementById('resultsTable'))
    });

    //Tabs and search

    $('#resultTabs').on('click', 'button', function () {
        type = this.dataset.type;
        $('#resultTabs .nav-link').removeClass('active');
        this.classList.add('active');
        table.ajax.reload();                         // back to page 1
    });

    let timer;
    $('#resultsSearch').on('input', function () {
        const text = this.value;
        clearTimeout(timer);
        timer = setTimeout(() => table.search(text).draw(), 300);     // wait until the user stops typing
    });
})();
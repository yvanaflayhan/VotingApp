// The polls page for users and guests: one card per row, loaded with DataTables (server-side).
(() => {
    const { esc, post, statusBadge, formatDate, formatDateTime, firstLetter, display } = App;
    const cfg = window.PollsPage;

    const PAGE_SIZE = 4;
    const MAX_OPTIONS_SHOWN = 6;     // options drawn on a card
    const FADE_AFTER = 3;            // with more options than this, the list fades out
    const CARDS_DOM = 'rt<"d-flex justify-content-between align-items-center flex-wrap gap-2 mt-4"ip>';

    const polls = {};                // poll id -> poll data, so a card can redraw itself
    let current = null;              // the poll shown in the vote popup

    //Pieces of a card

    const note = html => `<div class="poll-note">${html}</div>`;

    function optionHtml(poll, option) {
        const isMine = option.id === poll.myChoiceId;
        const photo = option.imageUrl
            ? `<img class="opt-thumb" src="${esc(option.imageUrl)}" alt="" loading="lazy" />` : '';
        const yourVote = isMine
            ? ' <span class="badge bg-primary-subtle text-primary rounded-pill ms-1">Your vote</span>' : '';

        return `<div class="opt-line ${isMine ? 'mine' : ''}">
            <div class="opt-top">
                <span class="opt-name">${photo}<span class="opt-text">${esc(option.text)}</span>${yourVote}</span>
                <span class="text-muted">${Number(option.percent)}%</span>
            </div>
            <div class="progress"><div class="progress-bar" style="width:0" data-w="${Number(option.percent)}"></div></div>
        </div>`;
    }

    // Your own vote goes first, so the fade can never hide it
    function sortedOptions(poll) {
        const mine = poll.options.find(o => o.id === poll.myChoiceId);
        return mine ? [mine, ...poll.options.filter(o => o !== mine)] : poll.options;
    }

    function optionsHtml(poll) {
        const options = sortedOptions(poll);
        const lines = options.slice(0, MAX_OPTIONS_SHOWN).map(o => optionHtml(poll, o)).join('');
        return `<div class="poll-options ${options.length > FADE_AFTER ? 'fade-out' : ''}">${lines}</div>`;
    }

    // The bottom right of the card: a button or a short note, depending on the situation
    function actionHtml(poll) {
        if (!poll.options.length) return note('No options yet');
        if (poll.myChoiceId != null) return note('&#10003; You voted');
        if (poll.status === 'Closed') return note('This poll is closed');
        if (poll.status === 'Upcoming') return note(`Starts ${esc(formatDateTime(poll.startDate))}`);
        if (!cfg.isAuth) return `<a class="btn btn-primary rounded-pill px-4" href="${cfg.urls.login}">Log in to vote</a>`;
        return '<button type="button" class="btn btn-primary rounded-pill px-4 vote-open">Vote</button>';
    }

    // Edit and Delete appear only on the polls you created
    function ownerButtons(poll) {
        if (!poll.isMine) return '';
        return `<a class="btn btn-outline-secondary btn-sm rounded-pill px-3" href="${cfg.urls.edit}/${Number(poll.id)}">Edit</a>
            <button type="button" class="btn btn-outline-danger btn-sm rounded-pill px-3 poll-delete"
                    data-id="${Number(poll.id)}" data-title="${esc(poll.title)}" data-votes="${Number(poll.votes)}">Delete</button>`;
    }

    function cardHtml(poll) {
        const url = `${cfg.urls.details}/${Number(poll.id)}`;
        const photo = poll.imageUrl
            ? `<img src="${esc(poll.imageUrl)}" alt="" loading="lazy" />`
            : `<span>${esc(firstLetter(poll.title))}</span>`;

        return `<div class="poll-card row-card" data-id="${Number(poll.id)}">
            <a class="poll-photo ${poll.imageUrl ? '' : 'ph'}" href="${url}">${photo}</a>
            <div class="poll-content">
                <div class="poll-head">
                    <h5 class="poll-title"><a href="${url}">${esc(poll.title)}</a></h5>
                    <div class="d-flex align-items-center gap-2 flex-shrink-0">
                        ${ownerButtons(poll)}
                        ${statusBadge(poll.status)}
                    </div>
                </div>
                ${optionsHtml(poll)}
                <div class="poll-bottom">
                    <div class="poll-meta">
                        <span>${Number(poll.choices)} choices</span>
                        <span>${Number(poll.votes)} votes</span>
                        <span>Ends ${esc(formatDate(poll.endDate))}</span>
                    </div>
                    <div class="poll-action">${actionHtml(poll)}</div>
                </div>
            </div>
        </div>`.trim();
    }

    // The bars start at 0 and grow to their value
    function growBars(scope) {
        requestAnimationFrame(() => requestAnimationFrame(() =>
            scope.querySelectorAll('.progress-bar[data-w]').forEach(bar => bar.style.width = bar.dataset.w + '%')));
    }

    // Draws one card again, without reloading the table
    function redrawCard(poll) {
        const old = document.querySelector(`.poll-card[data-id="${poll.id}"]`);
        if (!old) return;

        const fresh = $(cardHtml(poll))[0];
        old.replaceWith(fresh);
        growBars(fresh);
    }

    //The table

    const table = App.createTable('#pollsTable', {
        url: cfg.urls.list,
        pageLength: PAGE_SIZE,
        dom: CARDS_DOM,
        extraData: () => ({ type: cfg.type, mine: cfg.mine }),
        language: {
            emptyTable: cfg.mine ? 'You have not created any poll yet.' : 'No polls yet.',
            zeroRecords: 'No polls found.',
            infoEmpty: 'No polls'
        },
        onData: json => {
            json.data.forEach(p => polls[p.id] = p);
            $('#totalChip').text(`${json.recordsTotal} polls`);
        },
        columns: [{ title: '', data: 'id', render: display(cardHtml) }],     // one column: each row is a card
        drawCallback: () => growBars(document.getElementById('pollsTable'))
    });

    //The vote popup

    const voteModal = new bootstrap.Modal(document.getElementById('voteModal'));

    // With photos (Awards): a grid of photo tiles. Without photos: a simple list.
    function voteOptionHtml(option, index, withPhotos) {
        const picture = !withPhotos ? ''
            : option.imageUrl
                ? `<img class="vote-pic" src="${esc(option.imageUrl)}" alt="" />`
                : `<div class="vote-pic vote-pic-ph">${esc(firstLetter(option.text))}</div>`;

        return `<label class="vote-choice ${withPhotos ? 'with-pic' : ''}" style="--i:${index}">
            <input type="radio" name="choice" value="${Number(option.id)}" />
            <span>${picture}${esc(option.text)}</span>
        </label>`;
    }

    $('#pollsTable').on('click', '.vote-open', function () {
        current = polls[this.closest('.poll-card').dataset.id];
        const withPhotos = current.options.some(o => o.imageUrl);

        $('#vTitle').text(current.title);
        $('#vNote').text('Choose one option. A vote cannot be changed.');
        $('#vError').addClass('d-none');
        $('#vConfirm').prop('disabled', true);
        $('#vOptions')
            .toggleClass('vote-grid', withPhotos)
            .html(current.options.map((o, i) => voteOptionHtml(o, i, withPhotos)).join(''));

        voteModal.show();
    });

    $('#vOptions').on('change', 'input', function () {
        const option = current.options.find(o => o.id === Number(this.value));
        // .text() on a new element: the option text is never read as HTML
        $('#vNote').empty().append('Vote for ', $('<strong>').text(option.text), '? A vote cannot be changed.');
        $('#vConfirm').prop('disabled', false);
    });

    $('#vConfirm').on('click', async function () {
        const poll = current;
        const chosen = document.querySelector('#vOptions input:checked');
        if (!poll || !chosen) return;

        const button = this;
        button.disabled = true;
        $('#vError').addClass('d-none');

        const result = await post(cfg.urls.vote, { pollId: poll.id, choiceId: chosen.value });

        if (!result.success) {
            $('#vError').text(result.message).removeClass('d-none');
            button.disabled = false;
            return;
        }

        // The server answers with the fresh results: redraw only this card
        Object.assign(poll, { myChoiceId: result.myChoiceId, options: result.options, votes: result.votes });
        voteModal.hide();
        redrawCard(poll);
    });

    //Delete (only your own polls show the button)

    App.initPollDelete('#pollsTable', table, cfg.urls.remove);
})();
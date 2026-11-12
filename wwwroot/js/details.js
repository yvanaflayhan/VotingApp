// The poll details page: the countdown and voting.
(() => {
    const MINUTE = 60 * 1000;
    const HOUR = 60 * MINUTE;
    const DAY = 24 * HOUR;

    //Countdown

    const countdown = document.getElementById('countdown');
    const start = new Date(countdown.dataset.start);
    const end = new Date(countdown.dataset.end);
    const isUpcoming = countdown.dataset.status === 'Upcoming';

    function tick() {
        if (isUpcoming) {
            countdown.textContent = App.formatDateTime(start);
            return;
        }

        const left = end - new Date();
        if (left <= 0) {
            countdown.textContent = 'Poll ended';
            return;
        }

        const days = Math.floor(left / DAY);
        const hours = Math.floor(left % DAY / HOUR);
        const minutes = Math.floor(left % HOUR / MINUTE);
        countdown.textContent = `${days}d ${hours}h ${minutes}m`;
    }

    tick();
    setInterval(tick, 30 * 1000);

    //Voting
    // The vote area exists only when this visitor can vote
    const area = document.getElementById('voteArea');
    if (!area) return;

    const button = document.getElementById('voteBtn');
    const error = document.getElementById('voteMsg');

    // The Vote button stays disabled until a choice is selected
    area.querySelectorAll('input[name="choiceId"]').forEach(radio =>
        radio.addEventListener('change', () => button.disabled = false));

    button.addEventListener('click', async () => {
        const chosen = area.querySelector('input[name="choiceId"]:checked');
        if (!chosen) return;

        button.disabled = true;
        error.classList.add('d-none');

        const result = await App.post(area.dataset.voteUrl, { pollId: area.dataset.pollId, choiceId: chosen.value });

        if (result.success) {
            location.reload();          // the page then shows "You voted for ..." and the new results
            return;
        }

        error.textContent = result.message;
        error.classList.remove('d-none');
        button.disabled = false;
    });
})();
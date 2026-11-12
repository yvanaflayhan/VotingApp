// Shared by the Create and Edit poll forms.
(() => {
    const WEEK = 7 * 24 * 60 * 60 * 1000;

    // The server stores UTC, so it needs the visitor's time zone to convert the dates they type
    $('#TimezoneOffset').val(new Date().getTimezoneOffset());

    // Date inputs: keep what was typed after an error. Otherwise show the poll's saved date
    // (Edit: it arrives in data-utc) or a default (Create: now, and a week later).
    const defaultOffset = { StartDate: 0, EndDate: WEEK };

    ['StartDate', 'EndDate'].forEach(id => {
        const input = document.getElementById(id);
        if (!input || input.value) return;

        const date = input.dataset.utc ? new Date(input.dataset.utc) : new Date(Date.now() + defaultOffset[id]);
        input.value = App.toLocalInput(date);
    });

    // Poll photo: preview (Create only, it has an #imagePreview)
    function showPreview(input) {
        const preview = document.getElementById('imagePreview');
        if (!preview) return;

        const file = input.files[0];
        preview.classList.toggle('d-none', !file);
        if (file) preview.src = URL.createObjectURL(file);
    }

    // Size check before uploading (the server checks again). The choice photos of the create form
    // have their own handler in create-poll.js.
    document.querySelectorAll('input[type=file]:not(.choice-file)').forEach(input =>
        input.addEventListener('change', () => {
            App.checkImageSize(input);              // clears the input when the file is too big
            if (input.id === 'Image') showPreview(input);
        }));
})();
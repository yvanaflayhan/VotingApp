// The "New poll" form: add and remove choices, and a photo for each choice (Awards only).
(() => {
    const MIN_CHOICES = 2;
    const MAX_CHOICES = 10;

    const list = document.getElementById('choiceList');
    const addButton = document.getElementById('addChoice');
    const counter = document.getElementById('choiceCount');
    const template = document.getElementById('choiceTemplate');
    const awardsRadio = document.getElementById('typeAwards');
    const communityRadio = document.getElementById('typeCommunity');

    const rows = () => list.querySelectorAll('.choice-row');

    // Numbers the rows and renames the inputs: Choices[0].Text, Choices[1].Text, ...
    // MVC needs the indexes to be 0, 1, 2 with no gaps, even after a row was removed.
    function refresh() {
        const all = rows();

        all.forEach((row, i) => {
            row.querySelector('.choice-num').textContent = i + 1;
            row.querySelector('.choice-text').name = `Choices[${i}].Text`;
            row.querySelector('.choice-file').name = `Choices[${i}].Image`;
            row.querySelector('.btn-remove').disabled = all.length <= MIN_CHOICES;
        });

        list.classList.toggle('awards-mode', awardsRadio.checked);     // the camera buttons show in Awards only
        addButton.style.display = all.length >= MAX_CHOICES ? 'none' : '';
        counter.textContent = `${all.length} of ${MAX_CHOICES}`;
    }

    function clearPhoto(row) {
        const label = row.querySelector('.choice-photo');
        row.querySelector('.choice-file').value = '';
        label.style.backgroundImage = '';
        label.classList.remove('has-photo');
    }

    addButton.addEventListener('click', () => {
        list.appendChild(template.content.cloneNode(true));
        refresh();
        list.lastElementChild.querySelector('.choice-text').focus();
    });

    // One listener on the list, because rows are added and removed
    list.addEventListener('click', e => {
        const button = e.target.closest('.btn-remove');
        if (!button || button.disabled) return;

        button.closest('.choice-row').remove();
        refresh();
    });

    // A photo was chosen for a choice: size check, then a round preview
    list.addEventListener('change', e => {
        if (!e.target.matches('.choice-file')) return;

        const row = e.target.closest('.choice-row');
        if (!App.checkImageSize(e.target) || !e.target.files[0]) {
            clearPhoto(row);
            return;
        }

        const label = row.querySelector('.choice-photo');
        label.style.backgroundImage = `url("${URL.createObjectURL(e.target.files[0])}")`;
        label.classList.add('has-photo');
    });

    // Enter in a choice adds a new one instead of submitting the form
    list.addEventListener('keydown', e => {
        if (e.key !== 'Enter' || !e.target.matches('.choice-text')) return;

        e.preventDefault();
        if (rows().length < MAX_CHOICES) addButton.click();
    });

    // Switching to Community removes the choice photos (they exist only in Awards)
    awardsRadio.addEventListener('change', refresh);
    communityRadio.addEventListener('change', () => {
        rows().forEach(clearPhoto);
        refresh();
    });

    refresh();
})();
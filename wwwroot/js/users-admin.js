// The users page for the admin: a table (DataTables, server-side) where roles can be changed.
(() => {
    const { esc, post, showMessage, firstLetter, display } = App;
    const cfg = window.UsersPage;

    const ADMIN = 'Admin';
    const USER = 'User';

    //Cells

    function userCell(user) {
        const name = user.fullName || user.userName;
        const rootBadge = user.isRoot ? '<span class="badge bg-primary ms-1">Main admin</span>' : '';
        const youBadge = user.isMe ? '<span class="badge bg-secondary ms-1">You</span>' : '';

        return `<div class="d-flex align-items-center">
            <span class="avatar me-3">${esc(firstLetter(name))}</span>
            <div>
                <div class="fw-semibold">${esc(name)}${rootBadge}${youBadge}</div>
                <div class="text-muted small">${esc(user.email)}</div>
            </div>
        </div>`;
    }

    // One half of the User | Admin switch
    function roleRadio(user, role, locked) {
        const id = `${role.toLowerCase()}-${esc(user.id)}`;

        return `<input type="radio" class="btn-check role-radio" name="role-${esc(user.id)}" id="${id}"
                       value="${role}" ${user.role === role ? 'checked' : ''} ${locked ? 'disabled' : ''}
                       data-id="${esc(user.id)}" data-username="${esc(user.userName)}" data-original="${esc(user.role)}" />
                <label class="btn btn-outline-primary btn-sm px-3" for="${id}">${role}</label>`;
    }

    function roleCell(user) {
        // The main admin can never be changed, and nobody changes their own role
        const locked = user.isRoot || user.isMe;

        return `<div class="btn-group role-switch" role="group">
            ${roleRadio(user, USER, locked)}${roleRadio(user, ADMIN, locked)}
        </div>`;
    }

    //The table

    const table = App.createTable('#usersTable', {
        url: cfg.urls.list,
        pageLength: 20,
        language: { emptyTable: 'No users found.', zeroRecords: 'No users found.', infoEmpty: 'No users' },
        onData: json => {
            $('#totalChip').text(`${json.recordsTotal} users`);
            $('#adminChip').text(`${json.adminCount} admin(s)`);
        },
        columns: [
            { title: 'User', data: 'fullName', width: '45%', render: display(userCell) },
            { title: 'Username', data: 'userName', width: '25%', className: 'd-none d-md-table-cell text-muted' },
            { title: 'Role', data: 'role', width: '30%', className: 'text-end', render: display(roleCell) }
        ]
    });

    //Role change, with a confirmation popup

    const modalElement = document.getElementById('confirmRoleModal');
    const modal = new bootstrap.Modal(modalElement);
    let pending = null;         // the radio the admin just clicked
    let confirmed = false;

    // The rows are drawn by AJAX, so the listener is on the table
    $('#usersTable').on('change', '.role-radio', function () {
        pending = this;
        confirmed = false;
        const toAdmin = this.value === ADMIN;

        // .text() shows the username as plain text, never as HTML
        $('#mUser').text(this.dataset.username);
        $('#mFrom').text(this.dataset.original);
        $('#mTo').text(this.value);
        $('#confirmIcon').toggleClass('warn', toAdmin);
        $('#mNote').text(toAdmin
            ? 'Admins can manage all users and polls.'
            : 'They will become a normal user and lose access to the admin pages.');

        modal.show();
    });

    $('#confirmRoleBtn').on('click', async () => {
        confirmed = true;
        modal.hide();

        const result = await post(cfg.urls.changeRole, { userId: pending.dataset.id, role: pending.value });

        showMessage(result.message, result.success);
        table.ajax.reload(null, false);         // stay on the same page
    });

    // Closed without confirming (Cancel, X, outside click): put the old value back
    modalElement.addEventListener('hidden.bs.modal', () => {
        if (confirmed || !pending) return;

        $(pending).closest('.btn-group')
            .find(`input[value="${pending.dataset.original}"]`).prop('checked', true);
    });
})();
export function targetStatus(column) {
    return column?.dataset.status ?? null;
}

export function mount(root, win) {
    let dragged = null;

    for (const button of root.querySelectorAll('[data-card-form] button[type="submit"]')) {
        button.hidden = true;
    }

    const submit = (card, status) => {
        const form = card?.querySelector('[data-card-form]');
        const select = form?.querySelector('[data-card-status]');
        if (!form || !select || !status || select.value === status) {
            return;
        }
        select.value = status;
        form.requestSubmit();
    };

    root.addEventListener('change', event => {
        const select = event.target instanceof win.HTMLSelectElement && event.target.matches('[data-card-status]') ? event.target : null;
        select?.closest('form')?.requestSubmit();
    });

    root.addEventListener('dragstart', event => {
        const card = event.target instanceof win.Element ? event.target.closest('[data-card]') : null;
        if (!card || card.getAttribute('draggable') !== 'true') {
            return;
        }
        dragged = card;
        event.dataTransfer?.setData('text/plain', card.dataset.card);
        if (event.dataTransfer) {
            event.dataTransfer.effectAllowed = 'move';
        }
    });

    root.addEventListener('dragend', () => {
        dragged = null;
        for (const column of root.querySelectorAll('.drop-target')) {
            column.classList.remove('drop-target');
        }
    });

    root.addEventListener('dragover', event => {
        const column = event.target instanceof win.Element ? event.target.closest('[data-status]') : null;
        if (!dragged || !column) {
            return;
        }
        event.preventDefault();
        for (const other of root.querySelectorAll('.drop-target')) {
            if (other !== column) {
                other.classList.remove('drop-target');
            }
        }
        column.classList.add('drop-target');
    });

    root.addEventListener('drop', event => {
        const column = event.target instanceof win.Element ? event.target.closest('[data-status]') : null;
        if (!dragged || !column) {
            return;
        }
        event.preventDefault();
        column.classList.remove('drop-target');
        const card = dragged;
        dragged = null;
        if (card.closest('[data-status]') !== column) {
            column.append(card);
            submit(card, targetStatus(column));
        }
    });
}

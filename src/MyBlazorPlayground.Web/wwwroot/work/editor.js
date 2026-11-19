const BLOCKS = new Set(['P', 'DIV', 'SECTION', 'ARTICLE', 'HEADER', 'FOOTER', 'TABLE', 'TR', 'UL', 'OL', 'PRE', 'BLOCKQUOTE', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6']);
const SKIP = new Set(['SCRIPT', 'STYLE', 'TEMPLATE', 'NOSCRIPT', 'IFRAME', 'OBJECT', 'SVG', 'HEAD', 'TITLE', 'META', 'LINK']);

export function isWebUrl(value) {
    try {
        const url = new URL(value);
        return url.protocol === 'https:' || url.protocol === 'http:' || url.protocol === 'mailto:';
    } catch {
        return false;
    }
}

export function tidy(text) {
    return text
        .replace(/[ \t]+\n/g, '\n')
        .replace(/\n{3,}/g, '\n\n')
        .trim();
}

function inline(node) {
    let out = '';
    for (const child of node.childNodes) {
        out += convert(child);
    }
    return out;
}

function wrap(text, marker) {
    const trimmed = text.trim();
    return trimmed.length === 0 ? text : `${marker}${trimmed}${marker}`;
}

function convert(node) {
    if (node.nodeType === 3) {
        return node.nodeValue.replace(/\s+/g, ' ');
    }
    if (node.nodeType !== 1) {
        return '';
    }
    const tag = node.tagName.toUpperCase();
    if (SKIP.has(tag)) {
        return '';
    }
    switch (tag) {
        case 'BR':
            return '\n';
        case 'B':
        case 'STRONG':
            return wrap(inline(node), '**');
        case 'I':
        case 'EM':
            return wrap(inline(node), '*');
        case 'S':
        case 'DEL':
        case 'STRIKE':
            return wrap(inline(node), '~~');
        case 'CODE':
            return node.closest('pre') ? node.textContent : `\`${node.textContent.replace(/`/g, '')}\``;
        case 'PRE':
            return `\n\n\`\`\`\n${node.textContent.replace(/\n$/, '')}\n\`\`\`\n\n`;
        case 'A': {
            const href = node.getAttribute('href') ?? '';
            const text = inline(node).trim();
            return isWebUrl(href) ? `[${text || href}](${href})` : text;
        }
        case 'IMG': {
            const src = node.getAttribute('src') ?? '';
            const alt = (node.getAttribute('alt') ?? 'image').replace(/[[\]]/g, '');
            return isWebUrl(src) ? `[${alt}](${src})` : '';
        }
        case 'H1':
        case 'H2':
        case 'H3':
        case 'H4':
        case 'H5':
        case 'H6':
            return `\n\n${'#'.repeat(Number(tag[1]))} ${inline(node).trim()}\n\n`;
        case 'LI': {
            const list = node.parentElement?.tagName.toUpperCase();
            const index = [...(node.parentElement?.children ?? [])].indexOf(node) + 1;
            const marker = list === 'OL' ? `${index}.` : '-';
            return `\n${marker} ${inline(node).trim().replace(/\n+/g, ' ')}`;
        }
        case 'BLOCKQUOTE':
            return `\n\n${tidy(inline(node)).split('\n').map(line => `> ${line}`).join('\n')}\n\n`;
        case 'TD':
        case 'TH':
            return `${inline(node).trim()} | `;
        case 'TR':
            return `\n| ${inline(node).trim()}`;
        default:
            return BLOCKS.has(tag) ? `\n\n${inline(node)}\n\n` : inline(node);
    }
}

export function htmlToMarkdown(html, parser) {
    const doc = parser.parseFromString(html, 'text/html');
    return tidy(inline(doc.body));
}

export function markdownFor(file) {
    const name = file.name.replace(/[[\]]/g, '');
    return file.image ? `![${name}](${file.url})` : `[${name}](${file.url})`;
}

export function pastedName(file, now = new Date()) {
    if (file.name && file.name !== 'image.png' && file.name !== 'blob') {
        return file.name;
    }
    const stamp = now.toISOString().replace(/[-:]/g, '').replace('T', '-').slice(0, 15);
    const extension = (file.type.split('/')[1] ?? 'bin').replace('jpeg', 'jpg').replace(/[^a-z0-9]/g, '');
    return `screenshot-${stamp}.${extension}`;
}

export function mount(root, win) {
    const doc = win.document;
    const input = root.querySelector('[data-editor-input]');
    const status = root.querySelector('[data-editor-status]');
    const attach = root.querySelector('[data-editor-attach]');
    const picker = root.querySelector('[data-editor-files]');
    const toggle = root.querySelector('[data-editor-toggle]');
    const preview = root.querySelector('[data-editor-preview]');
    if (!input) {
        return;
    }
    if (attach) {
        attach.hidden = false;
    }
    if (toggle && preview) {
        toggle.hidden = false;
    }
    let pending = 0;

    const token = () => root.closest('form')?.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    const say = text => {
        if (status) {
            status.value = text;
            status.textContent = text;
        }
    };

    const insert = text => {
        const start = input.selectionStart ?? input.value.length;
        const end = input.selectionEnd ?? start;
        const before = input.value.slice(0, start);
        const spacer = before.length > 0 && !before.endsWith('\n') && text.startsWith('!') ? '\n' : '';
        input.setRangeText(spacer + text, start, end, 'end');
        input.dispatchEvent(new win.Event('input', { bubbles: true }));
    };

    const replace = (marker, text) => {
        const at = input.value.indexOf(marker);
        if (at >= 0) {
            input.setRangeText(text, at, at + marker.length, 'preserve');
            input.dispatchEvent(new win.Event('input', { bubbles: true }));
        }
    };

    const upload = async file => {
        const name = pastedName(file);
        const marker = `[Uploading ${name.replace(/[[\]]/g, '')}…]`;
        insert(marker);
        pending += 1;
        say(`Uploading ${pending === 1 ? name : `${pending} files`}…`);
        try {
            const response = await win.fetch(root.dataset.upload, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    RequestVerificationToken: token(),
                    'X-File-Name': encodeURIComponent(name),
                    'Content-Type': file.type || 'application/octet-stream',
                },
                body: file,
            });
            const body = await response.json().catch(() => ({}));
            if (!response.ok || !body.url) {
                replace(marker, '');
                say(body.error ?? `Could not upload ${name}.`);
                return;
            }
            replace(marker, markdownFor(body));
            say(`Uploaded ${body.name} (${body.size}). Save to keep the link.`);
        } catch {
            replace(marker, '');
            say(`Could not upload ${name}. Check the connection and try again.`);
        } finally {
            pending -= 1;
        }
    };

    const uploadAll = files => {
        for (const file of files) {
            upload(file);
        }
    };

    input.addEventListener('paste', event => {
        const data = event.clipboardData;
        if (!data) {
            return;
        }
        const files = [...data.files];
        if (files.length > 0) {
            event.preventDefault();
            uploadAll(files);
            return;
        }
        const html = data.getData('text/html');
        if (html && 'DOMParser' in win) {
            const markdown = htmlToMarkdown(html, new win.DOMParser());
            if (markdown.length > 0) {
                event.preventDefault();
                insert(markdown);
            }
        }
    });

    input.addEventListener('dragover', event => {
        if (event.dataTransfer?.types?.includes('Files')) {
            event.preventDefault();
            root.classList.add('dragging');
        }
    });
    input.addEventListener('dragleave', () => root.classList.remove('dragging'));
    input.addEventListener('drop', event => {
        root.classList.remove('dragging');
        const files = [...(event.dataTransfer?.files ?? [])];
        if (files.length > 0) {
            event.preventDefault();
            input.focus();
            uploadAll(files);
        }
    });

    picker?.addEventListener('change', () => {
        uploadAll([...picker.files]);
        picker.value = '';
    });

    input.addEventListener('keydown', event => {
        if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
            event.preventDefault();
            input.closest('form')?.requestSubmit();
        }
    });

    input.closest('form')?.addEventListener('submit', event => {
        if (pending > 0) {
            event.preventDefault();
            say('Wait for the uploads to finish.');
        }
    });

    toggle?.addEventListener('click', async () => {
        if (!preview) {
            return;
        }
        if (!preview.hidden) {
            preview.hidden = true;
            toggle.textContent = 'Preview';
            return;
        }
        toggle.disabled = true;
        try {
            const response = await win.fetch(root.dataset.preview, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { RequestVerificationToken: token(), 'Content-Type': 'text/plain; charset=utf-8' },
                body: input.value,
            });
            preview.innerHTML = response.ok ? await response.text() : '<p class="muted">Preview is not available right now.</p>';
            preview.hidden = false;
            toggle.textContent = 'Edit';
        } catch {
            say('Preview is not available right now.');
        } finally {
            toggle.disabled = false;
        }
    });

    doc.addEventListener('visibilitychange', () => {
        if (doc.visibilityState === 'hidden' && pending > 0) {
            say('Uploads continue in the background.');
        }
    });
}

import { describe, expect, it } from 'vitest';
import { isWebUrl, markdownFor, pastedName, tidy } from '../../../src/MyBlazorPlayground.Web/wwwroot/work/editor.js';
import { targetStatus } from '../../../src/MyBlazorPlayground.Web/wwwroot/work/board.js';
import { ENHANCERS, enhance } from '../../../src/MyBlazorPlayground.Web/Components/App.razor.js';

describe('isWebUrl', () => {
    it('accepts web and mail links only', () => {
        expect(isWebUrl('https://example.com/a')).toBe(true);
        expect(isWebUrl('http://example.com')).toBe(true);
        expect(isWebUrl('mailto:someone@example.com')).toBe(true);
        expect(isWebUrl('javascript:alert(1)')).toBe(false);
        expect(isWebUrl('data:text/html,hi')).toBe(false);
        expect(isWebUrl('/relative')).toBe(false);
    });
});

describe('tidy', () => {
    it('collapses blank runs and trailing spaces', () => {
        expect(tidy('  a  \n\n\n\nb \n')).toBe('a\n\nb');
    });
});

describe('markdownFor', () => {
    it('embeds images and links other files', () => {
        expect(markdownFor({ name: 'shot.png', url: '/work/files/1/shot.png', image: true })).toBe('![shot.png](/work/files/1/shot.png)');
        expect(markdownFor({ name: 'notes [v2].pdf', url: '/work/files/2/notes.pdf', image: false })).toBe('[notes v2.pdf](/work/files/2/notes.pdf)');
    });
});

describe('pastedName', () => {
    const now = new Date('2026-11-19T10:24:05Z');

    it('keeps real file names', () => {
        expect(pastedName({ name: 'diagram.png', type: 'image/png' }, now)).toBe('diagram.png');
    });

    it('names clipboard screenshots by time', () => {
        expect(pastedName({ name: 'image.png', type: 'image/png' }, now)).toBe('screenshot-20261119-102405.png');
        expect(pastedName({ name: '', type: 'image/jpeg' }, now)).toBe('screenshot-20261119-102405.jpg');
    });
});

describe('board', () => {
    it('reads the status of a column', () => {
        expect(targetStatus({ dataset: { status: 'done' } })).toBe('done');
        expect(targetStatus(null)).toBeNull();
    });
});

describe('enhance', () => {
    it('mounts each element once per module', async () => {
        const editor = { id: 'editor' };
        const board = { id: 'board' };
        const win = {
            document: {
                querySelectorAll: selector => (selector === '[data-editor]' ? [editor] : selector === '[data-board]' ? [board] : []),
            },
        };
        const mounted = [];
        const load = async path => ({ mount: element => mounted.push([path, element.id]) });
        const seen = new WeakSet();
        await enhance(win, load, seen);
        await enhance(win, load, seen);
        expect(mounted).toEqual([
            [ENHANCERS[0].path, 'editor'],
            [ENHANCERS[1].path, 'board'],
        ]);
    });
});

import { describe, expect, it } from 'vitest';
import { TONES, showStatus } from '../../../src/MyBlazorPlayground.Web/wwwroot/js/status.js';

function output() {
    return { textContent: 'old', dataset: {} };
}

describe('showStatus', () => {
    it('shows problems without a tone', () => {
        const element = output();
        element.dataset.tone = 'ok';
        showStatus(element, 'Fix the highlighted values.');
        expect(element.textContent).toBe('Fix the highlighted values.');
        expect(element.dataset.tone).toBeUndefined();
    });

    it('marks good news and notes', () => {
        const element = output();
        showStatus(element, 'Saved.', 'ok');
        expect(element.dataset.tone).toBe('ok');
        showStatus(element, 'Nothing changed.', 'note');
        expect(element.dataset.tone).toBe('note');
    });

    it('ignores unknown tones and clears with the text', () => {
        const element = output();
        showStatus(element, 'Hello', 'shout');
        expect(element.dataset.tone).toBeUndefined();
        showStatus(element, 'Saved.', 'ok');
        showStatus(element, null, 'ok');
        expect(element.textContent).toBe('');
        expect(element.dataset.tone).toBeUndefined();
    });

    it('tolerates a missing element', () => {
        expect(() => showStatus(null, 'Saved.', 'ok')).not.toThrow();
        expect(TONES).toEqual(['ok', 'note']);
    });
});

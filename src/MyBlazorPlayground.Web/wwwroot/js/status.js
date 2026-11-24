export const TONES = Object.freeze(['ok', 'note']);

export function showStatus(element, text, tone = null) {
    if (!element) {
        return;
    }
    const value = text ?? '';
    element.textContent = value;
    if (value && TONES.includes(tone)) {
        element.dataset.tone = tone;
    } else {
        delete element.dataset.tone;
    }
}

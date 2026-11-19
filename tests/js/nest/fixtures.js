import { readFileSync } from 'node:fs';

export function fixture(name) {
    return JSON.parse(readFileSync(new URL(`../../fixtures/nest/${name}.json`, import.meta.url), 'utf8'));
}

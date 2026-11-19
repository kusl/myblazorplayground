import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

const repository = fileURLToPath(new URL('../../', import.meta.url));
const web = `${repository}src/MyBlazorPlayground.Web/`;

export default defineConfig({
    resolve: {
        alias: [
            { find: /^(?:\.\.\/)+js\//, replacement: `${web}wwwroot/js/` },
            { find: /^(?:\.\.\/)+work\//, replacement: `${web}wwwroot/work/` },
        ],
    },
    server: {
        fs: {
            allow: [repository],
        },
    },
    test: {
        include: ['**/*.test.js'],
        environment: 'node',
        coverage: {
            provider: 'v8',
            allowExternal: true,
            include: [`${web}**/*.js`],
            exclude: [`${web}bin/**`, `${web}obj/**`],
            reporter: ['text', 'cobertura'],
            reportsDirectory: `${repository}artifacts/coverage/js`,
        },
    },
});

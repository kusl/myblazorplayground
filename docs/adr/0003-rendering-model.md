# 0003 Static forms for work and accounts, islands for live pages

Status: accepted

- Accounts, administration and the project tracker are static server-side rendering with plain `<form method="post">` and `EditForm`, enhanced by Blazor when its script is loaded. They need no circuit, so they keep working over a slow or flaky quick tunnel and they work without JavaScript.
- Each row action is its own small form with a unique `@formname` (`up-12`, `restore-<id>`). After an action the page reloads its data and renders the result; enhanced forms keep the URL and history clean.
- Homes and the toys keep their interactive islands (ADR `homes/0009`, `toys/0007`).
- Progressive enhancement: `App.razor.js` mounts `wwwroot/work/editor.js` on `[data-editor]` (paste and drop uploads, preview, Ctrl+Enter) and `wwwroot/work/board.js` on `[data-board]` (drag and drop, status change on select). Without them, every action still works through the forms.
- Mobile first: board columns scroll and snap sideways below 48 rem, buttons and inputs are at least 44 px tall (small row buttons 34 px), nothing scrolls the page sideways from 320 px up.
- Form models give missing values their defaults in their setters, because form binding assigns every property, including the ones a form did not post.

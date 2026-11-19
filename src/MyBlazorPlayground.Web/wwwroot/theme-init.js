(() => {
    let theme = null;
    try {
        theme = localStorage.getItem('playground-theme');
    } catch {
    }
    if (theme === 'light' || theme === 'dark') {
        document.documentElement.dataset.theme = theme;
    }
})();

// Expose startup failures without displaying server exception details or patient data.
(() => {
    const banner = document.getElementById("connection-startup");
    const show = () => { if (banner) banner.hidden = false; };
    const timer = window.setTimeout(show, 15000);
    if (!window.Blazor) {
        window.clearTimeout(timer);
        show();
        return;
    }
    window.Blazor.start().then(() => {
        window.clearTimeout(timer);
        if (banner) banner.hidden = true;
    }).catch(() => {
        window.clearTimeout(timer);
        show();
    });
})();

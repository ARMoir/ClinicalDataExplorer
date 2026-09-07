let cleanup;
export function start(receiver) {
    stop();
    const report = (kind, control) => receiver.invokeMethodAsync("Observe", kind, control).catch(() => {
        if (document.getElementById("audit-failure")) return;
        const warning = document.createElement("div");
        warning.id = "audit-failure";
        warning.className = "error-card";
        warning.setAttribute("role", "alert");
        warning.textContent = "Interface activity could not be audited. Reload the page or contact an administrator.";
        document.querySelector("main")?.prepend(warning);
    });
    const handler = event => {
        const element = event.target.closest?.("[data-audit],button,a,input,select,textarea,summary,details,form");
        if (!element || element.disabled) return;
        // Never send text content, entered values, hrefs, or DOM snapshots.
        report(event.type, element.dataset.audit || element.id || element.tagName.toLowerCase());
    };
    const print = () => report("print-dialog", "browser-print");
    for (const kind of ["click", "change", "submit", "toggle"]) document.addEventListener(kind, handler, true);
    window.addEventListener("beforeprint", print);
    cleanup = () => {
        for (const kind of ["click", "change", "submit", "toggle"]) document.removeEventListener(kind, handler, true);
        window.removeEventListener("beforeprint", print);
    };
}
export function stop() { cleanup?.(); cleanup = undefined; }

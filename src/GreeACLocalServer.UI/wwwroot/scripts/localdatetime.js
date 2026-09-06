// Formats a UTC ISO-8601 timestamp using the *browser's* locale and time zone.
// Used by the <LocalDateTime> component so timestamps render in the visitor's
// zone regardless of whether the component is running interactively on the
// server (circuit / prerender: server process TZ) or in WebAssembly.
window.gacLocalDateTime = {
    format: function (isoUtc) {
        try {
            const d = new Date(isoUtc);
            if (isNaN(d.getTime())) {
                return isoUtc;
            }
            return d.toLocaleString();
        } catch {
            return isoUtc;
        }
    }
};

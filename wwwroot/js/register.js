document.addEventListener('alpine:init', () => {
    Alpine.data('registerUi', () => ({
        matching: false,
        init() {
            window.PolicyPOC?.auth?.applyNavState();
        },
        updateMatch() {
            const password = this.$refs.registerPassword?.value ?? '';
            const confirm = this.$refs.registerConfirm?.value ?? '';
            this.matching = password.length >= 6 && password === confirm;
        }
    }));
});

document.addEventListener('htmx:afterRequest', (event) => {
    const target = event.detail.target;
    if (!target || !target.dataset.responseTarget) {
        return;
    }

    let html;
    try {
        const payload = event.detail.xhr.response ? JSON.parse(event.detail.xhr.response) : null;
        if (payload?.email) {
            html = `
                <div class="alert alert-success mb-0">
                    <p class="mb-1">User <strong>${payload.email}</strong> was created successfully.</p>
                    <p class="mb-0 text-muted">You can now <a href="/login.html" class="alert-link">sign in</a> using the credentials above.</p>
                </div>`;
        } else if (payload?.message) {
            html = `<div class="alert alert-info mb-0">${payload.message}</div>`;
        } else if (payload?.errors) {
            const errors = Object.values(payload.errors).flat().join('<br>');
            html = `<div class="alert alert-warning mb-0">${errors}</div>`;
        } else if (payload) {
            html = `<div class="alert alert-info mb-0">${JSON.stringify(payload)}</div>`;
        } else {
            html = `<div class="alert alert-secondary mb-0">Empty response.</div>`;
        }
    } catch {
        html = `<div class="alert alert-danger mb-0">${event.detail.xhr.responseText || 'Unexpected response from server.'}</div>`;
    }

    target.innerHTML = html;
});

document.addEventListener('DOMContentLoaded', () => {
    window.PolicyPOC?.auth?.applyNavState();
});


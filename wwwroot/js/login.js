document.addEventListener('htmx:afterRequest', (event) => {
    const target = event.detail.target;
    if (!target || target.dataset.responseTarget === undefined) {
        return;
    }

    const isSuccess = event.detail.successful;
    let html;

    try {
        const payload = event.detail.xhr.response ? JSON.parse(event.detail.xhr.response) : null;
        
        if (isSuccess && payload?.token) {
            window.PolicyPOC?.auth?.save(payload);
            window.PolicyPOC?.auth?.applyNavState();

            html = `
                <div class="alert alert-success mb-0">
                    <h6 class="mb-1">Welcome back, ${payload.displayName ?? payload.email ?? 'user'}!</h6>
                    <p class="mb-0">Redirecting to dashboard...</p>
                </div>`;

            setTimeout(() => {
                window.location.replace('/index.html');
            }, 800);
        } else if (payload?.message) {
            html = `<div class="alert alert-danger mb-0"><strong>Login failed:</strong> ${payload.message}</div>`;
        } else if (payload?.errors) {
            const errors = Object.values(payload.errors).flat().join('<br>');
            html = `<div class="alert alert-danger mb-0"><strong>Login failed:</strong><br>${errors}</div>`;
        } else if (!isSuccess) {
            html = `<div class="alert alert-danger mb-0">Login failed. Please check your credentials and try again.</div>`;
        } else {
            html = `<div class="alert alert-warning mb-0">Unexpected response from server.</div>`;
        }
    } catch (error) {
        html = `<div class="alert alert-danger mb-0">Login failed: ${event.detail.xhr.responseText || 'Network error. Please try again.'}</div>`;
    }

    target.innerHTML = html;
});

document.addEventListener('DOMContentLoaded', () => {
    const auth = window.PolicyPOC?.auth;
    if (auth?.isAuthenticated()) {
        window.location.replace('/index.html');
        return;
    }

    auth?.applyNavState();
});


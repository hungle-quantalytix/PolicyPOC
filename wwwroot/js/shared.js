(() => {
    const STORAGE_KEY = 'policypoc.auth';

    const safeParse = (raw) => {
        try {
            return raw ? JSON.parse(raw) : null;
        } catch {
            return null;
        }
    };

    const load = () => safeParse(localStorage.getItem(STORAGE_KEY));

    const normalize = (payload) => {
        if (!payload?.token) {
            return null;
        }

        return {
            token: payload.token,
            displayName: payload.displayName ?? payload.email ?? 'User',
            email: payload.email ?? '',
            expiresAtUtc: payload.expiresAtUtc ?? null,
            roles: payload.roles ?? []
        };
    };

    const save = (payload) => {
        const record = normalize(payload);
        if (!record) {
            return;
        }

        localStorage.setItem(STORAGE_KEY, JSON.stringify(record));
    };

    const clear = () => localStorage.removeItem(STORAGE_KEY);

    const isExpired = (auth) => {
        if (!auth?.expiresAtUtc) {
            return false;
        }

        const expires = Date.parse(auth.expiresAtUtc);
        return Number.isFinite(expires) && expires <= Date.now();
    };

    const hasValidAuth = () => {
        const auth = load();
        if (!auth?.token) {
            return false;
        }

        if (isExpired(auth)) {
            clear();
            return false;
        }

        return true;
    };

    const ensureAuthenticated = (redirectTo = '/login.html') => {
        if (!hasValidAuth()) {
            window.location.replace(redirectTo);
        }
    };

    const initialsFrom = (displayName = '', fallback = 'U') => {
        const letters = displayName
            .trim()
            .split(/\s+/)
            .filter(Boolean)
            .map((chunk) => chunk[0]?.toUpperCase())
            .filter(Boolean)
            .slice(0, 2);

        if (letters.length === 0 && fallback) {
            return fallback[0]?.toUpperCase() ?? 'U';
        }

        return letters.join('');
    };

    const applyNavState = () => {
        const nav = document.querySelector('[data-auth-nav]');
        if (!nav) {
            return;
        }

        const loggedIn = hasValidAuth();
        const auth = load();

        const loginLink = nav.querySelector('[data-auth-link="login"]');
        const registerLink = nav.querySelector('[data-auth-link="register"]');
        const avatar = nav.querySelector('[data-auth-avatar]');
        const authMenuItems = nav.querySelectorAll('[data-auth-menu]');
        const adminMenuItems = nav.querySelectorAll('[data-auth-adminmenu]');
        const nameEl = nav.querySelector('[data-auth-name]');
        const emailEl = nav.querySelector('[data-auth-email]');
        const initialsEl = nav.querySelector('[data-avatar-initials]');

        if (loginLink) {
            loginLink.classList.toggle('d-none', loggedIn);
        }

        if (registerLink) {
            registerLink.classList.toggle('d-none', loggedIn);
        }

        if (avatar) {
            avatar.classList.toggle('d-none', !loggedIn);
        }

        // Show menu items for all authenticated users
        if (authMenuItems.length > 0) {
            authMenuItems.forEach(item => {
                item.classList.toggle('d-none', !loggedIn);
            });
        }

        // Show admin menu items for users with Administrator, SuperAdmin, or Lender Admin role
        if (adminMenuItems.length > 0) {
            const isAdminUser = loggedIn && auth?.roles?.some(role => 
                role === 'Administrator' || role === 'SuperAdmin'
            );
            adminMenuItems.forEach(item => {
                item.classList.toggle('d-none', !isAdminUser);
            });
        }

        if (loggedIn && auth) {
            nameEl && (nameEl.textContent = auth.displayName ?? 'User');
            emailEl && (emailEl.textContent = auth.email ?? '');
            initialsEl && (initialsEl.textContent = initialsFrom(auth.displayName, auth.email));
        } else {
            nameEl && (nameEl.textContent = 'Guest');
            emailEl && (emailEl.textContent = 'Not signed in');
            initialsEl && (initialsEl.textContent = '?');
        }

        // Attach logout button event listener
        nav.querySelectorAll('[data-logout]').forEach((btn) => {
            btn.addEventListener('click', logout);
        });
    };

    const logout = () => {
        clear();
        window.location.replace('/login.html');
    };

    document.addEventListener('DOMContentLoaded', () => {
        applyNavState();
    });

    window.PolicyPOC = window.PolicyPOC ?? {};
    window.PolicyPOC.auth = {
        load,
        save,
        clear,
        logout,
        ensureAuthenticated,
        isAuthenticated: hasValidAuth,
        applyNavState
    };
})();


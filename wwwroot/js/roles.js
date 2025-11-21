document.addEventListener('alpine:init', () => {
    Alpine.data('rolesPage', () => ({
        roles: [],
        loading: false,
        successMessage: '',
        errorMessage: '',
        showCreateModal: false,
        newRoleName: '',
        creatingRole: false,
        createError: '',

        async init() {
            // Ensure user is authenticated
            if (window.PolicyPOC?.auth?.ensureAuthenticated) {
                window.PolicyPOC.auth.ensureAuthenticated();
            }

            await this.loadRoles();
        },

        async loadRoles() {
            this.loading = true;
            this.errorMessage = '';

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch('/api/roles', {
                    headers: {
                        'Authorization': `Bearer ${auth.token}`
                    }
                });

                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    if (response.status === 403) {
                        throw new Error('Access denied. You must be an administrator to view roles.');
                    }
                    throw new Error(`Failed to load roles: ${response.statusText}`);
                }

                const data = await response.json();
                this.roles = data || [];
            } catch (error) {
                console.error('Error loading roles:', error);
                this.errorMessage = error.message || 'Failed to load roles. Please try again.';
            } finally {
                this.loading = false;
            }
        },

        async createRole() {
            if (!this.newRoleName.trim()) {
                this.createError = 'Role name is required.';
                return;
            }

            if (this.newRoleName.trim().length < 2) {
                this.createError = 'Role name must be at least 2 characters.';
                return;
            }

            this.creatingRole = true;
            this.createError = '';

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch('/api/roles', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${auth.token}`
                    },
                    body: JSON.stringify({
                        name: this.newRoleName.trim()
                    })
                });

                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    if (response.status === 403) {
                        throw new Error('Access denied. You must be an administrator to create roles.');
                    }
                    if (response.status === 409) {
                        throw new Error('A role with this name already exists.');
                    }

                    const errorData = await response.json().catch(() => null);
                    if (errorData?.errors) {
                        const errorMessages = Object.values(errorData.errors).flat();
                        throw new Error(errorMessages.join(', '));
                    }

                    throw new Error(`Failed to create role: ${response.statusText}`);
                }

                this.successMessage = `Role "${this.newRoleName.trim()}" created successfully!`;
                this.closeCreateModal();
                await this.loadRoles();

                // Clear success message after 5 seconds
                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);
            } catch (error) {
                console.error('Error creating role:', error);
                this.createError = error.message || 'Failed to create role. Please try again.';
            } finally {
                this.creatingRole = false;
            }
        },

        closeCreateModal() {
            this.showCreateModal = false;
            this.newRoleName = '';
            this.createError = '';
            this.creatingRole = false;
        }
    }));
});


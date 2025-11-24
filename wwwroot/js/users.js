document.addEventListener('alpine:init', () => {
    Alpine.data('usersPage', () => ({
        users: [],
        roles: [],
        loading: false,
        successMessage: '',
        errorMessage: '',
        showCreateModal: false,
        showAssignModal: false,
        newUser: {
            email: '',
            displayName: '',
            department: '',
            password: ''
        },
        creatingUser: false,
        createError: '',
        selectedUser: null,
        selectedRole: '',
        assigningRole: false,
        removingRole: false,
        assignError: '',

        async init() {
            // Ensure user is authenticated
            if (window.PolicyPOC?.auth?.ensureAuthenticated) {
                window.PolicyPOC.auth.ensureAuthenticated();
            }

            await this.loadData();
        },

        async loadData() {
            this.loading = true;
            this.errorMessage = '';

            try {
                await Promise.all([
                    this.loadUsers(),
                    this.loadRoles()
                ]);
            } catch (error) {
                console.error('Error loading data:', error);
                this.errorMessage = error.message || 'Failed to load data. Please try again.';
            } finally {
                this.loading = false;
            }
        },

        async loadUsers() {
            const auth = window.PolicyPOC?.auth?.load();
            if (!auth?.token) {
                throw new Error('Not authenticated');
            }

            const response = await fetch('/api/users', {
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
                    throw new Error('Access denied. You must be an administrator to view users.');
                }
                throw new Error(`Failed to load users: ${response.statusText}`);
            }

            const data = await response.json();
            this.users = data || [];
        },

        async loadRoles() {
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
        },

        getInitials(name) {
            if (!name) return '?';
            
            const parts = name.trim().split(/\s+/);
            if (parts.length === 0) return '?';
            
            if (parts.length === 1) {
                return parts[0].substring(0, 2).toUpperCase();
            }
            
            return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
        },

        get availableRolesForUser() {
            if (!this.selectedUser || !this.roles.length) {
                return [];
            }

            const userRoles = this.selectedUser.roles || [];
            return this.roles.filter(role => !userRoles.includes(role));
        },

        openCreateUserModal() {
            this.newUser = {
                email: '',
                displayName: '',
                department: '',
                password: ''
            };
            this.createError = '';
            this.showCreateModal = true;
        },

        closeCreateModal() {
            this.showCreateModal = false;
            this.newUser = {
                email: '',
                displayName: '',
                department: '',
                password: ''
            };
            this.createError = '';
            this.creatingUser = false;
        },

        async createUser() {
            if (!this.newUser.email || !this.newUser.password) {
                this.createError = 'Email and password are required.';
                return;
            }

            this.creatingUser = true;
            this.createError = '';

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch('/api/users', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${auth.token}`
                    },
                    body: JSON.stringify({
                        email: this.newUser.email,
                        displayName: this.newUser.displayName || null,
                        department: this.newUser.department || null,
                        password: this.newUser.password
                    })
                });

                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    if (response.status === 403) {
                        throw new Error('Access denied. You must be an administrator to create users.');
                    }
                    if (response.status === 409) {
                        throw new Error('A user with this email already exists.');
                    }

                    const errorData = await response.json().catch(() => null);
                    if (errorData?.errors) {
                        const errorMessages = Object.values(errorData.errors).flat();
                        throw new Error(errorMessages.join(', '));
                    }

                    throw new Error(`Failed to create user: ${response.statusText}`);
                }

                this.successMessage = `User "${this.newUser.email}" created successfully!`;
                this.closeCreateModal();
                await this.loadUsers();

                // Clear success message after 5 seconds
                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);
            } catch (error) {
                console.error('Error creating user:', error);
                this.createError = error.message || 'Failed to create user. Please try again.';
            } finally {
                this.creatingUser = false;
            }
        },

        openAssignRoleModal(user) {
            this.selectedUser = user;
            this.selectedRole = '';
            this.assignError = '';
            this.showAssignModal = true;
        },

        closeAssignModal() {
            this.showAssignModal = false;
            this.selectedUser = null;
            this.selectedRole = '';
            this.assignError = '';
            this.assigningRole = false;
        },

        async assignRole() {
            if (!this.selectedUser || !this.selectedRole) {
                this.assignError = 'Please select a role.';
                return;
            }

            this.assigningRole = true;
            this.assignError = '';

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch('/api/roles/assign', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${auth.token}`
                    },
                    body: JSON.stringify({
                        userEmail: this.selectedUser.email,
                        roleName: this.selectedRole
                    })
                });

                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    if (response.status === 403) {
                        throw new Error('Access denied. You must be an administrator to assign roles.');
                    }
                    if (response.status === 404) {
                        const errorData = await response.json().catch(() => null);
                        throw new Error(errorData?.message || 'User or role not found.');
                    }

                    const errorData = await response.json().catch(() => null);
                    if (errorData?.errors) {
                        const errorMessages = Object.values(errorData.errors).flat();
                        throw new Error(errorMessages.join(', '));
                    }

                    throw new Error(`Failed to assign role: ${response.statusText}`);
                }

                this.successMessage = `Role "${this.selectedRole}" assigned to ${this.selectedUser.displayName || this.selectedUser.email} successfully!`;
                this.closeAssignModal();
                await this.loadUsers();

                // Clear success message after 5 seconds
                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);
            } catch (error) {
                console.error('Error assigning role:', error);
                this.assignError = error.message || 'Failed to assign role. Please try again.';
            } finally {
                this.assigningRole = false;
            }
        },

        async removeRole(user, roleName) {
            if (!confirm(`Remove role "${roleName}" from ${user.displayName || user.email}?`)) {
                return;
            }

            this.removingRole = true;
            this.errorMessage = '';

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch(`/api/users/${user.id}/roles/${encodeURIComponent(roleName)}`, {
                    method: 'POST',
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
                        throw new Error('Access denied. You must be an administrator to remove roles.');
                    }
                    if (response.status === 404) {
                        throw new Error('User not found.');
                    }

                    const errorData = await response.json().catch(() => null);
                    if (errorData?.errors) {
                        const errorMessages = Object.values(errorData.errors).flat();
                        throw new Error(errorMessages.join(', '));
                    }

                    throw new Error(`Failed to remove role: ${response.statusText}`);
                }

                this.successMessage = `Role "${roleName}" removed from ${user.displayName || user.email} successfully!`;
                await this.loadUsers();

                // Clear success message after 5 seconds
                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);
            } catch (error) {
                console.error('Error removing role:', error);
                this.errorMessage = error.message || 'Failed to remove role. Please try again.';
            } finally {
                this.removingRole = false;
            }
        }
    }));
});


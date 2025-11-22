document.addEventListener('alpine:init', () => {
    Alpine.data('resourcesPage', () => ({
        resources: [],
        loading: false,
        successMessage: '',
        errorMessage: '',
        showModal: false,
        showDeleteModal: false,
        isEditing: false,
        currentResourceId: null,
        formData: {},
        formError: '',
        submitting: false,
        deleting: false,
        resourceToDelete: null,

        async init() {
            // Ensure user is authenticated
            if (window.PolicyPOC?.auth?.ensureAuthenticated) {
                window.PolicyPOC.auth.ensureAuthenticated();
            }

            await this.loadResources();
        },

        resetFormData() {
            this.formData = {
                resourceName: ''
            };
        },

        async loadResources() {
            this.loading = true;
            this.errorMessage = '';

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch('/api/resources', {
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
                        throw new Error('Access denied. You do not have permission to view resources.');
                    }
                    throw new Error(`Failed to load resources: ${response.statusText}`);
                }

                const data = await response.json();
                this.resources = data || [];
            } catch (error) {
                console.error('Error loading resources:', error);
                this.errorMessage = error.message || 'Failed to load resources. Please try again.';
            } finally {
                this.loading = false;
            }
        },

        openCreateModal() {
            this.isEditing = false;
            this.currentResourceId = null;
            this.resetFormData();
            this.formError = '';
            this.showModal = true;
        },

        openEditModal(resource) {
            this.isEditing = true;
            this.currentResourceId = resource.id;
            this.formData = {
                resourceName: resource.resourceName
            };
            this.formError = '';
            this.showModal = true;
        },

        closeModal() {
            this.showModal = false;
            this.isEditing = false;
            this.currentResourceId = null;
            this.formError = '';
            this.resetFormData();
        },

        async submitForm() {
            this.formError = '';
            this.submitting = true;

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const url = this.isEditing ? `/api/resources/${this.currentResourceId}` : '/api/resources';
                const method = this.isEditing ? 'PUT' : 'POST';

                const response = await fetch(url, {
                    method: method,
                    headers: {
                        'Authorization': `Bearer ${auth.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(this.formData)
                });

                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    if (response.status === 403) {
                        throw new Error('Access denied. You do not have permission to perform this action.');
                    }
                    
                    const errorData = await response.json().catch(() => null);
                    if (errorData?.errors) {
                        const errorMessages = Object.values(errorData.errors).flat().join(', ');
                        throw new Error(errorMessages);
                    }
                    throw new Error(`Failed to ${this.isEditing ? 'update' : 'create'} resource: ${response.statusText}`);
                }

                this.successMessage = this.isEditing ? 
                    'Resource updated successfully!' : 
                    'Resource created successfully!';
                
                this.closeModal();
                await this.loadResources();

                // Clear success message after 5 seconds
                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);

            } catch (error) {
                console.error('Error submitting form:', error);
                this.formError = error.message || 'An error occurred. Please try again.';
            } finally {
                this.submitting = false;
            }
        },

        confirmDelete(resource) {
            this.resourceToDelete = resource;
            this.showDeleteModal = true;
        },

        async deleteResource() {
            if (!this.resourceToDelete) return;

            this.deleting = true;

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch(`/api/resources/${this.resourceToDelete.id}`, {
                    method: 'DELETE',
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
                        throw new Error('Access denied. You do not have permission to delete resources.');
                    }
                    throw new Error(`Failed to delete resource: ${response.statusText}`);
                }

                this.successMessage = 'Resource deleted successfully!';
                this.showDeleteModal = false;
                this.resourceToDelete = null;
                await this.loadResources();

                // Clear success message after 5 seconds
                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);

            } catch (error) {
                console.error('Error deleting resource:', error);
                this.errorMessage = error.message || 'Failed to delete resource. Please try again.';
                this.showDeleteModal = false;
            } finally {
                this.deleting = false;
            }
        }
    }));
});


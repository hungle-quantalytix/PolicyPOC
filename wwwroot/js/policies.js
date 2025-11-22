// Policy Management Page
document.addEventListener('alpine:init', () => {
    Alpine.data('policiesPage', () => ({
        // State
        policies: [],
        loading: false,
        successMessage: '',
        errorMessage: '',
        
        // Modal states
        showModal: false,
        showAssignModal: false,
        showDeleteModal: false,
        isEditing: false,
        
        // Form data
        formData: {
            description: '',
            policyData: ''
        },
        assignFormData: {
            resourceName: '',
            resourceColumns: ''
        },
        formError: '',
        assignFormError: '',
        submitting: false,
        assigning: false,
        deleting: false,
        
        // Selected items
        currentPolicy: null,
        policyToAssign: null,
        policyToDelete: null,

        // Initialize
        async init() {
            // Ensure user is authenticated
            if (window.PolicyPOC?.auth?.ensureAuthenticated) {
                window.PolicyPOC.auth.ensureAuthenticated();
            }
            
            await this.loadPolicies();
        },

        // Load all policies
        async loadPolicies() {
            this.loading = true;
            this.errorMessage = '';
            
            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch('/api/Policies', {
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
                        throw new Error('Access denied. You do not have permission to view policies.');
                    }
                    throw new Error(`Failed to load policies: ${response.statusText}`);
                }

                const data = await response.json();
                this.policies = data || [];
            } catch (error) {
                console.error('Error loading policies:', error);
                this.errorMessage = error.message || 'Failed to load policies. Please try again.';
            } finally {
                this.loading = false;
            }
        },

        // Open create modal
        openCreateModal() {
            this.isEditing = false;
            this.currentPolicy = null;
            this.formData = {
                description: '',
                policyData: '{\n  "version": "1.0",\n  "statement": []\n}'
            };
            this.formError = '';
            this.showModal = true;
        },

        // Open edit modal
        openEditModal(policy) {
            this.isEditing = true;
            this.currentPolicy = policy;
            this.formData = {
                description: policy.description || '',
                policyData: policy.policyData
            };
            this.formError = '';
            this.showModal = true;
        },

        // Close create/edit modal
        closeModal() {
            this.showModal = false;
            this.formError = '';
        },

        // Submit create/edit form
        async submitForm() {
            this.formError = '';
            this.submitting = true;

            try {
                // Validate JSON
                try {
                    JSON.parse(this.formData.policyData);
                } catch (e) {
                    this.formError = 'Invalid JSON format in Policy Data';
                    this.submitting = false;
                    return;
                }

                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const url = this.isEditing ? `/api/Policies/${this.currentPolicy.id}` : '/api/Policies';
                const method = this.isEditing ? 'PUT' : 'POST';

                const response = await fetch(url, {
                    method: method,
                    headers: {
                        'Authorization': `Bearer ${auth.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        description: this.formData.description || null,
                        policyData: this.formData.policyData
                    })
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
                    throw new Error(`Failed to ${this.isEditing ? 'update' : 'create'} policy: ${response.statusText}`);
                }

                this.successMessage = this.isEditing ? 
                    'Policy updated successfully!' : 
                    'Policy created successfully!';

                this.closeModal();
                await this.loadPolicies();

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

        // Open assign policy modal
        openAssignModal(policy) {
            this.policyToAssign = policy;
            this.assignFormData = {
                resourceName: '',
                resourceColumns: ''
            };
            this.assignFormError = '';
            this.showAssignModal = true;
        },

        // Close assign modal
        closeAssignModal() {
            this.showAssignModal = false;
            this.assignFormError = '';
            this.policyToAssign = null;
        },

        // Submit assign form
        async submitAssignForm() {
            this.assignFormError = '';
            this.assigning = true;

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch('/api/Policies/assign', {
                    method: 'POST',
                    headers: {
                        'Authorization': `Bearer ${auth.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        policyId: this.policyToAssign.id,
                        resourceName: this.assignFormData.resourceName,
                        resourceColumns: this.assignFormData.resourceColumns || null
                    })
                });

                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    if (response.status === 403) {
                        throw new Error('Access denied. You do not have permission to assign policies.');
                    }
                    
                    const errorData = await response.json().catch(() => null);
                    if (errorData?.errors) {
                        const errorMessages = Object.values(errorData.errors).flat().join(', ');
                        throw new Error(errorMessages);
                    }
                    throw new Error(`Failed to assign policy: ${response.statusText}`);
                }

                this.successMessage = `Policy assigned to resource "${this.assignFormData.resourceName}" successfully!`;
                this.closeAssignModal();
                await this.loadPolicies();

                // Clear success message after 5 seconds
                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);

            } catch (error) {
                console.error('Error assigning policy:', error);
                this.assignFormError = error.message || 'Failed to assign policy. Please try again.';
            } finally {
                this.assigning = false;
            }
        },

        // Confirm delete
        confirmDelete(policy) {
            this.policyToDelete = policy;
            this.showDeleteModal = true;
        },

        // Delete policy
        async deletePolicy() {
            if (!this.policyToDelete) return;

            this.deleting = true;

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch(`/api/Policies/${this.policyToDelete.id}`, {
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
                        throw new Error('Access denied. You do not have permission to delete policies.');
                    }
                    throw new Error(`Failed to delete policy: ${response.statusText}`);
                }

                this.successMessage = 'Policy deleted successfully!';
                this.showDeleteModal = false;
                this.policyToDelete = null;
                await this.loadPolicies();

                // Clear success message after 5 seconds
                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);

            } catch (error) {
                console.error('Error deleting policy:', error);
                this.errorMessage = error.message || 'Failed to delete policy. Please try again.';
                this.showDeleteModal = false;
            } finally {
                this.deleting = false;
            }
        }
    }));
});


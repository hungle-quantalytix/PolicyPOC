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
        showUpdateAssignmentModal: false,
        showUnassignModal: false,
        showDeleteModal: false,
        isEditing: false,
        
        // Form data
        formData: {
            description: '',
            policyData: ''
        },
        assignFormData: {
            resourceName: '',
            resourceColumns: '',
            action: '',
            effect: ''
        },
        updateAssignmentFormData: {
            assignmentId: '',
            resourceName: '',
            resourceColumns: '',
            action: '',
            effect: ''
        },
        formError: '',
        assignFormError: '',
        updateAssignmentFormError: '',
        submitting: false,
        assigning: false,
        updatingAssignment: false,
        unassigning: false,
        deleting: false,
        
        // Selected items
        currentPolicy: null,
        policyToAssign: null,
        updateAssignmentPolicy: null,
        currentAssignment: null,
        assignmentToUnassign: null,
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
                resourceColumns: '',
                action: '',
                effect: ''
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
                        resourceColumns: this.assignFormData.resourceColumns || null,
                        action: this.assignFormData.action || null,
                        effect: this.assignFormData.effect || null
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

        // Open update assignment modal
        openUpdateAssignmentModal(policy, assignment) {
            this.updateAssignmentPolicy = policy;
            this.currentAssignment = assignment;
            this.updateAssignmentFormData = {
                assignmentId: assignment.id,
                resourceName: assignment.resourceName || '',
                resourceColumns: assignment.resourceColumns || '',
                action: assignment.action || '',
                effect: assignment.effect || ''
            };
            this.updateAssignmentFormError = '';
            this.showUpdateAssignmentModal = true;
        },

        // Close update assignment modal
        closeUpdateAssignmentModal() {
            this.showUpdateAssignmentModal = false;
            this.updateAssignmentFormError = '';
            this.updateAssignmentPolicy = null;
            this.currentAssignment = null;
        },

        // Submit update assignment form
        async submitUpdateAssignmentForm() {
            this.updateAssignmentFormError = '';
            this.updatingAssignment = true;

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch(`/api/Policies/assign/${this.updateAssignmentFormData.assignmentId}`, {
                    method: 'PUT',
                    headers: {
                        'Authorization': `Bearer ${auth.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        assignmentId: this.updateAssignmentFormData.assignmentId,
                        resourceName: this.updateAssignmentFormData.resourceName || null,
                        resourceColumns: this.updateAssignmentFormData.resourceColumns || null,
                        action: this.updateAssignmentFormData.action || null,
                        effect: this.updateAssignmentFormData.effect || null
                    })
                });

                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    if (response.status === 403) {
                        throw new Error('Access denied. You do not have permission to update policy assignments.');
                    }
                    
                    const errorData = await response.json().catch(() => null);
                    if (errorData?.errors) {
                        const errorMessages = Object.values(errorData.errors).flat().join(', ');
                        throw new Error(errorMessages);
                    }
                    throw new Error(`Failed to update policy assignment: ${response.statusText}`);
                }

                this.successMessage = `Policy assignment updated successfully!`;
                this.closeUpdateAssignmentModal();
                await this.loadPolicies();

                // Clear success message after 5 seconds
                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);

            } catch (error) {
                console.error('Error updating policy assignment:', error);
                this.updateAssignmentFormError = error.message || 'Failed to update policy assignment. Please try again.';
            } finally {
                this.updatingAssignment = false;
            }
        },

        // Confirm unassign
        confirmUnassign(assignment) {
            this.assignmentToUnassign = assignment;
            this.showUnassignModal = true;
        },

        // Unassign policy from resource
        async unassignPolicy() {
            if (!this.assignmentToUnassign) return;

            this.unassigning = true;

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch(`/api/Policies/assign/${this.assignmentToUnassign.id}`, {
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
                        throw new Error('Access denied. You do not have permission to unassign policies.');
                    }
                    throw new Error(`Failed to unassign policy: ${response.statusText}`);
                }

                this.successMessage = 'Policy unassigned successfully!';
                this.showUnassignModal = false;
                this.assignmentToUnassign = null;
                await this.loadPolicies();

                // Clear success message after 5 seconds
                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);

            } catch (error) {
                console.error('Error unassigning policy:', error);
                this.errorMessage = error.message || 'Failed to unassign policy. Please try again.';
                this.showUnassignModal = false;
            } finally {
                this.unassigning = false;
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


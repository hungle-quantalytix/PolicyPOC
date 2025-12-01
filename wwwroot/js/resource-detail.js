// Resource Detail Page
document.addEventListener('alpine:init', () => {
    Alpine.data('resourceDetailPage', () => ({
        // State
        resource: null,
        policies: [],
        loading: false,
        successMessage: '',
        errorMessage: '',
        
        // Field modal states
        showFieldModal: false,
        isEditingField: false,
        currentField: null,
        fieldFormData: {
            fieldName: '',
            isPublic: false
        },
        fieldFormError: '',
        submittingField: false,
        
        // Delete field
        showDeleteFieldModal: false,
        fieldToDelete: null,
        deletingField: false,
        
        // Policy assignment
        showAssignPolicyModal: false,
        assignPolicyTarget: 'resource', // 'resource' or 'field'
        fieldForPolicy: null,
        assignPolicyFormData: {
            policyId: '',
            action: ''
        },
        assignPolicyFormError: '',
        assigningPolicy: false,
        
        // Unassign policy
        showUnassignPolicyModal: false,
        policyToUnassign: null,
        unassigningPolicy: false,

        // Initialize
        async init() {
            // Ensure user is authenticated
            if (window.PolicyPOC?.auth?.ensureAuthenticated) {
                window.PolicyPOC.auth.ensureAuthenticated();
            }
            
            await Promise.all([
                this.loadResource(),
                this.loadPolicies()
            ]);
        },

        // Get resource ID from URL
        getResourceId() {
            const params = new URLSearchParams(window.location.search);
            return params.get('id');
        },

        // Load resource details
        async loadResource() {
            const resourceId = this.getResourceId();
            if (!resourceId) {
                this.errorMessage = 'No resource ID provided';
                return;
            }

            this.loading = true;
            this.errorMessage = '';
            
            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch(`/api/resources/${resourceId}`, {
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
                    if (response.status === 404) {
                        throw new Error('Resource not found');
                    }
                    throw new Error(`Failed to load resource: ${response.statusText}`);
                }

                this.resource = await response.json();
            } catch (error) {
                console.error('Error loading resource:', error);
                this.errorMessage = error.message || 'Failed to load resource. Please try again.';
            } finally {
                this.loading = false;
            }
        },

        // Load all policies for dropdown
        async loadPolicies() {
            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) return;

                const response = await fetch('/api/Policies', {
                    headers: {
                        'Authorization': `Bearer ${auth.token}`
                    }
                });

                if (response.ok) {
                    this.policies = await response.json();
                }
            } catch (error) {
                console.error('Error loading policies:', error);
            }
        },

        // Get policy description by ID
        getPolicyDescription(policyId) {
            const policy = this.policies.find(p => p.id === policyId);
            return policy?.description || policyId?.substring(0, 8) + '...';
        },

        // ==================== Field CRUD ====================

        openCreateFieldModal() {
            this.isEditingField = false;
            this.currentField = null;
            this.fieldFormData = {
                fieldName: '',
                isPublic: false
            };
            this.fieldFormError = '';
            this.showFieldModal = true;
        },

        openEditFieldModal(field) {
            this.isEditingField = true;
            this.currentField = field;
            this.fieldFormData = {
                fieldName: field.fieldName,
                isPublic: field.isPublic
            };
            this.fieldFormError = '';
            this.showFieldModal = true;
        },

        closeFieldModal() {
            this.showFieldModal = false;
            this.fieldFormError = '';
        },

        async submitFieldForm() {
            this.fieldFormError = '';
            this.submittingField = true;

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const resourceId = this.getResourceId();
                const url = this.isEditingField 
                    ? `/api/resources/${resourceId}/fields/${this.currentField.id}` 
                    : `/api/resources/${resourceId}/fields`;
                const method = this.isEditingField ? 'PUT' : 'POST';

                const response = await fetch(url, {
                    method: method,
                    headers: {
                        'Authorization': `Bearer ${auth.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(this.fieldFormData)
                });

                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    
                    const errorData = await response.json().catch(() => null);
                    if (errorData?.errors) {
                        const errorMessages = Object.values(errorData.errors).flat().join(', ');
                        throw new Error(errorMessages);
                    }
                    throw new Error(`Failed to ${this.isEditingField ? 'update' : 'create'} field: ${response.statusText}`);
                }

                this.successMessage = this.isEditingField ? 
                    'Field updated successfully!' : 
                    'Field created successfully!';

                this.closeFieldModal();
                await this.loadResource();

                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);

            } catch (error) {
                console.error('Error submitting field form:', error);
                this.fieldFormError = error.message || 'An error occurred. Please try again.';
            } finally {
                this.submittingField = false;
            }
        },

        confirmDeleteField(field) {
            this.fieldToDelete = field;
            this.showDeleteFieldModal = true;
        },

        async deleteField() {
            if (!this.fieldToDelete) return;

            this.deletingField = true;

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const resourceId = this.getResourceId();
                const response = await fetch(`/api/resources/${resourceId}/fields/${this.fieldToDelete.id}`, {
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
                    throw new Error(`Failed to delete field: ${response.statusText}`);
                }

                this.successMessage = 'Field deleted successfully!';
                this.showDeleteFieldModal = false;
                this.fieldToDelete = null;
                await this.loadResource();

                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);

            } catch (error) {
                console.error('Error deleting field:', error);
                this.errorMessage = error.message || 'Failed to delete field. Please try again.';
                this.showDeleteFieldModal = false;
            } finally {
                this.deletingField = false;
            }
        },

        // ==================== Policy Assignment ====================

        openAssignResourcePolicyModal() {
            this.assignPolicyTarget = 'resource';
            this.fieldForPolicy = null;
            this.assignPolicyFormData = {
                policyId: '',
                action: ''
            };
            this.assignPolicyFormError = '';
            this.showAssignPolicyModal = true;
        },

        openAssignFieldPolicyModal(field) {
            this.assignPolicyTarget = 'field';
            this.fieldForPolicy = field;
            this.assignPolicyFormData = {
                policyId: '',
                action: ''
            };
            this.assignPolicyFormError = '';
            this.showAssignPolicyModal = true;
        },

        closeAssignPolicyModal() {
            this.showAssignPolicyModal = false;
            this.assignPolicyFormError = '';
        },

        async submitAssignPolicyForm() {
            this.assignPolicyFormError = '';
            this.assigningPolicy = true;

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const resourceId = this.getResourceId();
                let url;
                
                if (this.assignPolicyTarget === 'resource') {
                    url = `/api/resources/${resourceId}/assign-policy`;
                } else {
                    url = `/api/resources/${resourceId}/fields/${this.fieldForPolicy.id}/assign-policy`;
                }

                const response = await fetch(url, {
                    method: 'POST',
                    headers: {
                        'Authorization': `Bearer ${auth.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(this.assignPolicyFormData)
                });

                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    
                    const errorData = await response.json().catch(() => null);
                    if (errorData?.message) {
                        throw new Error(errorData.message);
                    }
                    throw new Error(`Failed to assign policy: ${response.statusText}`);
                }

                const targetName = this.assignPolicyTarget === 'resource' ? 'resource' : `field "${this.fieldForPolicy.fieldName}"`;
                this.successMessage = `Policy assigned to ${targetName} successfully!`;

                this.closeAssignPolicyModal();
                await this.loadResource();

                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);

            } catch (error) {
                console.error('Error assigning policy:', error);
                this.assignPolicyFormError = error.message || 'Failed to assign policy. Please try again.';
            } finally {
                this.assigningPolicy = false;
            }
        },

        confirmUnassignResourcePolicy(policyId, action) {
            this.policyToUnassign = {
                target: 'resource',
                policyId: policyId,
                action: action
            };
            this.showUnassignPolicyModal = true;
        },

        confirmUnassignFieldPolicy(field, policyId, action) {
            this.policyToUnassign = {
                target: 'field',
                fieldId: field.id,
                fieldName: field.fieldName,
                policyId: policyId,
                action: action
            };
            this.showUnassignPolicyModal = true;
        },

        async unassignPolicy() {
            if (!this.policyToUnassign) return;

            this.unassigningPolicy = true;

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const resourceId = this.getResourceId();
                let url;
                
                if (this.policyToUnassign.target === 'resource') {
                    url = `/api/resources/${resourceId}/unassign-policy`;
                } else {
                    url = `/api/resources/${resourceId}/fields/${this.policyToUnassign.fieldId}/unassign-policy`;
                }

                const response = await fetch(url, {
                    method: 'DELETE',
                    headers: {
                        'Authorization': `Bearer ${auth.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        policyId: this.policyToUnassign.policyId,
                        action: this.policyToUnassign.action
                    })
                });

                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    throw new Error(`Failed to unassign policy: ${response.statusText}`);
                }

                this.successMessage = 'Policy removed successfully!';
                this.showUnassignPolicyModal = false;
                this.policyToUnassign = null;
                await this.loadResource();

                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);

            } catch (error) {
                console.error('Error unassigning policy:', error);
                this.errorMessage = error.message || 'Failed to unassign policy. Please try again.';
                this.showUnassignPolicyModal = false;
            } finally {
                this.unassigningPolicy = false;
            }
        }
    }));
});


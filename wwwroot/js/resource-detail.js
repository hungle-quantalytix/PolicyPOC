// Resource Detail Page
document.addEventListener('alpine:init', () => {
    Alpine.data('resourceDetailPage', () => ({
        // State
        resource: null,
        loading: false,
        successMessage: '',
        errorMessage: '',
        
        // Field modal states
        showFieldModal: false,
        isEditingField: false,
        currentField: null,
        fieldFormData: {
            fieldName: '',
            maskFormat: null,
            maskFormatType: 'none' // 'none', 'empty', 'custom'
        },
        fieldFormError: '',
        submittingField: false,
        
        // Delete field
        showDeleteFieldModal: false,
        fieldToDelete: null,
        deletingField: false,

        // Initialize
        async init() {
            // Ensure user is authenticated
            if (window.PolicyPOC?.auth?.ensureAuthenticated) {
                window.PolicyPOC.auth.ensureAuthenticated();
            }
            
            await this.loadResource();
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

        // ==================== Field CRUD ====================

        openCreateFieldModal() {
            this.isEditingField = false;
            this.currentField = null;
            this.fieldFormData = {
                fieldName: '',
                maskFormat: null,
                maskFormatType: 'none'
            };
            this.fieldFormError = '';
            this.showFieldModal = true;
        },

        openEditFieldModal(field) {
            this.isEditingField = true;
            this.currentField = field;
            
            // Determine maskFormatType from maskFormat value
            let maskFormatType = 'none';
            if (field.maskFormat === '') {
                maskFormatType = 'empty';
            } else if (field.maskFormat !== null && field.maskFormat !== undefined) {
                maskFormatType = 'custom';
            }
            
            this.fieldFormData = {
                fieldName: field.fieldName,
                maskFormat: field.maskFormat,
                maskFormatType: maskFormatType
            };
            this.fieldFormError = '';
            this.showFieldModal = true;
        },

        // Update maskFormat based on maskFormatType selection
        updateMaskFormat() {
            switch (this.fieldFormData.maskFormatType) {
                case 'none':
                    this.fieldFormData.maskFormat = null;
                    break;
                case 'empty':
                    this.fieldFormData.maskFormat = '';
                    break;
                case 'custom':
                    // Keep existing value or set a placeholder
                    if (!this.fieldFormData.maskFormat) {
                        this.fieldFormData.maskFormat = '***';
                    }
                    break;
            }
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

                // Prepare data - only send fieldName and maskFormat
                const requestData = {
                    fieldName: this.fieldFormData.fieldName,
                    maskFormat: this.fieldFormData.maskFormat
                };

                const response = await fetch(url, {
                    method: method,
                    headers: {
                        'Authorization': `Bearer ${auth.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(requestData)
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
        }
    }));
});

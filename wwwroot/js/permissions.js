// Permissions Page Alpine.js Component
document.addEventListener('alpine:init', () => {
    Alpine.data('permissionsPage', () => ({
        permissions: [],
        loading: true,
        successMessage: '',
        errorMessage: '',
        
        // Modal state
        showModal: false,
        isEditing: false,
        submitting: false,
        formError: '',
        editingId: null,
        
        // Delete modal state
        showDeleteModal: false,
        permissionToDelete: null,
        deleting: false,
        
        // Form data
        formData: {
            resourceType: '',
            resourceId: '',
            fieldName: '',
            action: '',
            subjectType: '',
            subjectId: '',
            description: ''
        },
        
        // Filter state
        filters: {
            resourceType: '',
            subjectType: '',
            action: ''
        },
        
        // Available options
        availableResources: [],
        subjects: {
            users: [],
            roles: [],
            policies: []
        },
        
        // Current fields based on selected resource
        currentFields: [],
        
        // Get auth token
        getAuthToken() {
            const auth = window.PolicyPOC?.auth?.load();
            return auth?.token;
        },
        
        // Get auth headers
        getAuthHeaders() {
            const token = this.getAuthToken();
            return {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            };
        },
        
        async init() {
            // Ensure user is authenticated
            if (window.PolicyPOC?.auth?.ensureAuthenticated) {
                window.PolicyPOC.auth.ensureAuthenticated();
            }
            
            await this.loadMetadata();
            await this.loadPermissions();
        },
        
        async loadMetadata() {
            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }
                
                const headers = { 'Authorization': `Bearer ${auth.token}` };
                
                // Load resources and subjects in parallel
                const [resourcesRes, subjectsRes] = await Promise.all([
                    fetch('/api/permissions/resources', { headers }),
                    fetch('/api/permissions/subjects', { headers })
                ]);
                
                if (resourcesRes.ok) {
                    this.availableResources = await resourcesRes.json();
                }
                
                if (subjectsRes.ok) {
                    this.subjects = await subjectsRes.json();
                }
            } catch (error) {
                console.error('Failed to load metadata:', error);
            }
        },
        
        async loadPermissions() {
            this.loading = true;
            this.errorMessage = '';
            
            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }
                
                const params = new URLSearchParams();
                
                if (this.filters.resourceType) params.append('resourceType', this.filters.resourceType);
                if (this.filters.subjectType) params.append('subjectType', this.filters.subjectType);
                if (this.filters.action) params.append('action', this.filters.action);
                
                const url = `/api/permissions${params.toString() ? '?' + params.toString() : ''}`;
                
                const response = await fetch(url, {
                    headers: { 'Authorization': `Bearer ${auth.token}` }
                });
                
                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    if (response.status === 403) {
                        throw new Error('Access denied. You do not have permission to view permissions.');
                    }
                    throw new Error('Failed to load permissions');
                }
                
                this.permissions = await response.json();
            } catch (error) {
                console.error('Error loading permissions:', error);
                this.errorMessage = error.message || 'Failed to load permissions. Please try again.';
            } finally {
                this.loading = false;
            }
        },
        
        clearFilters() {
            this.filters = {
                resourceType: '',
                subjectType: '',
                action: ''
            };
            this.loadPermissions();
        },
        
        openCreateModal() {
            this.isEditing = false;
            this.editingId = null;
            this.formData = {
                resourceType: '',
                resourceId: '',
                fieldName: '',
                action: '',
                subjectType: '',
                subjectId: '',
                description: ''
            };
            this.currentFields = [];
            this.formError = '';
            this.showModal = true;
        },
        
        openEditModal(permission) {
            this.isEditing = true;
            this.editingId = permission.id;
            
            // Store the subjectId to set after DOM updates
            const subjectIdToSet = permission.subjectId;
            
            this.formData = {
                resourceType: permission.resourceType,
                resourceId: permission.resourceId || '',
                fieldName: permission.fieldName || '',
                action: permission.action,
                subjectType: permission.subjectType,
                subjectId: '', // Set empty first, then set after DOM renders options
                description: permission.description || ''
            };
            this.onResourceTypeChange();
            this.formError = '';
            this.showModal = true;
            
            // Use $nextTick to set subjectId after the dropdown options are rendered
            this.$nextTick(() => {
                this.formData.subjectId = subjectIdToSet;
            });
        },
        
        closeModal() {
            this.showModal = false;
            this.formError = '';
        },
        
        onResourceTypeChange() {
            const resource = this.availableResources.find(r => r.name === this.formData.resourceType);
            this.currentFields = resource?.fields || [];
            
            // Clear field if it's not in the new list
            if (this.formData.fieldName && !this.currentFields.includes(this.formData.fieldName)) {
                this.formData.fieldName = '';
            }
        },
        
        onSubjectTypeChange() {
            this.formData.subjectId = '';
        },
        
        async submitForm() {
            this.formError = '';
            
            // Validation
            if (!this.formData.resourceType) {
                this.formError = 'Resource Type is required';
                return;
            }
            if (!this.formData.action) {
                this.formError = 'Action is required';
                return;
            }
            if (!this.formData.subjectType) {
                this.formError = 'Subject Type is required';
                return;
            }
            if (!this.formData.subjectId) {
                this.formError = 'Subject is required';
                return;
            }
            
            this.submitting = true;
            
            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }
                
                const url = this.isEditing 
                    ? `/api/permissions/${this.editingId}` 
                    : '/api/permissions';
                const method = this.isEditing ? 'PUT' : 'POST';
                
                const body = {
                    resourceType: this.formData.resourceType,
                    resourceId: this.formData.resourceId || null,
                    fieldName: this.formData.fieldName || null,
                    action: this.formData.action,
                    subjectType: this.formData.subjectType,
                    subjectId: this.formData.subjectId,
                    description: this.formData.description || null
                };
                
                const response = await fetch(url, {
                    method,
                    headers: {
                        'Authorization': `Bearer ${auth.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(body)
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
                    const error = await response.json();
                    throw new Error(error.message || 'Failed to save permission');
                }
                
                this.successMessage = this.isEditing 
                    ? 'Permission updated successfully' 
                    : 'Permission created successfully';
                    
                this.closeModal();
                await this.loadPermissions();
                
                // Clear success message after 5 seconds
                setTimeout(() => { this.successMessage = ''; }, 5000);
            } catch (error) {
                console.error('Error saving permission:', error);
                this.formError = error.message || 'An error occurred. Please try again.';
            } finally {
                this.submitting = false;
            }
        },
        
        confirmDelete(permission) {
            this.permissionToDelete = permission;
            this.showDeleteModal = true;
        },
        
        async deletePermission() {
            if (!this.permissionToDelete) return;
            
            this.deleting = true;
            
            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }
                
                const response = await fetch(`/api/permissions/${this.permissionToDelete.id}`, {
                    method: 'DELETE',
                    headers: { 'Authorization': `Bearer ${auth.token}` }
                });
                
                if (!response.ok) {
                    if (response.status === 401) {
                        window.PolicyPOC.auth.clear();
                        window.location.replace('/login.html');
                        return;
                    }
                    if (response.status === 403) {
                        throw new Error('Access denied. You do not have permission to delete permissions.');
                    }
                    const error = await response.json();
                    throw new Error(error.message || 'Failed to delete permission');
                }
                
                this.successMessage = 'Permission deleted successfully';
                this.showDeleteModal = false;
                this.permissionToDelete = null;
                await this.loadPermissions();
                
                // Clear success message after 5 seconds
                setTimeout(() => { this.successMessage = ''; }, 5000);
            } catch (error) {
                console.error('Error deleting permission:', error);
                this.errorMessage = error.message || 'Failed to delete permission. Please try again.';
                this.showDeleteModal = false;
                setTimeout(() => { this.errorMessage = ''; }, 5000);
            } finally {
                this.deleting = false;
            }
        }
    }));
});

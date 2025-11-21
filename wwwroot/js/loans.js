document.addEventListener('alpine:init', () => {
    Alpine.data('loansPage', () => ({
        loans: [],
        loading: false,
        successMessage: '',
        errorMessage: '',
        showModal: false,
        showDeleteModal: false,
        isEditing: false,
        currentLoanId: null,
        formData: {},
        formError: '',
        submitting: false,
        deleting: false,
        loanToDelete: null,

        async init() {
            // Ensure user is authenticated
            if (window.PolicyPOC?.auth?.ensureAuthenticated) {
                window.PolicyPOC.auth.ensureAuthenticated();
            }

            await this.loadLoans();
        },

        generateRandomId(prefix = '', length = 8) {
            const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';
            let result = prefix;
            for (let i = 0; i < length; i++) {
                result += chars.charAt(Math.floor(Math.random() * chars.length));
            }
            return result;
        },

        resetFormData() {
            this.formData = {
                loanNumber: this.generateRandomId('LN-', 10),
                loanStatus: 'Draft',
                loanStage: '',
                borrowerId: this.generateRandomId('BR-', 8),
                borrowerName: '',
                borrowerEmail: '',
                assignedLoanOfficerId: '',
                assignedLoanOfficerName: '',
                assignedUnderwriterId: '',
                assignedUnderwriterName: '',
                lenderId: this.generateRandomId('LD-', 8),
                lenderName: '',
                department: '',
                branchId: '',
                region: '',
                loanType: '',
                loanPurpose: '',
                totalLoanAmount: null,
                ltv: null,
                interestRate: null,
                propertyState: '',
                propertyType: '',
                riskRating: '',
                requiresComplianceReview: false
            };
        },

        async loadLoans() {
            this.loading = true;
            this.errorMessage = '';

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch('/api/loans', {
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
                        throw new Error('Access denied. You do not have permission to view loans.');
                    }
                    throw new Error(`Failed to load loans: ${response.statusText}`);
                }

                const data = await response.json();
                this.loans = data || [];
            } catch (error) {
                console.error('Error loading loans:', error);
                this.errorMessage = error.message || 'Failed to load loans. Please try again.';
            } finally {
                this.loading = false;
            }
        },

        openCreateModal() {
            this.isEditing = false;
            this.currentLoanId = null;
            this.resetFormData();
            this.formError = '';
            this.showModal = true;
        },

        openEditModal(loan) {
            this.isEditing = true;
            this.currentLoanId = loan.loanId;
            this.formData = {
                loanNumber: loan.loanNumber,
                loanStatus: loan.loanStatus,
                loanStage: loan.loanStage || '',
                borrowerId: loan.borrowerId,
                borrowerName: loan.borrowerName || '',
                borrowerEmail: loan.borrowerEmail || '',
                assignedLoanOfficerId: loan.assignedLoanOfficerId || '',
                assignedLoanOfficerName: loan.assignedLoanOfficerName || '',
                assignedUnderwriterId: loan.assignedUnderwriterId || '',
                assignedUnderwriterName: loan.assignedUnderwriterName || '',
                lenderId: loan.lenderId,
                lenderName: loan.lenderName || '',
                department: loan.department || '',
                branchId: loan.branchId || '',
                region: loan.region || '',
                loanType: loan.loanType || '',
                loanPurpose: loan.loanPurpose || '',
                totalLoanAmount: loan.totalLoanAmount,
                ltv: loan.ltv,
                interestRate: loan.interestRate,
                propertyState: loan.propertyState || '',
                propertyType: loan.propertyType || '',
                riskRating: loan.riskRating || '',
                requiresComplianceReview: loan.requiresComplianceReview || false
            };
            this.formError = '';
            this.showModal = true;
        },

        closeModal() {
            this.showModal = false;
            this.isEditing = false;
            this.currentLoanId = null;
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

                // Convert numeric strings to numbers or null
                const payload = {
                    ...this.formData,
                    totalLoanAmount: this.formData.totalLoanAmount ? parseFloat(this.formData.totalLoanAmount) : null,
                    ltv: this.formData.ltv ? parseFloat(this.formData.ltv) : null,
                    interestRate: this.formData.interestRate ? parseFloat(this.formData.interestRate) : null,
                    // Convert empty strings to null for optional fields
                    loanStage: this.formData.loanStage || null,
                    borrowerName: this.formData.borrowerName || null,
                    borrowerEmail: this.formData.borrowerEmail || null,
                    assignedLoanOfficerId: this.formData.assignedLoanOfficerId || null,
                    assignedLoanOfficerName: this.formData.assignedLoanOfficerName || null,
                    assignedUnderwriterId: this.formData.assignedUnderwriterId || null,
                    assignedUnderwriterName: this.formData.assignedUnderwriterName || null,
                    lenderName: this.formData.lenderName || null,
                    department: this.formData.department || null,
                    branchId: this.formData.branchId || null,
                    region: this.formData.region || null,
                    loanType: this.formData.loanType || null,
                    loanPurpose: this.formData.loanPurpose || null,
                    propertyState: this.formData.propertyState || null,
                    propertyType: this.formData.propertyType || null,
                    riskRating: this.formData.riskRating || null
                };

                const url = this.isEditing ? `/api/loans/${this.currentLoanId}` : '/api/loans';
                const method = this.isEditing ? 'PUT' : 'POST';

                const response = await fetch(url, {
                    method: method,
                    headers: {
                        'Authorization': `Bearer ${auth.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(payload)
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
                    throw new Error(`Failed to ${this.isEditing ? 'update' : 'create'} loan: ${response.statusText}`);
                }

                this.successMessage = this.isEditing ? 
                    'Loan updated successfully!' : 
                    'Loan created successfully!';
                
                this.closeModal();
                await this.loadLoans();

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

        confirmDelete(loan) {
            this.loanToDelete = loan;
            this.showDeleteModal = true;
        },

        async deleteLoan() {
            if (!this.loanToDelete) return;

            this.deleting = true;

            try {
                const auth = window.PolicyPOC?.auth?.load();
                if (!auth?.token) {
                    throw new Error('Not authenticated');
                }

                const response = await fetch(`/api/loans/${this.loanToDelete.loanId}`, {
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
                        throw new Error('Access denied. You do not have permission to delete loans.');
                    }
                    throw new Error(`Failed to delete loan: ${response.statusText}`);
                }

                this.successMessage = 'Loan deleted successfully!';
                this.showDeleteModal = false;
                this.loanToDelete = null;
                await this.loadLoans();

                // Clear success message after 5 seconds
                setTimeout(() => {
                    this.successMessage = '';
                }, 5000);

            } catch (error) {
                console.error('Error deleting loan:', error);
                this.errorMessage = error.message || 'Failed to delete loan. Please try again.';
                this.showDeleteModal = false;
            } finally {
                this.deleting = false;
            }
        }
    }));
});


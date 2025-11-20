document.addEventListener('DOMContentLoaded', () => {
    window.PolicyPOC?.auth?.ensureAuthenticated();
});

document.addEventListener('alpine:init', () => {
    Alpine.data('forecastPage', () => ({
        forecasts: [],
        loading: false,
        error: null,
        init() {
            window.PolicyPOC?.auth?.applyNavState();
            this.loadForecast();
        },
        async loadForecast() {
            this.loading = true;
            this.error = null;
            try {
                const response = await fetch('/WeatherForecast');
                if (!response.ok) {
                    throw new Error(`Request failed (${response.status})`);
                }

                this.forecasts = await response.json();
            } catch (error) {
                this.error = error.message ?? 'Unexpected error';
            } finally {
                this.loading = false;
            }
        },
        formatDate(value) {
            return new Date(value).toLocaleDateString();
        }
    }));
});


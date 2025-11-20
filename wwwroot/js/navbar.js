// Load navbar component on page load
(async () => {
    try {
        const response = await fetch('/navbar.html');
        if (!response.ok) {
            throw new Error('Failed to load navbar');
        }
        const html = await response.text();
        
        // Insert navbar at the beginning of body
        const navPlaceholder = document.getElementById('navbar-placeholder');
        if (navPlaceholder) {
            navPlaceholder.innerHTML = html;
            
            // Apply auth state after navbar is loaded
            if (window.PolicyPOC?.auth?.applyNavState) {
                window.PolicyPOC.auth.applyNavState();
            }
        }
    } catch (error) {
        console.error('Error loading navbar:', error);
    }
})();


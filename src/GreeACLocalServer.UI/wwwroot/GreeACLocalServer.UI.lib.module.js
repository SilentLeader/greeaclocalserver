// Shared configuration for theme settings
const DARK_THEME_STYLES = {
    '--mud-palette-background': `rgba(50,51,61,1)`,
    '--mud-palette-text-primary': `rgba(255,255,255,0.6980392156862745)`,
    '--loader-base-color': `rgb(99, 99, 110)`
};

// Apply dark theme styles to root element
function applyDarkTheme() {
    Object.entries(DARK_THEME_STYLES).forEach(([property, value]) => {
        rootElement.style.setProperty(property, value);
    });
}

// Determine if dark mode should be applied
function isDarkModeEnabled() {
    const isDarkSettings = window.localStorage.getItem('IsDarkTheme');

    if (isDarkSettings === 'True') {
        return true;
    } else if (isDarkSettings !== 'False') {
        const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)');
        return prefersDark?.matches ?? false;
    }

    return false;
}

// Initialize resource counter
function initializeResources() {
    totalResources = localStorage.getItem("modulesNumber");
    if (!totalResources || totalResources <= 1) {
        totalResources = 200;
    }
}

// Apply theme initialization
function applyThemeInitialization() {
    initializeResources();

    if (isDarkModeEnabled()) {
        applyDarkTheme();
    }
}

// Hide loader and clean up
function completeInitialization(blazor) {
    rootElement.style.setProperty('--loading-display', `none`);
    localStorage.setItem("modulesNumber", resourcesLoaded);

    const appLoader = document.getElementById('app-loader');
    if (appLoader) {
        appLoader.remove();
    }
}

// Merged lifecycle hooks - no duplication
export function beforeServerStart(options) {
    applyThemeInitialization();
}

export function beforeWebAssemblyStart(options, extensions) {
    applyThemeInitialization();
}

export function afterServerStarted(blazor) {
    completeInitialization(blazor);
}

export function afterWebAssemblyStarted(blazor) {
    completeInitialization(blazor);
}

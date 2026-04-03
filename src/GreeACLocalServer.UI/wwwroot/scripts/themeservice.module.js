export class ThemeService {
    removeLoadingStyle() {
        document.body.removeAttribute('style');
    }
}

export const themeService = new ThemeService();
export class LocalStorageService {
    getItem(itemName) {
        return window.localStorage.getItem(itemName);
    }

    setItem(itemName, value) {
        window.localStorage.setItem(itemName, value);
    }

    removeItem(itemName) {
        return window.localStorage.removeItem(itemName);
    }
}

export const localStorageService = new LocalStorageService();
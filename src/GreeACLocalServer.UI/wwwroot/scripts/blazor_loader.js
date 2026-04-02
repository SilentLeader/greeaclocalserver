let percentage = 0;

Blazor.start({
    webAssembly: {
        loadBootResource: function (type, name, defaultUri, integrity) {
            resourcesLoaded++;

            let currentPercentage = Math.floor(resourcesLoaded / totalResources * 100);
            if (currentPercentage > 100) {
                currentPercentage = 100;
            }

            if (currentPercentage <= percentage) {
                return null;
            }

            percentage = currentPercentage;

            rootElement.style.setProperty(
                '--blazor-load-percentage-custom', `${percentage}%`);
            rootElement.style.setProperty(
                '--blazor-load-percentage-text-custom', `"${percentage} %"`);

            return null;
        }
    }
});
window.highlightCode = (elementId) => {
    try {
        const element = document.getElementById(elementId);
        if (element) {
            hljs.highlightElement(element);
        }
    } catch (error) {
        console.error("Error during highlighting:", error);
    }
};
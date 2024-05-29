window.preventEnterDefault = function (event) {
    if (event.key === "Enter") {
        event.preventDefault();
    }
};
// ScrollHelper.js
window.scrollToBottom = function (elementId) {
    var element = document.getElementById(elementId);
    if (element) {
        var isAtBottom = element.scrollHeight - element.scrollTop === element.clientHeight;
        if (!isAtBottom) {
            element.scrollTop = element.scrollHeight;
        }
    }
};

window.forceScrollToBottom = function (elementId) {
    var element = document.getElementById(elementId);
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

window.isAtBottom = function (elementId) {
    var element = document.getElementById(elementId);
    if (element) {
        return element.scrollHeight - element.scrollTop === element.clientHeight;
    }
    return false;
};
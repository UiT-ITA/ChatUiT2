// wwwroot/js/ScrollHelper.js
window.autoScrollEnabled = true;

//window.isAtBottom = function (elementId) {
//    var element = document.getElementById(elementId);
//    if (element) {
//        return element.scrollHeight - element.scrollTop === element.clientHeight;
//    }
//    return false;
//};

window.setupScrollListener = function (elementId) {
    window.autoScrollEnabled = true;
    var element = document.getElementById(elementId);
    if (element) {
        element.addEventListener('scroll', function () {
            var atBottom = element.scrollHeight - element.scrollTop === element.clientHeight;
            window.autoScrollEnabled = atBottom;
        });
    }
};

window.updateScroll = function (elementId) {
    var element = document.getElementById(elementId);
    if (element && window.autoScrollEnabled) {
        element.scrollTop = element.scrollHeight;
    }
};

window.forceScroll = function (elementId) {
    var element = document.getElementById(elementId);
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

window.resetAutoscroll = function () {
    window.autoScrollEnabled = true;
};

window.isAutoScrollEnabled = function () {
    return window.autoScrollEnabled;
};
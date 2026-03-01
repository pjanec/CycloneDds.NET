window.ddsMonitor = window.ddsMonitor || {};

window.ddsMonitor.setScrollTop = function (element, value) {
    if (!element) {
        return;
    }

    element.scrollTop = value;
};

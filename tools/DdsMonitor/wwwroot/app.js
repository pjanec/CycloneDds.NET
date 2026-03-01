window.ddsMonitor = window.ddsMonitor || {};

window.ddsMonitor.setScrollTop = function (element, value) {
    if (!element) {
        return;
    }

    element.scrollTop = value;
};

window.ddsMonitor.ensureRowVisible = function (element, rowIndex, rowHeight) {
    if (!element) {
        return;
    }

    var safeRowHeight = rowHeight > 0 ? rowHeight : 1;
    var rowTop = rowIndex * safeRowHeight;
    var rowBottom = rowTop + safeRowHeight;
    var scrollTop = element.scrollTop;
    var viewportBottom = scrollTop + element.clientHeight;

    if (rowTop < scrollTop) {
        element.scrollTop = rowTop;
    } else if (rowBottom > viewportBottom) {
        element.scrollTop = rowBottom - element.clientHeight;
    }
};

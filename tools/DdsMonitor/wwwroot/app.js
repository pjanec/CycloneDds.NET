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

    var header = element.querySelector('.samples-panel__header');
    var headerHeight = header ? header.offsetHeight : 0;
    var rowElement = element.querySelector('.samples-panel__row');
    var measuredRowHeight = rowElement ? rowElement.offsetHeight : 0;
    var safeRowHeight = rowHeight > 0 ? rowHeight : (measuredRowHeight > 0 ? measuredRowHeight : 1);
    var rowTop = rowIndex * safeRowHeight;
    var rowBottom = rowTop + safeRowHeight;
    rowTop += headerHeight;
    rowBottom += headerHeight;
    var scrollTop = element.scrollTop;
    var viewportBottom = scrollTop + element.clientHeight;

    if (rowTop < scrollTop) {
        element.scrollTop = rowTop;
    } else if (rowBottom > viewportBottom) {
        element.scrollTop = rowBottom - element.clientHeight;
    }
};

window.ddsMonitor.getElementSize = function (element) {
    if (!element) {
        return null;
    }

    return {
        width: element.clientWidth,
        height: element.clientHeight
    };
};

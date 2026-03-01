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
    var safeRowHeight = measuredRowHeight > 0 ? measuredRowHeight : (rowHeight > 0 ? rowHeight : 1);
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

window.ddsMonitor.ensureSelectedRowVisible = function (element) {
    if (!element) {
        return;
    }

    var selected = element.querySelector('.samples-panel__row.is-selected');
    if (!selected) {
        return;
    }

    var header = element.querySelector('.samples-panel__header');
    var headerHeight = header ? header.getBoundingClientRect().height : 0;
    var elementRect = element.getBoundingClientRect();
    var selectedRect = selected.getBoundingClientRect();

    var visibleTop = elementRect.top + headerHeight;
    var visibleBottom = elementRect.bottom;

    if (selectedRect.top < visibleTop) {
        element.scrollTop -= (visibleTop - selectedRect.top);
    } else if (selectedRect.bottom > visibleBottom) {
        element.scrollTop += (selectedRect.bottom - visibleBottom);
    }
};

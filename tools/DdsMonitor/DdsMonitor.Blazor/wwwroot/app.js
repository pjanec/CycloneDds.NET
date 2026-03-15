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

window.ddsMonitor.getTheme = function () {
    var stored = null;
    try {
        stored = window.localStorage.getItem("ddsMonitor.theme");
    } catch (err) {
        stored = null;
    }

    var html = document.documentElement;
    var current = html.getAttribute("data-theme");
    if (stored) {
        return stored;
    }

    return current || "dark";
};

window.ddsMonitor.setTheme = function (theme) {
    if (!theme) {
        return "dark";
    }

    var value = theme === "light" ? "light" : "dark";
    var html = document.documentElement;
    html.setAttribute("data-theme", value);

    try {
        window.localStorage.setItem("ddsMonitor.theme", value);
    } catch (err) {
        // Ignore storage failures.
    }

    return value;
};

window.ddsMonitor.toggleTheme = function () {
    var current = window.ddsMonitor.getTheme();
    var next = current === "dark" ? "light" : "dark";
    return window.ddsMonitor.setTheme(next);
};

window.ddsMonitor.applyStoredTheme = function () {
    var theme = window.ddsMonitor.getTheme();
    return window.ddsMonitor.setTheme(theme);
};

window.ddsMonitor.downloadTextFile = function (fileName, content, contentType) {
    var type = contentType || "application/json";
    var blob = new Blob([content || ""], { type: type });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = fileName || "download.json";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(function () {
        URL.revokeObjectURL(url);
    }, 0);
};

window.ddsMonitor.clickElement = function (element) {
    if (!element) {
        return;
    }

    element.click();
};

/** Selects all text inside an input element so the user can immediately type to replace it. */
window.ddsMonitor.selectInput = function (element) {
    if (!element) {
        return;
    }
    element.select();
};

/**
 * Scrolls the element bearing the class 'is-highlighted' inside the given container
 * into view, keeping it visible within the combo dropdown.
 */
window.ddsMonitor.scrollHighlightedComboOption = function (container) {
    if (!container) {
        return;
    }
    var highlighted = container.querySelector('.is-highlighted');
    if (!highlighted) {
        return;
    }
    var containerTop = container.scrollTop;
    var containerBottom = containerTop + container.clientHeight;
    var elemTop = highlighted.offsetTop;
    var elemBottom = elemTop + highlighted.offsetHeight;
    if (elemTop < containerTop) {
        container.scrollTop = elemTop;
    } else if (elemBottom > containerBottom) {
        container.scrollTop = elemBottom - container.clientHeight;
    }
};

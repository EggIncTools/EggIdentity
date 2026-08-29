(function () {
  if (window.__comboBoxPickerInit) return;
  window.__comboBoxPickerInit = true;

  window.comboBoxPickerPosition = function (popoverEl, anchorEl) {
    const rect = anchorEl.getBoundingClientRect();
    popoverEl.style.position = "fixed";
    popoverEl.style.left = rect.left + "px";
    popoverEl.style.top = rect.bottom + "px";
    popoverEl.style.width = rect.width + "px";
  };

  window.comboBoxPickerShow = function (popoverEl) {
    if (!popoverEl.matches(":popover-open")) {
      popoverEl.showPopover();
    }
  };

  window.comboBoxPickerHide = function (popoverEl) {
    if (popoverEl.matches(":popover-open")) {
      popoverEl.hidePopover();
    }
  };
})();

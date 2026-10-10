(function () {
  if (window.__comboBoxPickerInit) return;
  window.__comboBoxPickerInit = true;

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

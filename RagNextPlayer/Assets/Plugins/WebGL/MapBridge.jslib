mergeInto(LibraryManager.library, {
  ShowMapOverlay: function (configJsonPtr) {
    var configJson = UTF8ToString(configJsonPtr);
    if (typeof window.ShowMapOverlay === "function") {
      window.ShowMapOverlay(configJson);
    } else {
      console.warn("window.ShowMapOverlay not found!");
    }
  },
  HideMapOverlay: function () {
    if (typeof window.HideMapOverlay === "function") {
      window.HideMapOverlay();
    } else {
      console.warn("window.HideMapOverlay not found!");
    }
  }
});

// Small browser helpers for the WebGL build (see Game.Gameplay.BrowserBridge).
mergeInto(LibraryManager.library, {
  // Copies text to the clipboard. Returns 1 when a copy was attempted.
  BrowserCopyText: function (ptr) {
    var text = UTF8ToString(ptr);
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).catch(function () { legacyCopy(text); });
        return 1;
      }
    } catch (e) {}
    function legacyCopy(t) {
      var area = document.createElement("textarea");
      area.value = t;
      area.style.position = "fixed";
      area.style.opacity = "0";
      document.body.appendChild(area);
      area.select();
      try { document.execCommand("copy"); } catch (e) {}
      document.body.removeChild(area);
    }
    legacyCopy(text);
    return 1;
  },

  // 1 while the tab is in the background (minimised, other tab in front).
  BrowserPageHidden: function () {
    return document.hidden ? 1 : 0;
  }
});

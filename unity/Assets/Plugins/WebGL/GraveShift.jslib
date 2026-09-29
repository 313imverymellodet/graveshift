mergeInto(LibraryManager.library, {
  SD_Rewarded: function (goPtr) {
    var go = UTF8ToString(goPtr);
    var reply = function (ok) { try { window.unityInstance && window.unityInstance.SendMessage(go, "OnRewarded", ok ? "1" : "0"); } catch (e) {} };
    if (window.SD && window.SD.rewarded) window.SD.rewarded().then(function (ok) { reply(ok); }, function () { reply(false); });
    else reply(false);
  },
  SD_AdsAvailable: function () { return (window.SD && window.SD.adsAvailable && window.SD.adsAvailable()) ? 1 : 0; },
  SD_Gameplay: function (on) { if (window.SD && window.SD.gameplay) window.SD.gameplay(!!on); },
  SD_Event: function (namePtr, value) { if (window.SD && window.SD.track) window.SD.track(UTF8ToString(namePtr), value); },
  SD_Ready: function () { if (window.SD && window.SD.ready) window.SD.ready(); },

  GS_Vibrate: function (ms) { try { if (navigator.vibrate && (!navigator.userActivation || navigator.userActivation.hasBeenActive)) navigator.vibrate(ms); } catch (e) {} },

  GS_ArmShare: function (textPtr) {
    var text = UTF8ToString(textPtr), w = window;
    var url = w.location.origin + w.location.pathname;
    var doShare = function () {
      if (!w.__gsPending) return;
      var t = w.__gsPending; w.__gsPending = null;
      if (navigator.share) navigator.share({ title: "GRAVE SHIFT", text: t, url: url }).catch(function () {});
      else if (navigator.clipboard) navigator.clipboard.writeText(t + "\n" + url).then(function () { w.gsToast && w.gsToast("Copied! Paste it anywhere"); });
      if (w.SD && w.SD.track) w.SD.track("share", 0);
    };
    if (!w.__gsHooked) {
      w.__gsHooked = true;
      ["pointerup", "touchend", "click"].forEach(function (ev) { w.addEventListener(ev, doShare, true); });
    }
    w.__gsPending = text;
    setTimeout(doShare, 450);
  }
});

'use strict';

angular.module('learnSphereApp')
.service('AuthService', ['$http', '$q', '$location', '$timeout', 'API_URL', 'PendingMatchService', function ($http, $q, $location, $timeout, API_URL, PendingMatchService) {
  var self = this;
  var TOKEN_KEY = 'ls_token';
  var USER_KEY  = 'ls_user';
  var SESSION_ID_KEY = 'ls_session_id';
  var SIGNAL_PREFIX  = 'ls_active_session:';

  // Set once by the cross-tab takeover handler below; AuthCtrl reads and
  // clears it on load so the login page can explain why it appeared.
  self.kickedOutMessage = '';

  function signalKey(userId) { return SIGNAL_PREFIX + userId; }

  function generateSessionId() {
    return Date.now().toString(36) + '-' + Math.random().toString(36).slice(2);
  }

  // An account should only be signed in on one tab per browser at a time.
  // Each login stamps a fresh id into localStorage (the only storage that
  // broadcasts across tabs) under a key namespaced to that user — every
  // other tab still holding that same account's session is listening below
  // and logs itself out the moment a newer id lands. The token itself stays
  // in sessionStorage so closing a tab still ends that tab's session as
  // before; this is purely a "did someone else just sign into my account"
  // signal layered on top.
  function startSession(userId) {
    var sessionId = generateSessionId();
    sessionStorage.setItem(SESSION_ID_KEY, sessionId);
    try { localStorage.setItem(signalKey(userId), sessionId); } catch (e) { /* storage unavailable (e.g. private mode) — single-session just won't be enforced */ }
  }

  self.login = function (email, password) {
    return $http.post(API_URL + '/auth/login', { email: email, password: password })
      .then(function (res) {
        sessionStorage.setItem(TOKEN_KEY, res.data.token);
        sessionStorage.setItem(USER_KEY, JSON.stringify(res.data));
        startSession(res.data.userId);
        return res.data;
      });
  };

  self.register = function (email, password, name, role) {
    return $http.post(API_URL + '/auth/register', { email: email, password: password, name: name, role: role })
      .then(function (res) {
        sessionStorage.setItem(TOKEN_KEY, res.data.token);
        sessionStorage.setItem(USER_KEY, JSON.stringify(res.data));
        startSession(res.data.userId);
        return res.data;
      });
  };

  self.forgotPassword = function (email) {
    return $http.post(API_URL + '/auth/forgot-password', { email: email })
      .then(function (res) { return res.data; });
  };

  self.changePassword = function (currentPassword, newPassword) {
    return $http.post(API_URL + '/auth/change-password',
      { currentPassword: currentPassword, newPassword: newPassword },
      { headers: self.authHeader() }
    ).then(function (res) {
      var user = self.getCurrentUser();
      if (user) {
        user.mustChangePassword = false;
        sessionStorage.setItem(USER_KEY, JSON.stringify(user));
      }
      return res.data;
    });
  };

  self.logout = function () {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    sessionStorage.removeItem(SESSION_ID_KEY);
    // Single point of truth for "logout clears any pinned/pending tutor hand-off" —
    // covers every real logout path (AppCtrl's Sign Out button, ParentCtrl's
    // account-closure flow, change-password's "Sign out instead" link, and
    // WelcomeCtrl's auto-logout-if-already-signed-in) without needing each of
    // them to remember to call PendingMatchService.clear() themselves.
    PendingMatchService.clear();
  };

  self.getToken = function () {
    return sessionStorage.getItem(TOKEN_KEY);
  };

  self.getCurrentUser = function () {
    var raw = sessionStorage.getItem(USER_KEY);
    return raw ? JSON.parse(raw) : null;
  };

  self.isLoggedIn = function () {
    return !!self.getToken();
  };

  self.authHeader = function () {
    return { Authorization: 'Bearer ' + self.getToken() };
  };

  // Cross-tab takeover — the 'storage' event fires in every OTHER tab of this
  // browser whenever localStorage changes (never in the tab that made the
  // change), so this only ever runs in the tab being superseded. If the
  // changed key is our own account's signal and the new value isn't the id we
  // stamped at login, another tab just signed into this same account.
  window.addEventListener('storage', function (e) {
    var user = self.getCurrentUser();
    if (!user || !e.key || e.key !== signalKey(user.userId)) return;
    var mySessionId = sessionStorage.getItem(SESSION_ID_KEY);
    if (!mySessionId || e.newValue === mySessionId) return;
    $timeout(function () {
      self.logout();
      self.kickedOutMessage = 'You were logged out because this account was signed in on another tab.';
      $location.path('/login');
    });
  });
}]);

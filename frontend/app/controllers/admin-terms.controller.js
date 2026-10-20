'use strict';

// Admin → Terms & Conditions: see the current version, publish a new one, browse history.
angular.module('learnSphereApp')
.controller('AdminTermsCtrl', ['$filter', 'AuthService', 'TermsService', function ($filter, AuthService, TermsService) {
  var self = this;
  self.user = AuthService.getCurrentUser();

  self.history = [];
  self.form = { version: '', content: '' };
  self.viewing = null;      // history row expanded for reading
  self.busy = false;
  self.errorMsg = '';
  self.successMsg = '';

  function load() {
    return TermsService.getHistory().then(function (rows) {
      self.history = rows;
      // Start the editor from the current text so a change is an edit, not a rewrite.
      if (rows.length && !self.form.content) self.form.content = rows[0].content;
      if (!self.form.version) self.form.version = $filter('date')(new Date(), 'yyyy-MM-dd');
    }).catch(function () {
      self.errorMsg = 'Could not load the Terms history.';
    });
  }
  load();

  self.current = function () { return self.history[0] || null; };

  self.publish = function () {
    self.errorMsg = '';
    self.successMsg = '';
    if (!confirm('Publish this as a new version? Every user will be asked to accept it the next time they sign in.')) return;
    self.busy = true;
    TermsService.publish(self.form.version, self.form.content).then(function () {
      self.successMsg = 'Published. Users will be asked to accept it at their next sign-in.';
      self.form.version = '';
      return load();
    }).catch(function (err) {
      self.errorMsg = (err.data && err.data.message) || 'Could not publish. Please try again.';
    }).finally(function () {
      self.busy = false;
    });
  };

  self.toggleView = function (row) { self.viewing = self.viewing === row ? null : row; };
}]);

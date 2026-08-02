'use strict';

angular.module('learnSphereApp')
.controller('AdminCtrl', ['$location', '$timeout', 'AuthService', 'AdminService', 'TutorService',
function ($location, $timeout, AuthService, AdminService, TutorService) {
  var self = this;
  self.user = AuthService.getCurrentUser();

  self.stats = null;
  self.unverifiedTutors = [];
  self.disputes = [];
  self.systemLogs = [];

  // Scoring config — weightages are persisted server-side (ScoringWeightages
  // table, see AdminController/TutorsController.GetMatchScores) and drive the
  // actual AI Speed Match ranking on the parent side, not just this display.
  // Pre-seeded with the same 6 rows the backend seeds (rather than starting
  // empty and waiting on the GET below) because the template writes straight to
  // fixed indexes (vm.weightages[0].percent, etc.) — unlike reads, AngularJS's
  // ng-model assignment does NOT fail silently on an undefined array slot, so
  // typing into a field before the GET resolved would throw and the edit would
  // never actually land in this array, making Save a silent no-op.
  self.activeScoringTab = 'threshold';
  self.weightages = [
    { key: 'rating', label: 'Tutor Rating', percent: 0, sortOrder: 0 },
    { key: 'activeness', label: 'Tutor Activeness (Refresh Monthly)', percent: 0, sortOrder: 1 },
    { key: 'disputes', label: 'Tutor Dispute (Refresh Monthly)', percent: 0, sortOrder: 2 },
    { key: 'experience', label: 'Tutor Experience', percent: 0, sortOrder: 3 },
    { key: 'na1', label: 'NA', percent: 0, sortOrder: 4 },
    { key: 'na2', label: 'NA', percent: 0, sortOrder: 5 }
  ];
  self.ratingScale = [
    { range: '90% - 100%', points: 10 },
    { range: '80% - 90%', points: 9 },
    { range: '70% - 80%', points: 8 },
    { range: '60% - 70%', points: 7 },
    { range: '50% - 60%', points: 6 },
    { range: '40% - 50%', points: 5 },
    { range: '30% - 40%', points: 4 },
    { range: '20% - 30%', points: 3 },
    { range: '10% - 20%', points: 2 },
    { range: '0% - 10%', points: 1 }
  ];
  self.activenessScale = [
    { range: '> 15 classes', points: 5 },
    { range: '10 - 15 classes', points: 3 },
    { range: '5 - 10 classes', points: 1 },
    { range: '< 5 classes', points: 0 }
  ];
  self.disputesScale = [
    { range: '>= 2 disputes', points: -10 },
    { range: '1 dispute', points: -5 },
    { range: '0 disputes', points: 2 }
  ];
  self.experienceScale = [
    { range: '> 15 years', points: 5 },
    { range: '> 10 years', points: 4 },
    { range: '> 5 years', points: 3 },
    { range: '> 3 years', points: 2 },
    { range: '> 1 year', points: 1 }
  ];

  self.weightageSaveError = '';

  self.saveWeightages = function () {
    self.weightageSaveError = '';
    AdminService.updateScoringWeightages(self.weightages.map(function (w) {
      return { key: w.key, percent: w.percent };
    })).then(function (res) {
      self.weightages = res.data;
      self.weightageSaveSuccess = true;
      $timeout(function () {
        self.weightageSaveSuccess = false;
      }, 2000);
      // Scores depend on these percentages — refresh so the Tutor Scores tab
      // doesn't show stale numbers if the admin already had it loaded.
      self.loadTutorScores();
    }).catch(function (err) {
      self.weightageSaveError = (err.data && err.data.message) || 'Could not save weightages. Please try again.';
    });
  };

  // Tutor Scores tab — every verified/online tutor's live AI Speed Match score,
  // same computation TutorsController.GetMatchScores gives the parent-facing AI
  // Speed Match panel. Loaded on demand (not on page init) since it's a heavier
  // query than the other scoring-config data.
  self.tutorScores = [];
  self.tutorScoresLoading = false;

  self.loadTutorScores = function () {
    self.tutorScoresLoading = true;
    TutorService.getMatchScores().then(function (res) {
      self.tutorScores = res.data;
      self.tutorScoresLoading = false;
    }).catch(function () { self.tutorScoresLoading = false; });
  };

  self.openTutorScoresTab = function () {
    self.activeScoringTab = 'scores';
    self.loadTutorScores();
  };

  function init() {
    AdminService.getStats().then(function (res) { self.stats = res.data; });
    AdminService.getUnverifiedTutors().then(function (res) { self.unverifiedTutors = res.data; });
    AdminService.getDisputes().then(function (res) { self.disputes = res.data; });
    AdminService.getScoringWeightages().then(function (res) { self.weightages = res.data; });
  }
  init();

  self.verifyTutor = function (tutor) {
    AdminService.verifyTutor(tutor.id).then(function () {
      tutor.isVerified = true;
      self.unverifiedTutors = self.unverifiedTutors.filter(function (t) { return t.id !== tutor.id; });
      self.systemLogs.unshift('Approved tutor: ' + tutor.name + ' (Just now)');
      AdminService.getStats().then(function (res) { self.stats = res.data; });
    });
  };

  self.resolveDispute = function (dispute) {
    AdminService.resolveDispute(dispute.id).then(function () {
      self.disputes = self.disputes.filter(function (d) { return d.id !== dispute.id; });
      self.systemLogs.unshift('Conflict resolved for class: #' + dispute.id + ' (Just now)');
    });
  };
}]);

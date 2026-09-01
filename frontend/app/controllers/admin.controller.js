'use strict';

angular.module('learnSphereApp')
.controller('AdminCtrl', ['$location', '$timeout', '$filter', 'AuthService', 'AdminService', 'TutorService', 'PresetCancellationService', 'ProfanityFilterService',
function ($location, $timeout, $filter, AuthService, AdminService, TutorService, PresetCancellationService, ProfanityFilterService) {
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

  // Reschedule Rejections queue (Admin → Reschedule Rejections) — see
  // AdminController.GetPendingCancellations/ResolvePresetCancellation.
  self.rescheduleQueue = [];
  self.rescheduleActionBusy = false;
  self.rescheduleResolveSuccess = false;

  self.loadRescheduleQueue = function () {
    PresetCancellationService.getAdminQueue().then(function (res) { self.rescheduleQueue = res.data; });
  };

  self.resolveRescheduleRejection = function (d) {
    var noteError = ProfanityFilterService.validate(d._adminNote);
    if (noteError) { alert(noteError); return; }
    self.rescheduleActionBusy = true;
    PresetCancellationService.resolveAdmin(d.id, d._adminNote).then(function () {
      self.rescheduleQueue = self.rescheduleQueue.filter(function (x) { return x.id !== d.id; });
      self.rescheduleActionBusy = false;
      self.rescheduleResolveSuccess = true;
      $timeout(function () { self.rescheduleResolveSuccess = false; }, 2500);
    }).catch(function (err) {
      self.rescheduleActionBusy = false;
      alert((err.data && err.data.message) || 'Could not resolve this. Please try again.');
    });
  };

  function init() {
    AdminService.getStats().then(function (res) { self.stats = res.data; });
    AdminService.getUnverifiedTutors().then(function (res) { self.unverifiedTutors = res.data; });
    AdminService.getDisputes().then(function (res) { self.disputes = res.data; });
    AdminService.getScoringWeightages().then(function (res) { self.weightages = res.data; });
    self.loadRescheduleQueue();
  }
  init();

  // ── Tutor Vetting: per-document review ──────────────────────────────
  // Decisions are staged client-side only — nothing hits the backend per document
  // anymore. "Confirm & Verify" is the single action that applies every staged
  // decision in one batch (see TutorsController.ApplyVerificationDecisions).
  self.rejectionReasons = {};
  self.expandedTutorId = null;

  // Per-document staged decision: { [docId]: { status, selectedReason, freeText, staged, error } }
  self.docReview = {};

  AdminService.getRejectionReasons().then(function (res) {
    self.rejectionReasons = res.data;
  });

  self.toggleVettingExpand = function (tutorId) {
    self.expandedTutorId = self.expandedTutorId === tutorId ? null : tutorId;
    if (self.expandedTutorId === tutorId) {
      self.docReview = {};
    }
  };

  self.getDocReview = function (docId) {
    if (!self.docReview[docId]) {
      self.docReview[docId] = {
        status: null,
        selectedReason: '',
        freeText: '',
        staged: false,
        error: null
      };
    }
    return self.docReview[docId];
  };

  self.getReasonsForType = function (docType) {
    return self.rejectionReasons[docType] || ['Other'];
  };

  // Reuses the same document-type labels as the tutor-side verification section
  // (see filters.js verifDocLabel) instead of maintaining a second copy here.
  self.docTypeLabel = function (docType) {
    return $filter('verifDocLabel')(docType);
  };

  self.formatFileSize = function (bytes) {
    if (!bytes) return '';
    if (bytes >= 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return Math.round(bytes / 1024) + ' KB';
  };

  // A doc that already has a linked replacement (a newer pending row pointing
  // back at it) doesn't need its own decision this round — the replacement is
  // what's actually being reviewed now.
  function isSuperseded(tutor, doc) {
    return (tutor.documents || []).some(function (d) { return d.replacesDocumentId === doc.id; });
  }

  // identity_id has no file at all (just idType/idNumber), so the usual
  // fileUrl/externalUrl check alone would never recognize it as submitted —
  // mirrors TutorsController.HasContent.
  function hasContent(d) {
    return !!(d.fileUrl || d.externalUrl || (d.documentType === 'identity_id' && d.idNumber));
  }

  function docsNeedingDecision(tutor) {
    return (tutor.documents || []).filter(function (d) {
      return d.status === 'pending' && hasContent(d) && !isSuperseded(tutor, d);
    });
  }

  // Validates and stages a single document's decision locally — no backend call.
  self.stageDocReview = function (doc) {
    var review = self.getDocReview(doc.id);
    if (!review.status) { review.error = 'Please select approve or reject.'; return; }
    if (review.status === 'rejected') {
      var reason = review.selectedReason === 'Other' ? review.freeText : review.selectedReason;
      if (!reason) { review.error = 'Please select or enter a rejection reason.'; return; }
      var reasonProfanityError = ProfanityFilterService.validate(reason);
      if (reasonProfanityError) { review.error = reasonProfanityError; return; }
    }
    review.error = null;
    review.staged = true;
  };

  // Ready to confirm once every document actually awaiting a decision this round
  // has been staged — mirrors the backend's own check (Bug #6 fix) so the button
  // is disabled before a doomed request is even sent.
  self.canConfirmTutor = function (tutor) {
    var pending = docsNeedingDecision(tutor);
    if (!pending.length) return false;
    return pending.every(function (d) { return self.getDocReview(d.id).staged; });
  };

  self.confirmAndVerify = function (tutor) {
    if (!self.canConfirmTutor(tutor)) return;
    if (!confirm('Apply all staged decisions and notify ' + tutor.name + '?')) return;

    var decisions = docsNeedingDecision(tutor).map(function (d) {
      var review = self.getDocReview(d.id);
      var note = review.status === 'rejected'
        ? (review.selectedReason === 'Other' ? review.freeText : review.selectedReason)
        : null;
      return { docId: d.id, status: review.status, note: note };
    });

    tutor.confirming = true;
    tutor.confirmError = null;
    AdminService.applyVerificationDecisions(tutor.id, decisions)
      .then(function (res) {
        tutor.confirming = false;
        self.expandedTutorId = null;
        self.docReview = {};
        self.systemLogs.unshift((res.data.verified ? 'Approved tutor: ' : 'Sent verification update to: ')
          + tutor.name + ' (Just now)');
        AdminService.getStats().then(function (r) { self.stats = r.data; });
        // Re-fetch — dual-row archiving/discarding happens server-side, easier to
        // reflect the fresh state than reconcile it locally.
        AdminService.getUnverifiedTutors().then(function (r) { self.unverifiedTutors = r.data; });
      })
      .catch(function (err) {
        tutor.confirmError = err.data && err.data.message
          ? err.data.message : 'Failed to apply decisions. Please try again.';
        tutor.confirming = false;
      });
  };

  self.adminRemoveDoc = function (tutor, doc) {
    if (!confirm('Remove this document? The tutor must re-upload if this field is rejected.'))
      return;

    AdminService.adminRemoveDocument(tutor.id, doc.id)
      .then(function () {
        var idx = tutor.documents.indexOf(doc);
        if (idx > -1) tutor.documents.splice(idx, 1);
        delete self.docReview[doc.id];
        self.systemLogs.unshift('Removed document from tutor: ' + tutor.name + ' (Just now)');
      })
      .catch(function (err) {
        alert((err.data && err.data.message) || 'Failed to remove document.');
      });
  };

  self.resolveDispute = function (dispute) {
    AdminService.resolveDispute(dispute.id).then(function () {
      self.disputes = self.disputes.filter(function (d) { return d.id !== dispute.id; });
      self.systemLogs.unshift('Conflict resolved for class: #' + dispute.id + ' (Just now)');
    });
  };
}]);

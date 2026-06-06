import React, { useState } from 'react';
import { 
  ShieldCheck, Users, Calendar, Wallet, BarChart3, AlertTriangle, 
  CheckCircle, Settings, Award, RefreshCw, Layers, ShieldX
} from 'lucide-react';
import { Booking, Tutor } from '../types';

interface AdminDashboardProps {
  bookings: Booking[];
  tutors: Tutor[];
  onVerifyTutor: (tutorId: string) => void;
  onResolveIssue: (bookingId: string) => void;
}

export const AdminDashboard: React.FC<AdminDashboardProps> = ({
  bookings,
  tutors,
  onVerifyTutor,
  onResolveIssue
}) => {
  const [activeTab, setActiveTab] = useState<'overview' | 'vetting' | 'disputes'>('overview');
  const [systemLogs, setSystemLogs] = useState<string[]>([
    'System audit completed successfully (11:00 AM)',
    'Payout script triggered automatic bank transfers (10:15 AM)',
    'Verification engine vetted 3 new applicant records (09:40 AM)'
  ]);

  const ongoingDisputes = bookings.filter(b => b.issueReported);
  const unverifiedTutors = tutors.slice(1); // Simulate some and make vetting visible

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 font-sans">
      
      {/* Metrics Banner */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        {[
          { label: 'Total Parents', val: '1,245 users', change: '+12% this month', color: 'text-indigo-600' },
          { label: 'Active Vet Tutors', val: '3,876 vetted', change: '99.4% passing filter', color: 'text-blue-600' },
          { label: 'Sessions Dispatched', val: '8,642 completed', change: '0.04% flag rate', color: 'text-emerald-600' },
          { label: 'Gross Net Margins', val: '$245,300.00', change: '+18.5% year-to-year', color: 'text-violet-600' }
        ].map((met, i) => (
          <div key={i} className="bg-white border rounded-2xl p-5 shadow-xs">
            <span className="text-[10px] text-gray-400 font-bold uppercase tracking-wider">{met.label}</span>
            <p className={`text-xl font-display font-black mt-1.5 ${met.color}`}>{met.val}</p>
            <span className="text-[10px] text-emerald-600 font-semibold block mt-1.5 font-sans">✔️ {met.change}</span>
          </div>
        ))}
      </div>

      <div className="lg:grid lg:grid-cols-12 lg:gap-8">
        
        {/* Left Nav menu */}
        <nav className="lg:col-span-3 mb-6 lg:mb-0 bg-white border rounded-2xl p-4 shadow-xs space-y-1">
          {[
            { id: 'overview', name: 'Operations Overview', icon: BarChart3 },
            { id: 'vetting', name: 'Tutor Vetting Base', icon: ShieldCheck },
            { id: 'disputes', name: 'Resolution Disputes', icon: AlertTriangle }
          ].map(tab => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id as any)}
              className={`w-full text-left px-3 py-2.5 rounded-xl text-xs font-semibold flex items-center gap-2.5 transition cursor-pointer ${
                activeTab === tab.id 
                  ? 'bg-slate-900 text-white shadow-xs' 
                  : 'text-gray-600 hover:bg-slate-50'
              }`}
            >
              <tab.icon className="h-4 w-4" />
              {tab.name}
            </button>
          ))}
          <div className="pt-4 border-t border-gray-100 mt-4 text-[11px] text-gray-400 font-medium px-1">
            Access: <span className="text-red-600 font-bold">SECURE ROOT</span>
          </div>
        </nav>

        {/* Dynamic Context Panel */}
        <main className="lg:col-span-9 space-y-6">
          
          {/* 1. Overview */}
          {activeTab === 'overview' && (
            <div className="space-y-6">
              
              {/* Vitals Audit Log */}
              <div className="bg-white border rounded-2xl p-5 shadow-xs">
                <h3 className="font-display font-bold text-gray-900 text-sm mb-4">Operations System Log Tracker</h3>
                <div className="space-y-2 font-mono text-[11px]">
                  {systemLogs.map((log, i) => (
                    <div key={i} className="p-2.5 bg-slate-50 border border-slate-100 rounded-lg flex items-center justify-between text-slate-700">
                      <span>⚡ {log}</span>
                      <span className="text-[10px] text-emerald-600 font-semibold uppercase">ONLINE</span>
                    </div>
                  ))}
                </div>
              </div>

              {/* Summary Lists of issues & vetting */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                
                {/* Pending Actions */}
                <div className="bg-white border rounded-2xl p-5 shadow-xs">
                  <h4 className="font-display font-bold text-gray-900 text-sm mb-3">Vetting Queue</h4>
                  <p className="text-xs text-gray-400 mb-3">Applicants awaiting NIEC certification audits:</p>
                  <div className="space-y-2">
                    {unverifiedTutors.map((tut, i) => (
                      <div key={tut.id} className="p-3 bg-slate-50/50 border rounded-xl flex items-center justify-between">
                        <span className="text-xs font-bold text-gray-800">{tut.name}</span>
                        <button onClick={() => setActiveTab('vetting')} className="text-xs text-blue-600 font-semibold hover:underline">Vet applicant →</button>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Dispute summary */}
                <div className="bg-white border rounded-2xl p-5 shadow-xs">
                  <h4 className="font-display font-bold text-gray-900 text-sm mb-3">Disputes Desk</h4>
                  <p className="text-xs text-gray-400 mb-3">Ongoing claims reported from parent portals:</p>
                  {ongoingDisputes.length === 0 ? (
                    <div className="p-4 bg-emerald-50 text-emerald-800 text-[11px] rounded-xl font-medium">
                      ✔️ No unresolved disputes on queue.
                    </div>
                  ) : (
                    <div className="space-y-2">
                      {ongoingDisputes.map(disp => (
                        <div key={disp.id} className="p-3 bg-red-50/30 border border-red-100 rounded-xl flex items-center justify-between">
                          <span className="text-xs font-bold text-red-900 font-mono">Dispute #{disp.id}</span>
                          <button onClick={() => setActiveTab('disputes')} className="text-xs text-red-600 font-semibold hover:underline">Open resolution →</button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

              </div>

            </div>
          )}

          {/* 2. Tutor Vetting Base */}
          {activeTab === 'vetting' && (
            <div className="bg-white border rounded-2xl p-6 shadow-xs space-y-6">
              <div>
                <span className="text-[10px] bg-indigo-50 text-indigo-700 px-2 py-0.5 rounded font-mono font-bold uppercase">SECURITY VERIFICATION ENGINE</span>
                <h3 className="font-display font-black text-slate-900 text-base mt-2">Applicant Verification Queue</h3>
                <p className="text-xs text-gray-400">Validate diplomas, certificates, and NIE credentials directly on LearnSphere registry log.</p>
              </div>

              <div className="divide-y divide-gray-100">
                {unverifiedTutors.map(tut => (
                  <div key={tut.id} className="py-4 flex flex-col md:flex-row md:items-center justify-between gap-4">
                    <div className="flex items-start gap-3">
                      <img src={tut.imageUrl} alt={tut.name} className="w-10 h-10 rounded-full object-cover border" />
                      <div>
                        <h4 className="font-bold text-gray-900 text-xs">{tut.name}</h4>
                        <p className="text-[10px] text-gray-500">{tut.subjects.join('/')} • Exp: {tut.experienceYears} Years</p>
                        <div className="mt-2.5 flex flex-wrap gap-1">
                          {tut.qualifications.map((q, i) => (
                            <span key={i} className="bg-slate-100 text-slate-600 text-[9px] px-2 py-0.5 rounded font-mono font-medium">{q}</span>
                          ))}
                        </div>
                      </div>
                    </div>

                    <div className="flex gap-2">
                      <button className="bg-slate-50 border hover:bg-slate-100 text-gray-700 font-bold text-[11px] px-3 py-1.5 rounded-lg">Deny Registry</button>
                      <button onClick={() => {
                        onVerifyTutor(tut.id);
                        setSystemLogs(prev => [`Approved tutor: ${tut.name} (Just now)`, ...prev]);
                      }} className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-[11px] px-3 py-1.5 rounded-lg shadow-xs">
                        Validate Credentials & Verify
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* 3. Claims & disputes desk */}
          {activeTab === 'disputes' && (
            <div className="bg-white border rounded-2xl p-6 shadow-xs space-y-6">
              <div>
                <span className="text-[10px] bg-red-100 text-red-800 px-2 py-0.5 rounded font-mono font-bold uppercase">Incident Disputes Auditor</span>
                <h3 className="font-display font-black text-slate-900 text-base mt-2 font-display">Resolution Disputes Log</h3>
                <p className="text-xs text-gray-400 mt-1">Audit conflict reports, chat records, and check attendance receipts before issuing credit resolutions.</p>
              </div>

              {ongoingDisputes.length === 0 ? (
                <div className="p-8 text-center bg-gray-50 rounded-2xl border-2 border-dashed">
                  <CheckCircle className="h-10 w-10 text-emerald-600 mx-auto mb-3" />
                  <p className="text-xs text-gray-500 font-semibold">Terrific! No outstanding parent claims or billing disputes on records.</p>
                </div>
              ) : (
                <div className="space-y-4">
                  {ongoingDisputes.map(booking => (
                    <div key={booking.id} className="p-4 border-2 border-red-50 bg-red-50/10 rounded-2xl space-y-3">
                      <div className="flex justify-between items-center border-b border-dashed border-red-100 pb-2 text-xs">
                        <div>
                          <strong className="text-red-700 uppercase">CLAIM DISPATCHED #{booking.id}</strong>
                          <p className="text-gray-400 mt-0.5">Subject: {booking.subject}</p>
                        </div>
                        <span className="bg-red-100 text-red-800 text-[9px] px-2 py-0.5 rounded font-bold uppercase">PENDING_AUDIT</span>
                      </div>
                      
                      <div className="text-xs leading-relaxed text-slate-700 space-y-2">
                        <p><strong className="text-slate-900">Issue type:</strong> {booking.issueReported?.issueType}</p>
                        <p><strong className="text-slate-900">Incident Details:</strong> "{booking.issueReported?.details}"</p>
                        <p className="text-[10px] text-gray-400 font-mono">Incident Dispatch Stamp: {booking.issueReported?.timestamp}</p>
                      </div>

                      <div className="flex justify-end gap-2 pt-2 border-t border-red-100/50">
                        <button className="bg-white border text-gray-700 hover:bg-slate-50 text-[10.5px] font-bold px-3 py-1.5 rounded-lg font-sans">Contact Parent Sarah</button>
                        <button onClick={() => {
                          onResolveIssue(booking.id);
                          setSystemLogs(prev => [`Conflict resolved for class: ${booking.id} (Just now)`, ...prev]);
                        }} className="bg-red-600 hover:bg-red-700 text-white text-[10.5px] font-bold px-3 py-1.5 rounded-lg shadow-xs transition">
                          Verify Dispute & Resolve Case
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

        </main>
      </div>
    </div>
  );
};

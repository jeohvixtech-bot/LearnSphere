import React, { useState } from 'react';
import { 
  BookOpen, Users, Compass, Calendar, Clipboard, CheckCircle, 
  MessageSquare, Bell, Wallet, BarChart3, Settings, AlertTriangle, 
  Clock, ShieldAlert, Check, ChevronRight, Sparkles, Filter, Mail, Info, RefreshCw
} from 'lucide-react';
import { INITIAL_TUTORS, INITIAL_BOOKINGS, INITIAL_STUDENTS, INITIAL_MESSAGES } from '../data';

interface SpecsCatalogProps {
  onBackToInteractive: (role?: 'parent' | 'tutor' | 'admin', section?: string) => void;
}

export const SpecsCatalog: React.FC<SpecsCatalogProps> = ({ onBackToInteractive }) => {
  const [selectedSpecId, setSelectedSpecId] = useState<number>(6); // Default to Parent Dashboard
  const [activeTab, setActiveTab] = useState<'all' | 'onboarding' | 'parent' | 'tutor' | 'admin' | 'ops'>('all');

  const specs = [
    { id: 1, name: '1. Landing Page', category: 'onboarding', desc: 'Main public portal with matching tagline and navigation.' },
    { id: 2, name: '2. Role Selection', category: 'onboarding', desc: 'Custom role choosing interface to customize experiences.' },
    { id: 3, name: '3. Parent Registration', category: 'onboarding', desc: 'Secure parent signup form with trust badges.' },
    { id: 4, name: '4. Tutor Registration', category: 'onboarding', desc: 'Professional tutor registration for database vetting.' },
    { id: 5, name: '5. Email Verification', category: 'onboarding', desc: 'Security dispatch card to verify account identities.' },
    
    { id: 6, name: '6. Parent Dashboard', category: 'parent', desc: 'Hub for parent services, showing progress metrics and lessons.' },
    { id: 7, name: '7. Add Child Profile', category: 'parent', desc: 'Creation form to individualize kid curriculums & goals.' },
    { id: 8, name: '8. Find Tutors (Search)', category: 'parent', desc: 'Tutor searching listing with precise filter mechanisms.' },
    { id: 9, name: '9. Tutor Profile (Public)', category: 'parent', desc: 'Public page for a tutor, listing reviews, rates, bio.' },
    { id: 10, name: '10. Tutor Availability', category: 'parent', desc: 'Calendar grid visualizing available, booked, pending slots.' },
    
    { id: 11, name: '11. Book / Request Session', category: 'parent', desc: 'Formal scheduling form showing costs and details.' },
    { id: 12, name: '12. Counter Proposal', category: 'parent', desc: 'Negotiation step with parent to accept, decline, or reschedule.' },
    { id: 13, name: '13. My Sessions (Parent)', category: 'parent', desc: 'My Sessions listing tracker filtered by upcoming/completed.' },
    { id: 14, name: '14. Session Details (Pre)', category: 'parent', desc: 'Pre-session preparations checkout sheet.' },
    { id: 15, name: '15. Start Session (QR)', category: 'ops', desc: 'Quick session authorization code and matching mobile scanner.' },
    
    { id: 16, name: '16. Parent Session View', category: 'parent', desc: 'Real-time status tracking widget of in-progress sessions.' },
    { id: 17, name: '17. Report Absent / Issue', category: 'ops', desc: 'Conflict form to report student/tutor absences instantly.' },
    { id: 18, name: '18. Lesson Report (Tutor)', category: 'tutor', desc: 'Tutor submission form outlining performance, homework, and topics.' },
    { id: 19, name: '19. Lesson Report (Parent)', category: 'parent', desc: 'Detailed educational digest shared with the parent.' },
    { id: 20, name: '20. Edit History', category: 'tutor', desc: 'Historical archive of submitted report modifications.' },
    
    { id: 21, name: '21. Payments & Invoices', category: 'parent', desc: 'Billing ledger showing packages, subscriptions, and receipts.' },
    { id: 22, name: '22. Tutor Earnings', category: 'tutor', desc: 'Financial payout details and historical transfers index.' },
    { id: 23, name: '23. Messages (Chat)', category: 'ops', desc: 'Encrypted communication thread connecting parents & tutors.' },
    { id: 24, name: '24. Notifications Drawer', category: 'ops', desc: 'Push alert registry logs.' },
    { id: 25, name: '25. Tutor Dashboard', category: 'tutor', desc: 'Tutor control central (ratings, stats, and agenda details).' },
    { id: 26, name: '26. Tutor Earnings Overview', category: 'tutor', desc: 'Denser analytics of tutor margins and earnings streams.' },
    { id: 27, name: '27. Admin Dashboard', category: 'admin', desc: 'Central system oversight console (users, reviews, audits).' }
  ];

  const filteredSpecs = activeTab === 'all' 
    ? specs 
    : specs.filter(s => s.category === activeTab);

  // Parent State (simulate some interactive values)
  const [parentName, setParentName] = useState('Sarah Tan');
  const [childName, setChildName] = useState('Jessica Tan');
  const [chatMessage, setChatMessage] = useState('');
  const [chatList, setChatList] = useState(INITIAL_MESSAGES);

  // Issue report state
  const [reportedType, setReportedType] = useState('Tutor was absent (No show)');
  const [reportText, setReportText] = useState('');
  const [isReportSubmitted, setIsReportSubmitted] = useState(false);

  // Lesson Report editable
  const [coveredTopic, setCoveredTopic] = useState('Solving Simultaneous Quadratic Equations through substitution and elimination methodologies.');
  const [studentPerformance, setStudentPerformance] = useState('Excellent concentration today. Showed great mastery of formulas.');
  const [assignedHomework, setAssignedHomework] = useState('Worksheet 4 (Q1-Q10) on simultaneous quadratic pairs.');
  const [isReportSaved, setIsReportSaved] = useState(false);

  // Quick Action redirects
  const handleJumpToLive = (role: 'parent' | 'tutor' | 'admin', section?: string) => {
    onBackToInteractive(role, section);
  };

  return (
    <div className="max-w-7xl mx-auto px-4 py-8">
      <div className="mb-8">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-slate-900 text-white p-6 rounded-2xl shadow-md border border-slate-800">
          <div>
            <div className="flex items-center gap-2 mb-2 text-blue-400 font-mono text-xs tracking-wider">
              <Sparkles className="h-4 w-4 text-amber-400" />
              <span>LEARNSPHERE BLUEPRINT ARCHITECTURE</span>
            </div>
            <h2 className="font-display text-2xl font-bold">LearnSphere Spec Catalog</h2>
            <p className="text-slate-400 text-sm mt-1 max-w-2xl">
              Interact with each of the 27 design modules from the product specification blueprint. 
              Toggle any module below to inspect its layout, components, and responsive grid alignment.
            </p>
          </div>
          <button 
            onClick={() => onBackToInteractive('parent')}
            className="self-start md:self-auto text-xs bg-blue-600 hover:bg-blue-700 text-white font-medium px-4 py-2.5 rounded-xl transition duration-200 flex items-center gap-2 shadow-xs cursor-pointer"
          >
            <RefreshCw className="h-4 w-4 animate-reverse-spin" />
            Launch Active Multi-role Sandbox
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
        {/* Left Sidebar Index */}
        <div className="lg:col-span-4 bg-white border border-gray-100 rounded-2xl p-4 shadow-xs h-[640px] flex flex-col">
          <div className="mb-4">
            <h3 className="font-display font-bold text-gray-900 text-sm mb-3">Module Category</h3>
            <div className="flex flex-wrap gap-1">
              {(['all', 'onboarding', 'parent', 'tutor', 'admin', 'ops'] as const).map(tab => (
                <button
                  key={tab}
                  onClick={() => setActiveTab(tab)}
                  className={`px-2.5 py-1 rounded-lg text-[11px] font-medium transition duration-150 capitalize cursor-pointer ${
                    activeTab === tab 
                      ? 'bg-blue-600 text-white shadow-xs' 
                      : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                  }`}
                >
                  {tab}
                </button>
              ))}
            </div>
          </div>

          <div className="overflow-y-auto flex-1 pr-1 border-t border-gray-100 pt-3 space-y-1">
            {filteredSpecs.map(spec => (
              <button
                key={spec.id}
                onClick={() => setSelectedSpecId(spec.id)}
                className={`w-full text-left p-2.5 rounded-xl transition flex items-start gap-3 cursor-pointer ${
                  selectedSpecId === spec.id 
                    ? 'bg-blue-50/75 border-l-4 border-blue-600 text-blue-900 font-medium' 
                    : 'hover:bg-slate-50 text-gray-700'
                }`}
              >
                <div className={`p-1.5 rounded-lg mt-0.5 ${
                  selectedSpecId === spec.id ? 'bg-blue-100 text-blue-600' : 'bg-gray-100 text-gray-500'
                }`}>
                  {spec.category === 'onboarding' && <Users className="h-3.5 w-3.5" />}
                  {spec.category === 'parent' && <Calendar className="h-3.5 w-3.5" />}
                  {spec.category === 'tutor' && <Clipboard className="h-3.5 w-3.5" />}
                  {spec.category === 'admin' && <BarChart3 className="h-3.5 w-3.5" />}
                  {spec.category === 'ops' && <MessageSquare className="h-3.5 w-3.5" />}
                </div>
                <div className="flex-1">
                  <h4 className="text-[12.5px] font-semibold leading-tight">{spec.name}</h4>
                  <p className="text-[10px] text-gray-500 mt-0.5 font-sans line-clamp-1">{spec.desc}</p>
                </div>
                <ChevronRight className="h-3 w-3 self-center text-gray-400 shrink-0" />
              </button>
            ))}
          </div>
        </div>

        {/* Dynamic High-Fidelity Specs Viewer */}
        <div className="lg:col-span-8 bg-zinc-50 border border-gray-200/50 rounded-2xl p-6 min-h-[640px] shadow-inner relative flex flex-col justify-between">
          
          {/* Header Info */}
          <div className="border-b border-gray-200 pb-4 mb-6 flex items-start justify-between gap-4">
            <div>
              <span className="text-[10px] bg-slate-200/80 text-slate-700 font-mono px-2 py-0.5 rounded font-bold uppercase">
                SPEC CARD #{selectedSpecId}
              </span>
              <h3 className="font-display font-extrabold text-xl text-gray-900 mt-1">
                {specs.find(s => s.id === selectedSpecId)?.name}
              </h3>
              <p className="text-xs text-gray-500 mt-1 font-sans">
                {specs.find(s => s.id === selectedSpecId)?.desc}
              </p>
            </div>
            
            <button
              onClick={() => {
                const spec = specs.find(s => s.id === selectedSpecId);
                const role = spec?.category === 'admin' ? 'admin' : spec?.category === 'tutor' ? 'tutor' : 'parent';
                let sect = '';
                if (selectedSpecId === 8) sect = 'search';
                if (selectedSpecId === 21) sect = 'payments';
                if (selectedSpecId === 23) sect = 'chat';
                handleJumpToLive(role, sect);
              }}
              className="px-3 py-1.5 bg-blue-50 hover:bg-blue-100 text-blue-600 border border-blue-200 text-xs font-semibold rounded-lg transition duration-200 cursor-pointer"
            >
              Test Live Inside App →
            </button>
          </div>

          {/* Individual Spec Elements Re-created beautifully */}
          <div className="flex-1 flex items-center justify-center py-4">
            
            {/* 1. Landing Page */}
            {selectedSpecId === 1 && (
              <div className="w-full max-w-xl bg-white rounded-2xl border border-gray-100 shadow-lg overflow-hidden font-sans">
                <div className="bg-blue-600 p-4 text-white flex justify-between items-center">
                  <div className="flex items-center gap-1">
                    <BookOpen className="h-4 w-4" />
                    <span className="font-bold text-xs">LearnSphere</span>
                  </div>
                  <span className="text-[10px] opacity-80">Learn Continuously, Grow Confidently.</span>
                </div>
                <div className="p-8 text-center bg-radial from-blue-50 to-white">
                  <h2 className="font-display text-2xl font-bold tracking-tight text-gray-900">
                    Find the Right Tutor.<br/>Support Every Learning Journey.
                  </h2>
                  <p className="text-gray-600 text-xs mt-2 max-w-sm mx-auto">
                    Quality tutors. Trusted platform. Better learning outcomes.
                  </p>
                  <div className="mt-6 flex flex-col sm:flex-row justify-center gap-2">
                    <button onClick={() => setSelectedSpecId(3)} className="bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold px-4 py-2 rounded-xl">
                      I'm a Parent / Student
                    </button>
                    <button onClick={() => setSelectedSpecId(4)} className="bg-slate-100 hover:bg-gray-200 text-gray-800 text-xs font-semibold px-4 py-2 rounded-xl">
                      I'm a Tutor
                    </button>
                  </div>
                  <div className="mt-8 pt-6 border-t border-gray-100 flex justify-around text-[10px] text-gray-400 font-medium">
                    <span>✔️ Verified Tutors</span>
                    <span>🔒 Safe & Secure</span>
                    <span>⚡ AI-Powered Insights</span>
                  </div>
                </div>
              </div>
            )}

            {/* 2. Role Selection */}
            {selectedSpecId === 2 && (
              <div className="w-full max-w-md bg-white rounded-2xl border border-gray-100 shadow-xl p-8 font-sans text-center">
                <h3 className="font-display font-bold text-xl text-gray-900">Choose Your Role</h3>
                <p className="text-xs text-gray-500 mt-1">This helps us personalize your experience</p>
                <div className="mt-8 grid grid-cols-2 gap-4">
                  <button onClick={() => setSelectedSpecId(3)} className="p-5 border border-blue-200 bg-blue-50/50 hover:bg-blue-50 rounded-2xl flex flex-col items-center group transition">
                    <Users className="h-10 w-10 text-blue-600 group-hover:scale-110 transition duration-200" />
                    <span className="font-bold text-xs text-blue-900 mt-3">Parent / Student</span>
                    <span className="text-[10px] text-gray-500 mt-1 leading-normal">Find tutors, manage sessions & track progress.</span>
                  </button>
                  <button onClick={() => setSelectedSpecId(4)} className="p-5 border border-slate-100 hover:border-blue-100 hover:bg-slate-50 rounded-2xl flex flex-col items-center group transition">
                    <BookOpen className="h-10 w-10 text-gray-500 group-hover:text-blue-600 group-hover:scale-110 transition duration-200" />
                    <span className="font-bold text-xs text-gray-800 mt-3">Tutor</span>
                    <span className="text-[10px] text-gray-500 mt-1 leading-normal">Teach students, manage your schedule & grow.</span>
                  </button>
                </div>
              </div>
            )}

            {/* 3. Parent Registration */}
            {selectedSpecId === 3 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-xl p-6 font-sans">
                <span className="text-[10px] font-bold text-blue-600">Register as Parent</span>
                <h3 className="font-display font-bold text-lg text-gray-900">Create Parent Account</h3>
                <p className="text-[11px] text-gray-400 mt-0.5">Find the best educators near you.</p>
                <form className="mt-4 space-y-3" onSubmit={(e) => { e.preventDefault(); setSelectedSpecId(5); }}>
                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">Full Name</label>
                    <input type="text" placeholder="Sarah Tan" className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" defaultValue="Sarah Tan" />
                  </div>
                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">Email Address</label>
                    <input type="email" placeholder="sarah.tan@example.com" className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" defaultValue="sarah.tan@example.com" />
                  </div>
                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">Password</label>
                    <input type="password" placeholder="••••••••" className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" />
                  </div>
                  <button type="submit" className="w-full bg-blue-600 text-white rounded-lg py-2 mt-4 text-xs font-semibold hover:bg-blue-700 transition">
                    Create Account
                  </button>
                </form>
                <div className="text-center mt-4 text-[10px] text-gray-400">
                  Already have an account? <span className="text-blue-600 cursor-pointer hover:underline" onClick={() => setSelectedSpecId(2)}>Log in</span>
                </div>
              </div>
            )}

            {/* 4. Tutor Registration */}
            {selectedSpecId === 4 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-xl p-6 font-sans">
                <span className="text-[10px] font-bold text-violet-600">Register as Tutor</span>
                <h3 className="font-display font-bold text-lg text-gray-900">Create Tutor Account</h3>
                <p className="text-[11px] text-gray-400 mt-0.5">Teach on LearnSphere and elevate parameters.</p>
                <form className="mt-4 space-y-3" onSubmit={(e) => { e.preventDefault(); setSelectedSpecId(5); }}>
                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">Full Name</label>
                    <input type="text" placeholder="Mr. Lim Wei Sheng" className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" defaultValue="Mr. Lim Wei Sheng" />
                  </div>
                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">Email Address</label>
                    <input type="email" placeholder="lim.ws@example.com" className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" />
                  </div>
                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">Password</label>
                    <input type="password" placeholder="••••••••" className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" />
                  </div>
                  <button type="submit" className="w-full bg-violet-600 text-white rounded-lg py-2 mt-4 text-xs font-semibold hover:bg-violet-700 transition">
                    Create Account
                  </button>
                </form>
                <div className="text-center mt-4 text-[10px] text-gray-400">
                  Already have an account? <span className="text-violet-600 cursor-pointer hover:underline" onClick={() => setSelectedSpecId(2)}>Log in</span>
                </div>
              </div>
            )}

            {/* 5. Email Verification */}
            {selectedSpecId === 5 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-xl p-8 font-sans text-center">
                <div className="w-12 h-12 bg-blue-50 text-blue-600 rounded-full flex items-center justify-center mx-auto mb-4">
                  <Mail className="h-6 w-6" />
                </div>
                <h3 className="font-display font-bold text-lg text-gray-900">Verify Your Email</h3>
                <p className="text-xs text-gray-500 mt-2">
                  We have sent a verification email link to <br/>
                  <strong className="text-gray-800 font-medium">sarah.tan@example.com</strong>
                </p>
                <p className="text-[11px] text-gray-400 mt-3">
                  Please check your inbox or spam directory, and click the link to confirm your account creation.
                </p>
                <button onClick={() => setSelectedSpecId(6)} className="w-full bg-blue-600 text-white rounded-lg py-2 mt-6 text-xs font-semibold hover:bg-blue-700 transition">
                  Proceed to Login / Dashboard
                </button>
                <div className="text-center mt-4 text-[10px] text-gray-400">
                  Didn't receive? <span className="text-blue-600 cursor-pointer hover:underline">Resend email</span>
                </div>
              </div>
            )}

            {/* 6. Parent Dashboard Overview */}
            {selectedSpecId === 6 && (
              <div className="w-full bg-white rounded-2xl border border-gray-100 shadow-lg overflow-hidden font-sans text-xs">
                <div className="bg-slate-50 border-b border-gray-200 p-4 flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <img src="https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&q=80&w=100" alt="Sarah" className="w-8 h-8 rounded-full border" />
                    <div>
                      <h4 className="font-bold text-gray-900">Welcome back, Sarah!</h4>
                      <p className="text-[10px] text-gray-500">Parent Account Dashboard</p>
                    </div>
                  </div>
                  <div className="bg-blue-50 text-blue-600 px-2 py-1 rounded-lg text-[10px] font-semibold">
                    1 Child Profile (Jessica)
                  </div>
                </div>

                <div className="p-4 grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-3">
                    <h5 className="font-bold text-gray-700 uppercase tracking-wide text-[10px]">Upcoming Tutorial Sessions</h5>
                    <div className="p-3 border border-blue-100 bg-blue-50/30 rounded-xl flex justify-between items-center">
                      <div>
                        <span className="text-[9px] font-bold text-blue-600 uppercase">Math - Secondary 3</span>
                        <h4 className="font-bold text-gray-800 mt-0.5">Mr. Lim Wei Sheng</h4>
                        <p className="text-[10px] text-gray-500 flex items-center mt-1"><Clock className="h-3 w-3 mr-1" /> Friday, 4:00 PM</p>
                      </div>
                      <span className="text-[9px] bg-emerald-100 text-emerald-800 px-2 py-0.5 rounded font-bold uppercase">CONFIRMED</span>
                    </div>

                    <h5 className="font-bold text-gray-700 uppercase tracking-wide text-[10px] mt-4">Jessica's Progress</h5>
                    <div className="space-y-2">
                      <div>
                        <div className="flex justify-between text-[10px] text-gray-600 mb-1">
                          <span>Secondary Algebra</span>
                          <span className="font-semibold">85%</span>
                        </div>
                        <div className="w-full bg-gray-100 h-1.5 rounded-full overflow-hidden">
                          <div className="bg-blue-600 h-full rounded-full" style={{ width: '85%' }}></div>
                        </div>
                      </div>
                      <div>
                        <div className="flex justify-between text-[10px] text-gray-600 mb-1">
                          <span>Linear Systems</span>
                          <span className="font-semibold">76%</span>
                        </div>
                        <div className="w-full bg-gray-100 h-1.5 rounded-full overflow-hidden">
                          <div className="bg-emerald-500 h-full rounded-full" style={{ width: '76%' }}></div>
                        </div>
                      </div>
                    </div>
                  </div>

                  <div className="space-y-3">
                    <h5 className="font-bold text-gray-700 uppercase tracking-wide text-[10px]">Quick System Operations</h5>
                    <div className="grid grid-cols-2 gap-2 text-center text-[10px]">
                      <button onClick={() => setSelectedSpecId(8)} className="p-2 border border-gray-100 hover:bg-slate-50 rounded-lg flex flex-col items-center">
                        <Compass className="h-5 w-5 text-gray-500 mb-1" /> 🔍 Find Tutors
                      </button>
                      <button onClick={() => setSelectedSpecId(11)} className="p-2 border border-gray-100 hover:bg-slate-50 rounded-lg flex flex-col items-center">
                        <Calendar className="h-5 w-5 text-blue-600 mb-1" /> 📅 Book Session
                      </button>
                      <button onClick={() => setSelectedSpecId(7)} className="p-2 border border-gray-100 hover:bg-slate-50 rounded-lg flex flex-col items-center">
                        <Users className="h-5 w-5 text-green-600 mb-1" /> ➕ Add Child
                      </button>
                      <button onClick={() => setSelectedSpecId(21)} className="p-2 border border-gray-100 hover:bg-slate-50 rounded-lg flex flex-col items-center">
                        <Wallet className="h-5 w-5 text-violet-600 mb-1" /> 💳 Billing
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* 7. Add Child / Student Profile */}
            {selectedSpecId === 7 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-xl p-6 font-sans">
                <span className="text-[10px] font-bold text-blue-600">Setup Child Account</span>
                <h3 className="font-display font-bold text-lg text-gray-900">Add New Child</h3>
                <p className="text-[11px] text-gray-400 mt-0.5">Receive customized learning and matching filters.</p>
                <form className="mt-4 space-y-3" onSubmit={(e) => { e.preventDefault(); setSelectedSpecId(6); }}>
                  <div className="flex items-center gap-4 mb-4">
                    <div className="w-12 h-12 bg-slate-100 border border-dashed text-gray-400 rounded-full flex flex-col items-center justify-center text-[10px] p-1 font-semibold text-center cursor-pointer">
                      <span>Upload JPG</span>
                    </div>
                    <div className="flex-1">
                      <span className="text-xs text-gray-700 font-semibold block">Photo Option (JPG/PNG &lt; 2MB)</span>
                      <p className="text-[10px] text-gray-400">This helps tutors build user trust.</p>
                    </div>
                  </div>
                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">Child Full Name</label>
                    <input 
                      type="text" 
                      value={childName}
                      onChange={(e) => setChildName(e.target.value)} 
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" 
                    />
                  </div>
                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="text-[10px] text-gray-500 block font-semibold">Birthdate</label>
                      <input type="date" className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" defaultValue="2012-05-14" />
                    </div>
                    <div>
                      <label className="text-[10px] text-gray-500 block font-semibold">Education Level</label>
                      <select className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1">
                        <option>Secondary 1</option>
                        <option>Secondary 2</option>
                        <option selected>Secondary 3</option>
                        <option>Secondary 4</option>
                      </select>
                    </div>
                  </div>
                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">Learning Goals</label>
                    <textarea placeholder="e.g. Needs support preparing for Algebra and Trigonometry tests." className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1 h-16"></textarea>
                  </div>
                  <button type="submit" className="w-full bg-blue-600 text-white rounded-lg py-2 mt-2 text-xs font-semibold hover:bg-blue-700 transition">
                    Save Student Profile
                  </button>
                </form>
              </div>
            )}

            {/* 8. Find Tutors (Search Filter) */}
            {selectedSpecId === 8 && (
              <div className="w-full bg-white rounded-2xl border border-gray-100 shadow-lg p-4 font-sans text-xs">
                <div className="flex gap-2 mb-4 bg-slate-50 p-2 rounded-xl">
                  <input type="text" placeholder="Search subject or tutor name..." className="flex-1 bg-white border border-gray-200 text-xs px-3 py-1.5 rounded-lg" defaultValue="Math" />
                  <button className="bg-blue-600 text-white text-xs font-semibold px-3 py-1.5 rounded-lg flex items-center gap-1">
                    <Filter className="h-3 w-3" /> Filters
                  </button>
                </div>

                <div className="space-y-2">
                  {INITIAL_TUTORS.map(t => (
                    <div key={t.id} className="p-3 border border-gray-100 hover:border-blue-100 rounded-xl flex items-center justify-between gap-3 bg-white shadow-xs">
                      <div className="flex items-center gap-3">
                        <img src={t.imageUrl} alt={t.name} className="w-10 h-10 rounded-full object-cover border" />
                        <div>
                          <div className="flex items-center gap-1.5">
                            <h4 className="font-bold text-gray-950 text-xs">{t.name}</h4>
                            <span className="bg-amber-50 text-amber-600 border border-amber-100 text-[9px] px-1.5 py-0.2 rounded font-semibold flex items-center">⭐ {t.rating}</span>
                          </div>
                          <p className="text-[10px] text-gray-500 font-medium">{t.subjects[0]} • {t.levels[0]}</p>
                          <p className="text-[9px] text-gray-400 mt-0.5">{t.experienceYears} Years Exp • {t.modes.join('/')}</p>
                        </div>
                      </div>
                      <div className="text-right">
                        <span className="font-bold text-blue-600 text-xs">${t.pricePerSession}/hr</span>
                        <button onClick={() => { setSelectedSpecId(9); }} className="block bg-gray-50 hover:bg-blue-600 hover:text-white border text-gray-700 font-semibold text-[10px] px-2.5 py-1 rounded-lg mt-1 transition">
                          View Specs
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* 9. Tutor Profile (Public) */}
            {selectedSpecId === 9 && (
              <div className="w-full bg-white rounded-2xl border border-gray-100 shadow-xl overflow-hidden font-sans text-xs">
                <div className="bg-gradient-to-r from-blue-600 to-indigo-700 p-6 text-white flex items-center gap-4">
                  <img src="https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&q=80&w=150" alt="Mr. Lim" className="w-14 h-14 rounded-full border-2 border-white object-cover" />
                  <div>
                    <h3 className="font-display font-bold text-[15px] flex items-center gap-1.5">Mr. Lim Wei Sheng <span className="bg-amber-400 text-slate-900 text-[9px] px-1.5 py-0.5 rounded font-bold">★ Gold Tutor</span></h3>
                    <p className="text-[10px] mt-0.5 text-blue-100 font-sans">8 years exp • NIE Trained MOE Teacher</p>
                    <div className="flex items-center gap-1 mt-1 text-[10px]">
                      <span>⭐ 4.9 (128 reviews)</span>
                    </div>
                  </div>
                </div>

                <div className="p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-3">
                    <div>
                      <h4 className="font-bold text-gray-800 uppercase tracking-wide text-[9.5px]">Qualifications</h4>
                      <ul className="text-[10.5px] text-gray-600 list-disc list-inside mt-1 space-y-0.5">
                        <li>B.Sc. Mathematics (NUS)</li>
                        <li>PGDE (NIE Secondary math)</li>
                        <li>Ex-Secondary School Educator</li>
                      </ul>
                    </div>
                    <div>
                      <h4 className="font-bold text-gray-800 uppercase tracking-wide text-[9.5px]">Price Rating</h4>
                      <p className="text-sm font-bold text-blue-600 mt-1">$50 - $60 / 2 hours session</p>
                    </div>
                  </div>

                  <div className="space-y-3 bg-slate-50 p-3 rounded-xl border border-gray-100">
                    <h4 className="font-bold text-gray-800 uppercase tracking-wide text-[9.5px]">About Me</h4>
                    <p className="text-[10px] text-gray-600 leading-normal">
                      "Passionate in helping students build strong foundations in Math. My teaching style focuses on deep conceptual understanding rather than simple memorization."
                    </p>
                    <div className="pt-2 flex gap-2">
                      <button onClick={() => setSelectedSpecId(11)} className="flex-1 bg-blue-600 text-white rounded-lg py-1.5 text-[10px] font-bold hover:bg-blue-700 transition">
                        Pre-Book
                      </button>
                      <button onClick={() => setSelectedSpecId(10)} className="bg-gray-100 hover:bg-gray-200 text-gray-800 rounded-lg px-2.5 py-1.5 text-[10px] font-bold transition">
                        Availability
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* 10. Tutor Availability Calendar */}
            {selectedSpecId === 10 && (
              <div className="w-full bg-white rounded-2xl border border-gray-100 shadow-lg p-4 font-sans text-xs">
                <div className="flex justify-between items-center mb-3">
                  <h4 className="font-bold text-gray-800 text-[11px] uppercase tracking-wide">Mr. Lim's Active Agenda</h4>
                  <span className="text-[9px] text-gray-400">June 2026</span>
                </div>
                {/* 7 Days calendar slots visualization */}
                <div className="grid grid-cols-7 gap-1 border-t border-b border-gray-100 py-3 text-center">
                  {['Mon 1', 'Tue 2', 'Wed 3', 'Thu 4', 'Fri 5', 'Sat 6', 'Sun 7'].map((day, i) => (
                    <div key={day} className="p-1 rounded-lg">
                      <p className="font-bold text-[9px] text-gray-400 block">{day.split(' ')[0]}</p>
                      <p className="text-[10px] font-bold text-gray-700 bg-slate-50 rounded py-1.5 mt-1 block">{day.split(' ')[1]}</p>
                      <div className="mt-1.5 space-y-1">
                        {i === 4 ? (
                          <span className="bg-emerald-500 text-white text-[7.5px] px-1 py-0.5 rounded font-bold block" title="4PM Jessica">CONF</span>
                        ) : i === 1 ? (
                          <span className="bg-amber-400 text-white text-[7.5px] px-1 py-0.5 rounded font-bold block" title="6PM Marcus">PEND</span>
                        ) : (
                          <span className="bg-slate-100 text-slate-400 text-[7.5px] px-1 py-0.5 rounded block uppercase font-medium">Free</span>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
                <div className="flex items-center gap-4 text-[9px] text-gray-500 mt-3 justify-center">
                  <span className="flex items-center gap-1"><span className="w-2.5 h-2.5 bg-emerald-500 rounded"></span> Booked Session</span>
                  <span className="flex items-center gap-1"><span className="w-2.5 h-2.5 bg-amber-400 rounded"></span> Pending Session</span>
                  <span className="flex items-center gap-1"><span className="w-2.5 h-2.5 bg-slate-100 rounded"></span> Free Slot</span>
                </div>
              </div>
            )}

            {/* 11. Book / Request Session */}
            {selectedSpecId === 11 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-xl p-5 font-sans">
                <span className="text-[10px] font-bold text-blue-600 uppercase">Interactive Pre-Booking</span>
                <h3 className="font-display font-extrabold text-lg text-gray-900 mt-0.5">Request a Session</h3>
                <p className="text-[11px] text-gray-400 mb-4">Connecting with Mr. Lim Wei Sheng</p>
                
                <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); setSelectedSpecId(12); }}>
                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="text-[10px] text-gray-500 block font-semibold">Select Student</label>
                      <select className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1 font-semibold">
                        <option>Jessica Tan</option>
                      </select>
                    </div>
                    <div>
                      <label className="text-[10px] text-gray-500 block font-semibold">Subject Area</label>
                      <select className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1">
                        <option>Math - Secondary 3</option>
                      </select>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="text-[10px] text-gray-500 block font-semibold">Preferred Date</label>
                      <input type="date" className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" defaultValue="2026-06-11" />
                    </div>
                    <div>
                      <label className="text-[10px] text-gray-500 block font-semibold">Preferred Time</label>
                      <input type="text" className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" defaultValue="05:00 PM" />
                    </div>
                  </div>

                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">Message / Goal notes (Optional)</label>
                    <textarea placeholder="e.g. Need help studying linear systems for final tests next Wednesday." className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-2 text-xs mt-1 h-12"></textarea>
                  </div>

                  <div className="bg-slate-50 p-2.5 rounded-lg text-[10px] border border-gray-100 flex justify-between items-center">
                    <span className="text-gray-500 font-semibold">Total Price Estimation</span>
                    <span className="text-sm font-bold text-blue-600 block">$100 <span className="text-[9px] text-gray-400 font-light">(2 hours @ $50)</span></span>
                  </div>

                  <button type="submit" className="w-full bg-blue-600 text-white rounded-lg py-2 mt-2 text-xs font-semibold hover:bg-blue-700 transition">
                    Send Request to Tutor
                  </button>
                </form>
              </div>
            )}

            {/* 12. Counter Proposal */}
            {selectedSpecId === 12 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-xl p-5 font-sans">
                <span className="text-[9px] bg-amber-50 text-amber-700 font-mono font-bold px-2 py-0.5 rounded border border-amber-200">COUNTER PROPOSAL FROM MR. LIM</span>
                <h3 className="font-display font-extrabold text-base text-gray-900 mt-2">Adjust Appointment Proposal</h3>
                <p className="text-[11px] text-gray-500">Subject: Math - Secondary 3</p>

                <div className="bg-slate-50 rounded-xl p-3 border mt-3 space-y-2">
                  <div className="text-[10px] border-b border-gray-200 pb-2">
                    <p className="text-gray-400 uppercase font-semibold text-[8px]">Original Parent Request</p>
                    <p className="font-bold text-gray-700">Thursday, 11 June 2026 • 05:00 PM</p>
                  </div>
                  <div className="text-[11px]">
                    <p className="text-amber-600 uppercase font-bold text-[8px]">Proposed Alternative Time</p>
                    <p className="font-semibold text-gray-900">Friday, 12 June 2026 • 05:00 PM</p>
                  </div>
                  <div className="bg-white p-2 rounded border text-[10.5px] text-gray-600 italic">
                    "Hi Sarah, I have a prior scheduling conflict on June 11th. Can we do June 12th (Friday) at 5:00 PM instead?"
                  </div>
                </div>

                <div className="mt-4 flex gap-2">
                  <button onClick={() => setSelectedSpecId(6)} className="flex-1 bg-gray-50 border hover:bg-gray-100 text-gray-800 text-xs font-semibold py-2 rounded-lg transition">
                    Decline
                  </button>
                  <button onClick={() => setSelectedSpecId(14)} className="flex-1 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold py-2 rounded-lg transition">
                    Accept Proposal
                  </button>
                </div>
              </div>
            )}

            {/* 13. My Sessions List (Parent) */}
            {selectedSpecId === 13 && (
              <div className="w-full bg-white rounded-2xl border border-gray-100 shadow-lg p-5 font-sans text-xs">
                <div className="flex border-b border-gray-100 pb-2 mb-3">
                  <span className="font-bold border-b-2 border-blue-600 text-blue-600 pb-2 px-2 text-[10.5px]">Upcoming</span>
                  <span className="text-gray-400 pb-2 px-2 text-[10.5px]">Ongoing</span>
                  <span className="text-gray-400 pb-2 px-2 text-[10.5px]">Completed</span>
                  <span className="text-gray-400 pb-2 px-2 text-[10.5px]">Cancelled</span>
                </div>

                <div className="space-y-3">
                  <div className="p-3 bg-blue-50/20 border border-blue-50 hover:border-blue-100 rounded-xl flex items-center justify-between">
                    <div>
                      <span className="text-[9px] bg-blue-50 text-blue-600 font-bold px-1.5 py-0.5 rounded">MATH - SEC 3</span>
                      <h4 className="font-bold text-gray-900 mt-1">Mr. Lim Wei Sheng</h4>
                      <p className="text-[10px] text-gray-500 mt-1 flex items-center"><Clock className="h-3 w-3 mr-1" /> Tomorrow, 04:00 PM</p>
                    </div>
                    <button onClick={() => setSelectedSpecId(14)} className="bg-blue-600 hover:bg-blue-700 text-white text-[10px] px-3 py-1 rounded-lg font-bold transition">
                      Details
                    </button>
                  </div>
                </div>
              </div>
            )}

            {/* 14. Session Details (Pre-Session View) */}
            {selectedSpecId === 14 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-md p-5 font-sans">
                <div className="flex justify-between items-center mb-4">
                  <div>
                    <span className="text-[9px] font-bold text-blue-600 uppercase">Math - Secondary 3</span>
                    <h3 className="font-display font-extrabold text-base text-gray-900 leading-tight">Mr. Lim Wei Sheng</h3>
                  </div>
                  <span className="bg-emerald-50 text-emerald-700 font-bold px-2 py-0.5 rounded text-[10px] uppercase border border-emerald-200">CONFIRMED</span>
                </div>

                <div className="space-y-3 border-t border-b border-gray-100 py-3 text-xs">
                  <div>
                    <p className="text-gray-400 uppercase font-semibold text-[8px]">Date & Time</p>
                    <p className="font-bold text-gray-800"> Friday, 5 June 2026 • 4:00 PM - 6:00 PM</p>
                  </div>
                  <div>
                    <p className="text-gray-400 uppercase font-semibold text-[8px]">Session Mode</p>
                    <p className="font-bold text-gray-800">🏠 Home Visit (Sarah's Residence • 123 Maple Rd)</p>
                  </div>
                  <div className="bg-blue-50 p-3 rounded-lg text-blue-800">
                    <p className="font-semibold text-[9px] uppercase tracking-wider">What to prepare:</p>
                    <ul className="list-disc list-inside mt-1 space-y-0.5 text-[10px]">
                      <li>Quadratic assignment notes</li>
                      <li>Scientific calculator</li>
                    </ul>
                  </div>
                </div>

                <div className="mt-4 flex gap-2">
                  <button onClick={() => setSelectedSpecId(15)} className="flex-1 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold py-2 rounded-lg transition text-center font-display">
                    Start Session (QR Code)
                  </button>
                </div>
              </div>
            )}

            {/* 15. Start Session (QR Code) */}
            {selectedSpecId === 15 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-xl p-5 font-sans text-center">
                <h3 className="font-display font-extrabold text-base text-gray-900">Scan to Start Session</h3>
                <p className="text-xs text-gray-500 mt-1 max-w-[240px] mx-auto leading-relaxed">
                  Ask your student to scan this QR code or provide the authorization matching code below:
                </p>

                <div className="my-6 bg-slate-50 border border-gray-200/50 p-4 rounded-xl inline-block">
                  {/* Visual QR Simulation */}
                  <div className="w-24 h-24 bg-white border-2 border-slate-900 border-dashed rounded-lg flex items-center justify-center mx-auto mb-2 relative">
                    <div className="grid grid-cols-4 gap-1 p-3">
                      {Array.from({ length: 16 }).map((_, i) => (
                        <div key={i} className={`w-3.5 h-3.5 rounded-xs ${i % 3 === 0 ? 'bg-slate-900': 'bg-slate-200'}`}></div>
                      ))}
                    </div>
                  </div>
                  <span className="font-mono font-bold text-slate-800 tracking-wider text-sm block">9852 1734</span>
                </div>

                <div className="text-[10px] text-gray-400">
                  Code expires in <span className="font-bold text-red-500">09:58</span>
                </div>
                <button onClick={() => setSelectedSpecId(16)} className="mt-4 text-xs font-semibold text-blue-600 hover:underline">
                  Simulate QR Scanner Confirmation
                </button>
              </div>
            )}

            {/* 16. Parent Session View (During / After) */}
            {selectedSpecId === 16 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-lg p-5 font-sans">
                <div className="flex items-center justify-between mb-4">
                  <h4 className="font-display font-bold text-gray-900">Session In Progress</h4>
                  <span className="w-2.5 h-2.5 bg-emerald-500 rounded-full animate-ping"></span>
                </div>

                <div className="space-y-4">
                  <div className="p-3 border border-emerald-100 bg-emerald-50/50 rounded-xl flex items-center gap-3">
                    <Check className="text-emerald-600 h-5 w-5 bg-emerald-100 p-1 rounded-full shrink-0" />
                    <div>
                      <h5 className="font-bold text-emerald-900 text-[11px]">Attendance Confirmed</h5>
                      <p className="text-[10px] text-emerald-700">Tutor scanned code and session started at 4:02 PM.</p>
                    </div>
                  </div>

                  <div className="space-y-2">
                    <button onClick={() => setSelectedSpecId(17)} className="w-full bg-red-50 hover:bg-red-100 text-red-600 font-semibold py-2 rounded-lg text-xs transition">
                      ⚠️ Report Tutor Absent or Issue
                    </button>
                    <button onClick={() => setSelectedSpecId(18)} className="w-full bg-blue-50 hover:bg-blue-100 text-blue-600 font-semibold py-2 rounded-lg text-xs transition">
                      ✍️ Submit Lesson Report Draft (Tutor)
                    </button>
                  </div>
                </div>
              </div>
            )}

            {/* 17. Report Tutor Absent / Issue */}
            {selectedSpecId === 17 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-xl p-5 font-sans">
                <h3 className="font-display font-extrabold text-base text-gray-900 flex items-center gap-2 text-red-600">
                  <AlertTriangle className="h-5 w-5" /> Report Session Conflict
                </h3>
                <p className="text-xs text-gray-500 mt-1">Let us know if there are any issues with your appointment.</p>

                {isReportSubmitted ? (
                  <div className="bg-emerald-50 border border-emerald-100 text-emerald-800 p-4 rounded-xl mt-4 text-center">
                    <CheckCircle className="h-8 w-8 text-emerald-600 mx-auto mb-2" />
                    <h4 className="font-bold text-xs">Report Dispatched Sucessfully!</h4>
                    <p className="text-[10px] mt-1">The administration team is auditing this session (#booking-1).</p>
                    <button onClick={() => { setIsReportSubmitted(false); setSelectedSpecId(16); }} className="mt-3 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg px-3 py-1.5 text-[10px] font-semibold transition">
                      Return to Session
                    </button>
                  </div>
                ) : (
                  <form className="mt-4 space-y-3" onSubmit={(e) => { e.preventDefault(); setIsReportSubmitted(true); }}>
                    <div>
                      <label className="text-[10px] text-gray-500 block font-semibold">Incident Type</label>
                      <select 
                        value={reportedType}
                        onChange={(e) => setReportedType(e.target.value)}
                        className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1"
                      >
                        <option>Tutor was absent (No show)</option>
                        <option>Tutor was late ( {'>'} 20 mins)</option>
                        <option>Other technical/scheduling conflict</option>
                      </select>
                    </div>
                    <div>
                      <label className="text-[10px] text-gray-500 block font-semibold">Provide details</label>
                      <textarea 
                        required
                        value={reportText}
                        onChange={(e) => setReportText(e.target.value)}
                        placeholder="Please provide specifics (e.g. waited 30 minutes, tutor did not respond to messages)..." 
                        className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1 h-20"
                      ></textarea>
                    </div>
                    <div className="flex gap-2 pt-2">
                      <button type="button" onClick={() => setSelectedSpecId(16)} className="flex-1 bg-gray-50 border hover:bg-gray-100 text-gray-800 text-xs font-semibold py-2 rounded-lg transition">
                        Cancel
                      </button>
                      <button type="submit" className="flex-1 bg-red-600 hover:bg-red-700 text-white text-xs font-semibold py-2 rounded-lg transition">
                        Dispatch Audit Report
                      </button>
                    </div>
                  </form>
                )}
              </div>
            )}

            {/* 18. Lesson Report (Tutor submit) */}
            {selectedSpecId === 18 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-xl p-5 font-sans">
                <span className="text-[10px] font-bold text-violet-600 uppercase">Tutor Submission Portal</span>
                <h3 className="font-display font-extrabold text-base text-gray-900 mt-1">Submit Lesson Report</h3>
                <p className="text-[11px] text-gray-400">Class: Math - Secondary 3 with Jessica Tan</p>

                <div className="mt-4 space-y-3">
                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">What was covered?</label>
                    <textarea 
                      value={coveredTopic}
                      onChange={(e) => setCoveredTopic(e.target.value)}
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-[11px] mt-1 h-14"
                    />
                  </div>
                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">Student's performance & alignment</label>
                    <textarea 
                      value={studentPerformance}
                      onChange={(e) => setStudentPerformance(e.target.value)}
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-[11px] mt-1 h-14"
                    />
                  </div>
                  <div>
                    <label className="text-[10px] text-gray-500 block font-semibold">Assigned homework</label>
                    <input 
                      type="text"
                      value={assignedHomework}
                      onChange={(e) => setAssignedHomework(e.target.value)}
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-[11px] mt-1"
                    />
                  </div>

                  <div className="flex gap-2 pt-2">
                    <button onClick={() => setSelectedSpecId(25)} className="flex-1 bg-slate-100 hover:bg-slate-200 text-gray-800 text-xs font-semibold py-2 rounded-lg transition">
                      Cancel Draft
                    </button>
                    <button onClick={() => { setSelectedSpecId(19); }} className="flex-1 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold py-2 rounded-lg transition">
                      Publish Report
                    </button>
                  </div>
                </div>
              </div>
            )}

            {/* 19. Lesson Report (Parent View) */}
            {selectedSpecId === 19 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-lg p-5 font-sans">
                <div className="flex justify-between items-center border-b border-gray-100 pb-3 mb-3">
                  <div>
                    <span className="text-[9px] bg-blue-50 text-blue-600 font-semibold px-2 py-0.5 rounded font-bold uppercase">Lesson Report</span>
                    <h3 className="font-display font-bold text-gray-900 mt-1">Math - Secondary 3</h3>
                  </div>
                  <span className="text-[10px] text-gray-400">Date: 28 May 2026</span>
                </div>

                <div className="space-y-3 text-xs">
                  <div>
                    <h5 className="font-bold text-gray-800 uppercase tracking-wide text-[8px] text-gray-400">Tutor In-Charge</h5>
                    <p className="font-semibold text-gray-800 mt-0.5">Mr. Lim Wei Sheng</p>
                  </div>
                  <div>
                    <h5 className="font-bold text-gray-800 uppercase tracking-wide text-[8px] text-gray-400">Syllabus Covered</h5>
                    <p className="text-gray-700 leading-relaxed bg-slate-50 p-2 rounded border border-gray-100 mt-1">
                      {coveredTopic}
                    </p>
                  </div>
                  <div>
                    <h5 className="font-bold text-gray-800 uppercase tracking-wide text-[8px] text-gray-400">Performance Feedback</h5>
                    <p className="text-gray-700 leading-relaxed mt-0.5">
                      {studentPerformance}
                    </p>
                  </div>
                  <div>
                    <h5 className="font-bold text-gray-800 uppercase tracking-wide text-[8px] text-gray-400">Homework Guidelines</h5>
                    <p className="font-semibold text-blue-600 mt-0.5 bg-blue-50/50 p-2 rounded border border-blue-50">
                      📝 {assignedHomework}
                    </p>
                  </div>
                </div>

                <div className="mt-5 pt-3 border-t border-gray-100 flex gap-2">
                  <button onClick={() => setSelectedSpecId(20)} className="flex-1 bg-gray-50 hover:bg-gray-100 text-gray-700 text-xs font-semibold py-2 rounded-lg transition text-center">
                    📖 View Edit History
                  </button>
                  <button onClick={() => setSelectedSpecId(6)} className="flex-1 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold py-2 rounded-lg transition text-center">
                    Dismiss
                  </button>
                </div>
              </div>
            )}

            {/* 20. Edit History (Lesson Report) */}
            {selectedSpecId === 20 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-xl p-5 font-sans">
                <h3 className="font-display font-extrabold text-base text-gray-900">Audit & Edit Logs</h3>
                <p className="text-xs text-gray-500 mt-0.5">History of adjustments for lesson on 28 May 2026</p>

                <div className="mt-4 space-y-4">
                  <div className="relative pl-5 border-l-2 border-blue-500">
                    <div className="absolute left-[-5px] top-1 w-2.5 h-2.5 bg-blue-500 rounded-full"></div>
                    <span className="text-[10px] text-gray-400 font-mono">28 May 2026, 7:15 PM</span>
                    <p className="text-xs text-gray-900 font-semibold mt-0.5">Report Updated by Mr. Lim</p>
                    <p className="text-[11px] text-gray-600 mt-0.5 italic">"Corrected typo in worksheet instructions: Worksheet 4, not Worksheet 3."</p>
                  </div>

                  <div className="relative pl-5 border-l-2 border-gray-200">
                    <div className="absolute left-[-5px] top-1 w-2.5 h-2.5 bg-gray-300 rounded-full"></div>
                    <span className="text-[10px] text-gray-400 font-mono">28 May 2026, 6:15 PM</span>
                    <p className="text-xs text-gray-800 font-semibold mt-0.5">Report Originally Created</p>
                    <p className="text-[11px] text-gray-500 mt-0.5">Initial report submitted following session completion.</p>
                  </div>
                </div>

                <button onClick={() => setSelectedSpecId(19)} className="w-full bg-gray-100 hover:bg-gray-200 text-gray-800 rounded-lg py-2 mt-6 text-xs font-semibold transition">
                  Back to Report Details
                </button>
              </div>
            )}

            {/* 21. Payments & Invoices */}
            {selectedSpecId === 21 && (
              <div className="w-full bg-white rounded-2xl border border-gray-100 shadow-lg p-5 font-sans text-xs">
                <h4 className="font-bold text-gray-800 text-[11px] pb-3 border-b uppercase tracking-wide">Payments & Invoices ledger</h4>
                
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                  <div className="bg-slate-50 p-4 rounded-xl border border-gray-100">
                    <p className="text-gray-400 uppercase font-semibold text-[8px]">Active Subscription Package</p>
                    <h3 className="font-display font-extrabold text-sm text-gray-900 mt-1">Math Specialization (Weekly 2H)</h3>
                    <p className="text-blue-600 font-bold mt-1 text-sm">$200.00 / month <span className="text-[10px] text-gray-400 font-light">($50 / hour)</span></p>
                    <p className="text-gray-500 text-[10px] mt-2">Next automated billing on <strong className="text-gray-700">15 June 2026</strong></p>
                  </div>

                  <div>
                    <h5 className="font-bold text-gray-600 uppercase text-[9px] mb-2 tracking-wide">Invoice Log</h5>
                    <div className="space-y-1.5">
                      <div className="flex justify-between p-2 border border-gray-100 rounded-lg bg-white">
                        <div>
                          <p className="font-bold text-gray-800">15 May 2026</p>
                          <p className="text-[9px] text-gray-400">Inv #INV-0012</p>
                        </div>
                        <div className="text-right">
                          <p className="font-bold text-gray-900">$200.00</p>
                          <span className="text-[8px] bg-emerald-100 text-emerald-800 px-1.5 py-0.2 rounded font-bold">PAID</span>
                        </div>
                      </div>
                      <div className="flex justify-between p-2 border border-gray-100 rounded-lg bg-white">
                        <div>
                          <p className="font-bold text-gray-800">15 Apr 2026</p>
                          <p className="text-[9px] text-gray-400">Inv #INV-0011</p>
                        </div>
                        <div className="text-right">
                          <p className="font-bold text-gray-900">$200.00</p>
                          <span className="text-[8px] bg-emerald-100 text-emerald-800 px-1.5 py-0.2 rounded font-bold">PAID</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* 22. Tutor Earnings */}
            {selectedSpecId === 22 && (
              <div className="w-full bg-white rounded-2xl border border-gray-100 shadow-lg p-5 font-sans text-xs">
                <div className="flex justify-between items-center pb-3 border-b">
                  <h4 className="font-bold text-gray-800 text-[11px] uppercase tracking-wide">Tutor Earnings Ledger</h4>
                  <span className="text-[10px] text-emerald-600 font-semibold">Active Stripe Payouts</span>
                </div>

                <div className="grid grid-cols-3 gap-3 my-4">
                  <div className="bg-slate-50 p-3 rounded-xl text-center border">
                    <span className="text-[9px] text-gray-400 uppercase font-semibold">Gross Earned</span>
                    <p className="text-base font-bold text-gray-900 mt-1">$1,900</p>
                  </div>
                  <div className="bg-slate-50 p-3 rounded-xl text-center border">
                    <span className="text-[9px] text-gray-400 uppercase font-semibold">Pending payout</span>
                    <p className="text-base font-bold text-blue-600 mt-1">$350</p>
                  </div>
                  <div className="bg-emerald-50 text-emerald-900 p-3 rounded-xl text-center border border-emerald-100">
                    <span className="text-[9px] text-emerald-700 uppercase font-semibold">Ready Payout</span>
                    <p className="text-base font-bold text-emerald-800 mt-1">$450</p>
                  </div>
                </div>

                <h5 className="font-bold text-gray-600 uppercase text-[9px] mb-2">Deposit History</h5>
                <div className="space-y-1.5">
                  <div className="flex justify-between p-2 border border-gray-100 rounded-lg">
                    <span>15 May 2026 Payout</span>
                    <strong className="text-emerald-600">+$600.00 (Paid)</strong>
                  </div>
                  <div className="flex justify-between p-2 border border-gray-100 rounded-lg">
                    <span>30 Apr 2026 Payout</span>
                    <strong className="text-emerald-600">+$800.00 (Paid)</strong>
                  </div>
                </div>
              </div>
            )}

            {/* 23. Messages (Chat) */}
            {selectedSpecId === 23 && (
              <div className="w-full bg-white rounded-2xl border border-gray-100 shadow-lg overflow-hidden font-sans text-xs">
                <div className="bg-blue-600 text-white p-3 flex justify-between items-center">
                  <div className="flex items-center gap-2">
                    <div className="w-2.5 h-2.5 bg-emerald-400 rounded-full"></div>
                    <span className="font-bold">Chat Thread: Mr. Lim (Math Secondary 3)</span>
                  </div>
                </div>

                <div className="h-44 overflow-y-auto p-4 space-y-3 bg-slate-50">
                  {chatList.map(msg => (
                    <div key={msg.id} className={`flex flex-col ${msg.sender === 'parent' ? 'items-end' : msg.sender === 'system' ? 'items-center' : 'items-start'}`}>
                      {msg.sender === 'system' ? (
                        <span className="bg-amber-100 text-amber-800 px-2.5 py-0.5 rounded text-[8px] font-bold text-center">{msg.text}</span>
                      ) : (
                        <div className={`p-2.5 rounded-xl max-w-[80%] uppercase-none ${
                          msg.sender === 'parent' ? 'bg-blue-600 text-white rounded-tr-none' : 'bg-white text-gray-800 rounded-tl-none border'
                        }`}>
                          <p className="text-[10.5px] leading-snug">{msg.text}</p>
                          <span className="text-[7.5px] opacity-75 mt-1 block text-right font-mono">{msg.timestamp.split(' ')[1]} {msg.timestamp.split(' ')[2]}</span>
                        </div>
                      )}
                    </div>
                  ))}
                </div>

                <form className="border-t border-gray-200 p-2 flex gap-1 bg-white" onSubmit={(e) => {
                  e.preventDefault();
                  if (!chatMessage.trim()) return;
                  const newMsg = {
                    id: `msg-${Date.now()}`,
                    tutorId: 'tutor-1',
                    sender: 'parent' as const,
                    text: chatMessage,
                    timestamp: '2026-06-04 10:14 PM'
                  };
                  setChatList([...chatList, newMsg]);
                  setChatMessage('');
                  // Auto-respond for amazing feeling
                  setTimeout(() => {
                    setChatList(prev => [...prev, {
                      id: `msg-${Date.now() + 1}`,
                      tutorId: 'tutor-1',
                      sender: 'tutor' as const,
                      text: "Understood! Thank you, Sarah. Let's practice key equations.",
                      timestamp: '2026-06-04 10:15 PM'
                    }]);
                  }, 1500);
                }}>
                  <input 
                    type="text" 
                    value={chatMessage}
                    onChange={(e) => setChatMessage(e.target.value)}
                    placeholder="Type a message..." 
                    className="flex-1 bg-gray-50 border border-gray-200 rounded-lg text-xs px-3 focus:outline-none focus:ring-1 focus:ring-blue-600" 
                  />
                  <button type="submit" className="bg-blue-600 hover:bg-blue-700 text-white rounded-lg px-3 py-1.5 text-xs font-semibold shrink-0 cursor-pointer">
                    Send
                  </button>
                </form>
              </div>
            )}

            {/* 21+. Extra dashboard modules */}
            {selectedSpecId === 24 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-xl p-4 font-sans text-xs">
                <div className="flex justify-between items-center pb-2 border-b">
                  <h4 className="font-bold text-gray-800">Notifications Log</h4>
                  <button className="text-[10px] text-blue-600 font-semibold">Mark all as read</button>
                </div>
                <div className="mt-3 divide-y divide-gray-100">
                  <div className="py-2.5 flex items-start gap-3">
                    <span className="w-2.5 h-2.5 bg-blue-500 rounded-full mt-1 shrink-0"></span>
                    <div>
                      <h5 className="font-bold text-gray-900 leading-none">Booking Request Adjusted</h5>
                      <p className="text-[10px] text-gray-500 mt-1">Mr. Lim has proposed an alternative time slot (June 12th).</p>
                    </div>
                  </div>
                  <div className="py-2.5 flex items-start gap-3">
                    <span className="w-2.5 h-2.5 bg-gray-300 rounded-full mt-1 shrink-0"></span>
                    <div>
                      <h5 className="font-bold text-gray-850 leading-none">Lesson Report Published</h5>
                      <p className="text-[10px] text-gray-500 mt-1">Tutor submitted the report details for child Jessica on 28 May.</p>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {selectedSpecId === 25 && (
              <div className="w-full bg-white rounded-2xl border border-gray-100 shadow-lg overflow-hidden font-sans text-xs">
                <div className="bg-indigo-900 text-white p-4 flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <img src="https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&q=80&w=150" alt="Mr. Lim" className="w-8 h-8 rounded-full object-cover border" />
                    <div>
                      <h4 className="font-bold">Welcome back, Mr. Lim</h4>
                      <p className="text-[10px] text-indigo-200">Registered Singapore Educator</p>
                    </div>
                  </div>
                  <span className="bg-emerald-500 text-white px-2 py-0.5 rounded text-[9px] font-bold">STATUS: ON-DUTY</span>
                </div>

                <div className="p-4 grid grid-cols-1 md:grid-cols-4 gap-2 text-center">
                  <div className="p-2 border rounded-lg">
                    <span className="text-[9px] text-gray-400 font-semibold block uppercase">COMPLETED</span>
                    <strong className="text-sm font-bold text-gray-900">12 classes</strong>
                  </div>
                  <div className="p-2 border rounded-lg">
                    <span className="text-[9px] text-gray-400 font-semibold block uppercase">RATING</span>
                    <strong className="text-sm font-bold text-amber-500">⭐ 4.9</strong>
                  </div>
                  <div className="p-2 border rounded-lg">
                    <span className="text-[9px] text-gray-400 font-semibold block uppercase">COMPLETION</span>
                    <strong className="text-sm font-bold text-emerald-600">98%</strong>
                  </div>
                  <div className="p-2 border rounded-lg">
                    <span className="text-[9px] text-gray-400 font-semibold block uppercase">EARNINGS</span>
                    <strong className="text-sm font-bold text-blue-600">$1,200</strong>
                  </div>
                </div>
              </div>
            )}

            {selectedSpecId === 26 && (
              <div className="w-full max-w-sm bg-white rounded-2xl border border-gray-100 shadow-lg p-5 font-sans">
                <h4 className="font-display font-black text-gray-900 text-base">Tutor Earnings Spectrogram</h4>
                <p className="text-xs text-gray-400 mt-1">Breakdown of gross sales across academic courses</p>
                <div className="mt-4 space-y-3 font-mono text-[10.5px] text-gray-600">
                  <div className="flex justify-between border-b pb-1">
                    <span>Secondary 3 Math Package</span>
                    <span className="font-bold text-slate-900">$800.00</span>
                  </div>
                  <div className="flex justify-between border-b pb-1">
                    <span>Secondary 4 Math Package</span>
                    <span className="font-bold text-slate-900">$300.00</span>
                  </div>
                  <div className="flex justify-between border-b pb-1">
                    <span>O-Level Chemistry Prep</span>
                    <span className="font-bold text-slate-900">$100.00</span>
                  </div>
                </div>
              </div>
            )}

            {selectedSpecId === 27 && (
              <div className="w-full bg-white rounded-2xl border border-gray-100 shadow-lg p-5 font-sans text-xs">
                <div className="flex justify-between items-center pb-3 border-b border-gray-200">
                  <div>
                    <h3 className="font-display font-extrabold text-base text-gray-900 flex items-center gap-2">
                      <ShieldAlert className="h-5 w-5 text-blue-600" /> Admin Dashboard (Internal Team)
                    </h3>
                    <p className="text-[10px] text-gray-500 mt-0.5">LearnSphere Operations Console</p>
                  </div>
                  <span className="text-[10px] bg-blue-50 text-blue-600 font-mono font-bold px-2.5 py-1 rounded">MASTER LOGS</span>
                </div>

                <div className="grid grid-cols-2 md:grid-cols-4 gap-3 my-4">
                  <div className="p-2 bg-slate-50 border rounded-lg text-center">
                    <span className="text-[9px] text-gray-400 block font-semibold uppercase">Total Parents</span>
                    <strong className="text-sm font-bold text-slate-900">1,245</strong>
                  </div>
                  <div className="p-2 bg-slate-50 border rounded-lg text-center">
                    <span className="text-[9px] text-gray-400 block font-semibold uppercase">Active Tutors</span>
                    <strong className="text-sm font-bold text-blue-600">3,876</strong>
                  </div>
                  <div className="p-2 bg-slate-50 border rounded-lg text-center">
                    <span className="text-[9px] text-gray-400 block font-semibold uppercase">Sessions Start</span>
                    <strong className="text-sm font-bold text-emerald-600">8,642</strong>
                  </div>
                  <div className="p-2 bg-slate-50 border rounded-lg text-center">
                    <span className="text-[9px] text-gray-400 block font-semibold uppercase">Gross Revenue</span>
                    <strong className="text-sm font-bold text-violet-600">$245,300</strong>
                  </div>
                </div>

                <h5 className="font-bold text-gray-600 uppercase text-[9px] tracking-wider mb-2">Recent Reports & Disputes</h5>
                <div className="p-2.5 border border-red-100 bg-red-50/50 rounded-lg text-[10px] flex justify-between items-center">
                  <div>
                    <span className="font-bold text-red-700 uppercase">DISPUTE RA-1024</span>
                    <p className="text-gray-600 mt-0.5">Tutor absent claim submitted by Tan J.</p>
                  </div>
                  <span className="bg-amber-150 text-amber-800 border border-amber-200 px-2 py-0.5 rounded font-bold uppercase text-[9px]">PENDING AUDIT</span>
                </div>
              </div>
            )}

          </div>

          {/* Quick Help specs bar */}
          <div className="border-t border-gray-200 pt-4 mt-6 flex flex-col md:flex-row justify-between items-center text-xs text-gray-400 gap-2">
            <span className="flex items-center gap-1"><Info className="h-4.5 w-4.5 text-blue-500" /> Fully responsive design complying with WCAG contrast levels.</span>
            <div className="flex gap-2">
              <span className="rounded bg-sky-50 text-sky-700 px-2 py-0.5 font-mono text-[9.5px]">grid-layout-3x</span>
              <span className="rounded bg-slate-100 text-slate-700 px-2 py-0.5 font-mono text-[9.5px]">tailwind-v4</span>
            </div>
          </div>

        </div>
      </div>
    </div>
  );
};

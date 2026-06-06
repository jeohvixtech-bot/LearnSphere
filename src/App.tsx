import { useState, useEffect } from 'react';
import { MainHeader } from './components/MainHeader';
import { SpecsCatalog } from './components/SpecsCatalog';
import { ParentDashboard } from './components/ParentDashboard';
import { TutorDashboard } from './components/TutorDashboard';
import { AdminDashboard } from './components/AdminDashboard';
import { 
  Tutor, Student, Booking, ChatMessage, NotificationItem, Payout, Invoice 
} from './types';
import { 
  INITIAL_TUTORS, INITIAL_STUDENTS, INITIAL_BOOKINGS, INITIAL_MESSAGES, 
  INITIAL_NOTIFICATIONS, INITIAL_PAYOUTS, INITIAL_INVOICES 
} from './data';
import { Info, Bell, X, Compass, RefreshCw, Sparkles, BookOpen } from 'lucide-react';

export default function App() {
  const [currentMode, setCurrentMode] = useState<'interactive' | 'blueprint'>('interactive');
  const [userRole, setUserRole] = useState<'parent' | 'tutor' | 'admin'>('parent');
  
  // Persistence / States
  const [students, setStudents] = useState<Student[]>(INITIAL_STUDENTS);
  const [tutors, setTutors] = useState<Tutor[]>(INITIAL_TUTORS);
  const [bookings, setBookings] = useState<Booking[]>(INITIAL_BOOKINGS);
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>(INITIAL_MESSAGES);
  const [notifications, setNotifications] = useState<NotificationItem[]>(INITIAL_NOTIFICATIONS);
  const [payouts, setPayouts] = useState<Payout[]>(INITIAL_PAYOUTS);
  const [invoices, setInvoices] = useState<Invoice[]>(INITIAL_INVOICES);

  // Synchronize invoices with confirmed bookings automatically
  useEffect(() => {
    bookings.forEach(booking => {
      if (booking.status === 'confirmed') {
        setInvoices(prev => {
          if (prev.some(inv => inv.bookingId === booking.id)) {
            return prev;
          }
          const newInvoice: Invoice = {
            id: `inv-${booking.id}`,
            date: booking.date,
            amount: booking.totalPrice,
            status: 'Unpaid',
            bookingId: booking.id,
            subject: booking.subject
          };
          return [newInvoice, ...prev];
        });
      }
    });
  }, [bookings]);

  const handlePayInvoice = (id: string) => {
    let targetBookingId: string | undefined;

    setInvoices(prev => prev.map(inv => {
      if (inv.id === id) {
        targetBookingId = inv.bookingId;
        return { ...inv, status: 'Paid' as const };
      }
      return inv;
    }));

    if (targetBookingId) {
      const activeBooking = bookings.find(b => b.id === targetBookingId);
      if (activeBooking && activeBooking.slotId) {
        const tId = activeBooking.tutorId;
        const sId = activeBooking.slotId;
        setTutors(prev => prev.map(t => {
          if (t.id === tId && t.timetable) {
            return {
              ...t,
              timetable: t.timetable.map(slot => {
                if (slot.id === sId) {
                  return { ...slot, status: 'Booked', bookingId: targetBookingId };
                }
                return slot;
              })
            };
          }
          return t;
        }));
      }
    }

    setNotifications(prev => [{
      id: `notif-${Date.now()}`,
      title: 'Payment Successful',
      message: `Invoice #${id} paid successfully. Digital receipt issued!`,
      timestamp: 'Just now',
      type: 'payment',
      read: false
    }, ...prev]);
  };

  // Layout states
  const [isNotifDrawerOpen, setIsNotifDrawerOpen] = useState(false);
  const [landingStep, setLandingStep] = useState<'landing' | 'selectRole' | 'registerParent' | 'registerTutor' | 'verified' | 'dashboard'>('dashboard');
  const [initialParentSection, setInitialParentSection] = useState('dashboard');

  // Load from spec catalog action redirects
  const handleJumpFromSpecs = (role?: 'parent' | 'tutor' | 'admin', section?: string) => {
    setCurrentMode('interactive');
    setLandingStep('dashboard'); // bypass onboarding pages to jump live
    if (role) {
      setUserRole(role);
    }
    if (section) {
      setInitialParentSection(section);
    } else {
      setInitialParentSection('dashboard');
    }
  };

  // State actions
  const handleAddStudent = (std: Student) => {
    setStudents(prev => [...prev, std]);
  };

  const handleAddBooking = (bkg: Booking) => {
    setBookings(prev => [bkg, ...prev]);
    // Push system notifications alert
    const newNotif: NotificationItem = {
      id: `notif-${Date.now()}`,
      title: 'Booking request sent',
      message: `You requested a session on ${bkg.date} with Mr. Lim.`,
      timestamp: 'Just now',
      type: 'booking',
      read: false
    };
    setNotifications(prev => [newNotif, ...prev]);
  };

  const handleUpdateBookingStatus = (id: string, status: Booking['status'], extra?: any) => {
    setBookings(prev => prev.map(b => {
      if (b.id === id) {
        let updated = { ...b, status };
        if (extra) {
          if (status === 'countered') {
            updated.counterProposal = extra;
          } else if (extra.lessonReport) {
            updated.lessonReport = extra.lessonReport;
          }
        }
        return updated;
      }
      return b;
    }));

    // Trigger matching push notifications dynamically
    let title = 'Booking state changed';
    let message = `Booking status modified to ${status}.`;
    if (status === 'confirmed') {
      title = 'Class Appointment Approved';
      message = 'Your session slot was officially certified.';
    } else if (status === 'countered') {
      title = 'Alternative proposal adjusted';
      message = 'Tutor offered coordinates for alternative booking hour.';
    }

    setNotifications(prev => [{
      id: `notif-${Date.now()}`,
      title,
      message,
      timestamp: 'Just now',
      type: 'system',
      read: false
    }, ...prev]);
  };

  const handleReportIssue = (bookingId: string, issueType: string, details: string) => {
    setBookings(prev => prev.map(b => {
      if (b.id === bookingId) {
        return {
          ...b,
          issueReported: {
            issueType,
            details,
            timestamp: new Date().toLocaleTimeString()
          }
        };
      }
      return b;
    }));
  };

  const handlePublishLessonReport = (bookingId: string, covered: string, performance: string, homework: string) => {
    const reportDate = new Date().toLocaleString('en-US', { hour: 'numeric', minute: '2-digit', hour12: true, month: 'short', day: 'numeric', year: 'numeric' });
    setBookings(prev => prev.map(b => {
      if (b.id === bookingId) {
        return {
          ...b,
          lessonReport: {
            id: bookingId,
            covered,
            performance,
            homework,
            submitDate: reportDate,
            editHistory: []
          }
        };
      }
      return b;
    }));

    // Alert parent notification dashboard
    setNotifications(prev => [{
      id: `notif-${Date.now()}`,
      title: 'Edu Progress Report Received',
      message: 'Mr. Lim published lesson evaluation report for your child Jessica.',
      timestamp: 'Just now',
      type: 'system',
      read: false
    }, ...prev]);
  };

  const handleEditLessonReport = (bookingId: string, changesMade: string, revisedFields: { covered: string, performance: string, homework: string }) => {
    const editStamp = new Date().toLocaleString('en-US', { hour: 'numeric', minute: '2-digit', hour12: true, month: 'short', day: 'numeric', year: 'numeric' });
    setBookings(prev => prev.map(b => {
      if (b.id === bookingId && b.lessonReport) {
        const existingHistory = b.lessonReport.editHistory || [];
        return {
          ...b,
          lessonReport: {
            ...b.lessonReport,
            ...revisedFields,
            editHistory: [
              ...existingHistory,
              { date: editStamp, changes: changesMade }
            ]
          }
        };
      }
      return b;
    }));
  };

  const handleSendChatMessage = (text: string) => {
    const newMsg: ChatMessage = {
      id: `msg-${Date.now()}`,
      tutorId: 'tutor-1',
      sender: userRole === 'parent' ? 'parent' : 'tutor',
      text,
      timestamp: new Date().toLocaleDateString() + ' ' + new Date().toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})
    };
    setChatMessages(prev => [...prev, newMsg]);

    // Fast reply automation script simulating second user
    setTimeout(() => {
      const replySender = userRole === 'parent' ? 'tutor' : 'parent';
      const autoResponse: ChatMessage = {
        id: `msg-${Date.now() + 1}`,
        tutorId: 'tutor-1',
        sender: replySender,
        text: replySender === 'tutor' 
          ? "Thank you Sarah, I received your message. Let's make sure Jessica focuses on algebraic equations."
          : "Understood Mr. Lim. Please keep me updated with her homework worksheet results.",
        timestamp: new Date().toLocaleDateString() + ' ' + new Date().toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})
      };
      setChatMessages(prev => [...prev, autoResponse]);
    }, 2500);
  };

  const handleVerifyTutor = (tutorId: string) => {
    setTutors(prev => prev.map(t => {
      if (t.id === tutorId) {
        // Tutor verification logic updates qualifications
        return {
          ...t,
          qualifications: ['Verified by operations team', ...t.qualifications]
        };
      }
      return t;
    }));
  };

  const handleResolveIssue = (bookingId: string) => {
    setBookings(prev => prev.map(b => {
      if (b.id === bookingId) {
        const { issueReported, ...rest } = b; // settle dispute out of queue
        return { ...rest, status: 'completed' as const };
      }
      return b;
    }));
  };

  const unreadNotifCount = notifications.filter(n => !n.read).length;

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans text-gray-800">
      
      {/* Platform Header */}
      <MainHeader 
        currentMode={currentMode}
        onChangeMode={(mode) => setCurrentMode(mode)}
        userRole={userRole}
        onChangeRole={(role) => setUserRole(role)}
        notificationsCount={unreadNotifCount}
        onOpenNotifications={() => setIsNotifDrawerOpen(true)}
      />

      {/* Notifications Drawer */}
      {isNotifDrawerOpen && (
        <div className="fixed inset-0 bg-slate-900/30 backdrop-blur-xs z-50 flex justify-end" id="notif-drawer-backdrop">
          <div className="w-full max-w-sm bg-white h-full shadow-2xl p-6 flex flex-col justify-between animate-slide-in">
            <div>
              <div className="flex justify-between items-center border-b pb-4 mb-4" id="notif-drawer-header">
                <div className="flex items-center space-x-2">
                  <Bell className="h-5 w-5 text-blue-600" />
                  <span className="font-display font-bold text-gray-900 text-sm">Notifications Registry</span>
                </div>
                <button 
                  onClick={() => setIsNotifDrawerOpen(false)}
                  className="p-1 hover:bg-slate-50 rounded-lg text-gray-400 hover:text-gray-900 cursor-pointer text-xs"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>

              <div className="space-y-3 overflow-y-auto max-h-[80vh] pr-1" id="notifications-wrapper-list">
                {notifications.map(notif => (
                  <div 
                    key={notif.id} 
                    className={`p-3.5 rounded-xl border flex items-start gap-3 transition ${
                      notif.read ? 'bg-white border-gray-100' : 'bg-blue-50/50 border-blue-100'
                    }`}
                  >
                    <span className="w-2.5 h-2.5 bg-blue-500 rounded-full mt-1 shrink-0"></span>
                    <div>
                      <h4 className="font-bold text-gray-900 text-[11.5px] leading-tight">{notif.title}</h4>
                      <p className="text-[10.5px] text-gray-500 mt-1 leading-normal">{notif.message}</p>
                      <span className="text-[9px] text-gray-400 font-mono mt-1.5 block">{notif.timestamp}</span>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <button 
              onClick={() => {
                setNotifications(prev => prev.map(n => ({ ...n, read: true })));
                setIsNotifDrawerOpen(false);
              }}
              className="w-full bg-slate-900 text-white font-bold py-2.5 rounded-xl text-xs hover:bg-slate-800 cursor-pointer"
            >
              Mark all as read
            </button>
          </div>
        </div>
      )}

      {/* Main Content Areas */}
      <div className="flex-1">
        {currentMode === 'blueprint' ? (
          <SpecsCatalog onBackToInteractive={handleJumpFromSpecs} />
        ) : (
          /* Live Interactive State Router */
          <div>
            {landingStep === 'landing' ? (
              <div className="max-w-4xl mx-auto px-4 py-16 text-center font-sans">
                <div className="bg-white p-8 rounded-3xl border border-gray-100 shadow-xl max-w-xl mx-auto">
                  <span className="bg-blue-50 text-blue-600 font-mono text-[10px] font-bold px-3 py-1 rounded-full uppercase">Onboarding Landing Gateway</span>
                  <h1 className="font-display font-black text-gray-950 text-2xl tracking-tight mt-4">
                    Find the Right Tutor. Support every learning journey.
                  </h1>
                  <p className="text-gray-500 text-xs mt-3 leading-relaxed max-w-md mx-auto">
                    A comprehensive tutor matching database ecosystem featuring student profiles, calendar negotiations, real-time feedback reports, and direct parent-teacher chat logs.
                  </p>

                  <div className="mt-8 flex flex-col sm:flex-row gap-3 justify-center">
                    <button 
                      onClick={() => setLandingStep('selectRole')}
                      className="bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs px-6 py-3 rounded-xl transition"
                    >
                      Authenticate Account
                    </button>
                    <button 
                      onClick={() => setLandingStep('dashboard')}
                      className="bg-slate-100 hover:bg-slate-200 text-slate-800 font-bold text-xs px-6 py-3 rounded-xl transition"
                    >
                      Skip directly to Live Dashboard
                    </button>
                  </div>
                </div>
              </div>
            ) : landingStep === 'selectRole' ? (
              <div className="max-w-md mx-auto px-4 py-16 text-center">
                <div className="bg-white p-8 rounded-3xl border border-gray-100 shadow-xl">
                  <h3 className="font-display font-bold text-lg text-gray-950">Choose Your Role Profile</h3>
                  <p className="text-xs text-gray-400 mt-1">This helps us customize your initial dashboard permissions</p>
                  
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-8">
                    <button 
                      onClick={() => { setUserRole('parent'); setLandingStep('registerParent'); }}
                      className="p-5 border border-blue-100 bg-blue-50/20 hover:bg-blue-50 rounded-2xl text-center transition"
                    >
                      <span className="font-bold text-xs text-blue-900 block font-display">Parent / Student</span>
                      <span className="text-[10px] text-gray-500 mt-1 block">Search tutors & track curriculum milestones.</span>
                    </button>
                    <button 
                      onClick={() => { setUserRole('tutor'); setLandingStep('registerTutor'); }}
                      className="p-5 border border-slate-100 hover:border-indigo-100 hover:bg-slate-50 rounded-2xl text-center transition"
                    >
                      <span className="font-bold text-xs text-slate-900 block font-display">Tutor / Teacher</span>
                      <span className="text-[10px] text-gray-500 mt-1 block">Publish progress logs & track payouts.</span>
                    </button>
                  </div>
                </div>
              </div>
            ) : landingStep === 'registerParent' ? (
              <div className="max-w-md mx-auto px-4 py-12">
                <div className="bg-white p-6 rounded-2xl border border-gray-100 shadow-xl">
                  <span className="text-[10px] font-bold text-blue-600 uppercase font-mono">STEP 3 OF 5</span>
                  <h3 className="font-display font-extrabold text-lg text-gray-950 mt-1">Create Parent Account</h3>
                  
                  <form onSubmit={(e) => { e.preventDefault(); setLandingStep('verified'); }} className="mt-4 space-y-3 font-sans">
                    <div>
                      <span className="text-[10px] text-gray-500 font-semibold block">Full Name</span>
                      <input type="text" defaultValue="Sarah Tan" className="w-full bg-slate-50 border rounded-lg py-2 px-3 text-xs mt-1" required />
                    </div>
                    <div>
                      <span className="text-[10px] text-gray-500 font-semibold block">Primary Email</span>
                      <input type="email" defaultValue="sarah.tan@example.com" className="w-full bg-slate-50 border rounded-lg py-2 px-3 text-xs mt-1" required />
                    </div>
                    <div>
                      <span className="text-[10px] text-gray-500 font-semibold block">Secure Password</span>
                      <input type="password" placeholder="••••••••" className="w-full bg-slate-50 border rounded-lg py-2 px-3 text-xs mt-1" required />
                    </div>
                    <button type="submit" className="w-full bg-blue-600 text-white text-xs font-bold py-2.5 rounded-xl mt-4">Create Account</button>
                  </form>
                </div>
              </div>
            ) : landingStep === 'registerTutor' ? (
              <div className="max-w-md mx-auto px-4 py-12">
                <div className="bg-white p-6 rounded-2xl border border-gray-100 shadow-xl">
                  <span className="text-[10px] font-bold text-violet-600 uppercase font-mono">STEP 4 OF 5</span>
                  <h3 className="font-display font-extrabold text-lg text-gray-950 mt-1">Create Tutor Account</h3>
                  
                  <form onSubmit={(e) => { e.preventDefault(); setLandingStep('verified'); }} className="mt-4 space-y-3 font-sans">
                    <div>
                      <span className="text-[10px] text-gray-500 font-semibold block">Full Name</span>
                      <input type="text" defaultValue="Mr. Lim Wei Sheng" className="w-full bg-slate-50 border rounded-lg py-2 px-3 text-xs mt-1" required />
                    </div>
                    <div>
                      <span className="text-[10px] text-gray-500 font-semibold block">Primary Email</span>
                      <input type="email" defaultValue="lim.ws@example.com" className="w-full bg-slate-50 border rounded-lg py-2 px-3 text-xs mt-1" required />
                    </div>
                    <div>
                      <span className="text-[10px] text-gray-500 font-semibold block">Secure Password</span>
                      <input type="password" placeholder="••••••••" className="w-full bg-slate-50 border rounded-lg py-2 px-3 text-xs mt-1" required />
                    </div>
                    <button type="submit" className="w-full bg-violet-600 text-white text-xs font-bold py-2.5 rounded-xl mt-4">Create Account</button>
                  </form>
                </div>
              </div>
            ) : landingStep === 'verified' ? (
              <div className="max-w-sm mx-auto px-4 py-16 text-center">
                <div className="bg-white p-8 rounded-3xl border border-gray-100 shadow-xl">
                  <span className="bg-emerald-50 text-emerald-800 text-[9px] font-bold font-mono px-3 py-1 rounded">VERIFICATION SUCCESSFUL</span>
                  <h3 className="font-display font-bold text-lg text-gray-950 mt-4">Your email is validated</h3>
                  <p className="text-xs text-gray-400 mt-2">Connecting credentials, setting up persistent data registries...</p>
                  <button onClick={() => setLandingStep('dashboard')} className="w-full bg-blue-600 text-white text-xs font-bold py-2.5 rounded-xl mt-6">Open Live Dashboard</button>
                </div>
              </div>
            ) : (
              /* Inside active dashboards */
              <div>
                {userRole === 'parent' && (
                  <ParentDashboard 
                    students={students}
                    onAddStudent={handleAddStudent}
                    tutors={tutors}
                    bookings={bookings}
                    onAddBooking={handleAddBooking}
                    onUpdateBookingStatus={handleUpdateBookingStatus}
                    onReportIssue={handleReportIssue}
                    chatMessages={chatMessages}
                    onSendChatMessage={handleSendChatMessage}
                    invoices={invoices}
                    onPayInvoice={handlePayInvoice}
                    initialActiveSection={initialParentSection}
                    key={initialParentSection} // redraw cleanly
                  />
                )}

                {userRole === 'tutor' && (
                  <TutorDashboard 
                    tutor={tutors[0]}
                    bookings={bookings}
                    onUpdateBookingStatus={handleUpdateBookingStatus}
                    onSubmitLessonReport={handlePublishLessonReport}
                    onEditLessonReport={handleEditLessonReport}
                    chatMessages={chatMessages}
                    onSendChatMessage={handleSendChatMessage}
                    payouts={payouts}
                    invoices={invoices}
                  />
                )}

                {userRole === 'admin' && (
                  <AdminDashboard 
                    bookings={bookings}
                    tutors={tutors}
                    onVerifyTutor={handleVerifyTutor}
                    onResolveIssue={handleResolveIssue}
                  />
                )}

                {/* Switch tools footer box guidelines for easier interactions */}
                <div className="max-w-7xl mx-auto px-4 py-8 text-center text-xs text-gray-400 bg-white border-t mt-12">
                  <div className="flex flex-col md:flex-row justify-between items-center gap-4">
                    <p className="flex items-center gap-1"><Info className="h-4.5 w-4.5 text-blue-500" /> Toggle perspectives at the top acting bar to test simultaneous interactions.</p>
                    <div className="flex gap-2">
                      <button onClick={() => setLandingStep('landing')} className="text-blue-600 hover:underline">Restart Sandbox Flow</button>
                      <span>•</span>
                      <button onClick={() => setCurrentMode('blueprint')} className="text-blue-600 hover:underline">Explore Specs (27 Sheets)</button>
                    </div>
                  </div>
                </div>

              </div>
            )}
          </div>
        )}
      </div>

    </div>
  );
}

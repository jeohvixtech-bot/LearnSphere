using LearnSphere.API.Models;

namespace LearnSphere.API.Services;

public interface IPresetCancellationService
{
    // Resolves one PresetCancellationDecision toward "credit": shrinks the
    // booking/invoice (or cancels the booking outright if this was its last
    // remaining session), charges the tutor a 20% penalty against their payout
    // balance, and notifies the parent. Shared by two call sites — a straight
    // cancel with no reschedule offered resolves this immediately
    // (TutorsController.DeleteSlot), while a parent-rejected reschedule proposal
    // resolves it later once an admin approves it (AdminController). Both must
    // stay in lock-step, hence one shared implementation rather than duplicating
    // this financially-sensitive logic in two controllers.
    Task ResolveTowardCreditAsync(PresetCancellationDecision decision, Booking booking, string reason);
}

namespace LearnSphere.API.Services;

// Version 1 of the Terms and Conditions, inserted on first startup when the TermsVersions
// table is empty. Later versions are published by an admin and live only in the database.
public static class TermsSeedText
{
    public const string Version = "2026-10-14";

    public const string Content = """
Last updated: 14 October 2026

LearnSphere is an online marketplace operating in Malaysia and Singapore that connects Parents/Students seeking private tuition with independent Tutors ("the Platform"). These Terms and Conditions ("Terms") govern your access to and use of the Platform, operated by LearnSphere ("LearnSphere", "we", "us", or "our"). By creating an account, checking the box below, or otherwise accessing or using the Platform, you ("you" or the "User") agree to be bound by these Terms and by our Privacy Policy. If you do not agree, you must not register for or use the Platform.

## 1. Role of the Platform

LearnSphere is an intermediary technology and payment-facilitation platform that helps Parents/Students and Tutors find each other, schedule classes, communicate, pay and get paid, and exchange feedback. LearnSphere is not a tuition agency, employer, or principal to any tuition arrangement, and does not supervise or guarantee the conduct, quality, safety, or outcome of any class. Every Tutor is an independent self-employed service provider, not an employee, partner, or agent of LearnSphere, and every tuition arrangement is made directly between the Tutor and the Parent/Student. LearnSphere may refuse service to, or decline to onboard, any applicant at its discretion.

## 2. Eligibility and Accounts

- You must be at least 18 years old and legally capable of entering a binding contract to register an account, as either a **Parent / Student** or a **Tutor / Teacher**. A student under 18 may only be registered and booked for classes through a parent or legal guardian's account; a parent/guardian accepts these Terms on behalf of that student and remains responsible for their use of the Platform, including arranging for a responsible adult to be present or reasonably reachable during a minor's session.
- Tutors must hold a valid Malaysian IC / passport or Singapore NRIC / FIN (as applicable to where they are based), have the right to work in that jurisdiction, and provide accurate, current details of their qualifications and experience. Submitting a false, altered, or fraudulent identity or qualification document is grounds for immediate termination and may be reported to the relevant authorities.
- You are responsible for your account's confidentiality and all activity under it. LearnSphere operates a single active-session policy; signing in elsewhere may end an existing session.
- You must not register duplicate accounts, or a Tutor and Parent account under the same identity, to circumvent fees, verification, or rating mechanisms.

## 3. Tutor Verification

Tutors must submit identity documents, an identity number, and qualification/academic documents for administrator review before their profile is fully published and before payouts are released. LearnSphere checks submissions for completeness and apparent authenticity, but does not independently verify every credential or conduct a criminal background check, and does not guarantee a Tutor's competence or suitability for any particular Student. Parents remain responsible for exercising their own judgement, including about in-person safety arrangements. A rejected document may be corrected and resubmitted once; repeated fraudulent submissions will result in account termination.

## 4. Bookings, Scheduling and Classes

- Tutors publish one-to-one slots and/or recurring "preset" class groups with a stated subject, level, mode (online, at the Tutor's place, or at a tuition centre), price, and schedule. Booking a preset group is all-or-nothing across its sessions; a group that becomes fully booked, or any of whose sessions is no longer available, is removed from listings and cannot be booked.
- A booking is confirmed only once payment is successfully completed. An unpaid or failed checkout does not reserve the slot.
- **If a Tutor is unable to conduct a scheduled, confirmed class**, they must either (a) reschedule it to a new date and time — which does not require the Parent's prior approval, though the Parent will always be notified in-app of the new date and time — or (b) cancel the class outright. A Tutor who fails to show up for a scheduled class without having rescheduled it in advance is treated the same as a Tutor who cancels it outright (see Section 5 for the refund that follows). Repeated cancellations are tracked as a quality signal against the Tutor's score/ranking on the Platform, and may reduce their visibility to Parents or lead to further review.
- **A Parent may cancel a booking themselves on the Platform only before it has been paid for.** Once a class is confirmed and paid, the Parent cannot unilaterally cancel it through the Platform. A Parent who wishes to cancel a paid class and request a refund must email LearnSphere at jeohvixtech@gmail.com stating a valid reason. Whether to approve or reject the request is at LearnSphere's sole discretion, and any approved refund is capped at 50% of the amount paid (see Section 5).
- LearnSphere may set reasonable minimum-advance-notice rules for scheduling new classes.
- You agree not to arrange, or ask the other party to arrange, payment for a Platform-introduced class outside the Platform in order to avoid applicable fees. Doing so is a breach of these Terms and may result in suspension.

## 5. Fees and Payments

- Tutors set their own session price. LearnSphere adds a platform service fee/markup on top, shown in the payment summary before checkout is confirmed; rates may be updated from time to time and the current rate always governs at the time of booking. Prices and fees are shown in Malaysian Ringgit (MYR) or Singapore Dollars (SGD) depending on your account's country setting.
- Payments are processed by our third-party payment gateway, HitPay. LearnSphere does not store your full card or bank account details; payment information is handled by the processor under its own terms and security practices.
- **Failed, expired, cancelled, or abandoned checkouts:** if a payment is not completed successfully, the associated invoice is voided and the booking is automatically cancelled, releasing the class slot — no booking is held open against an incomplete payment.
- **Parent cancels before payment:** since no payment has been taken yet, there is nothing to refund — the booking is simply cancelled and the slot released.
- **Parent requests cancellation of a paid class:** this is not a self-service action. The Parent must email jeohvixtech@gmail.com with a valid reason. LearnSphere reviews each request and decides, at its sole discretion, whether to approve or reject it. If approved, the refund may be up to 50% of the amount paid, not the full amount, and is credited to the Parent's in-app wallet.
- **Tutor cancels, or fails to show up for, a confirmed, paid class:** the Parent receives a full refund to their in-app wallet for that class, with no deduction. This applies equally whether the Tutor cancels it outright or simply does not show up without having rescheduled it in advance (see Section 4) — in both cases the outcome is the same full refund.
- **Student does not attend a scheduled, paid class:** once the Tutor is present and ready to teach at the scheduled time, the class is treated as delivered. If the Student is marked "absent" on the Tutor's class report, no refund or wallet credit is given for that session.
- Tutors are paid out from completed, non-disputed sessions on LearnSphere's payout schedule, net of the applicable platform fee, and only once document verification is complete.
- Promotional credits, where issued, are non-transferable, have no cash value, may expire, and may be withdrawn if obtained through abuse of the Platform.

## 6. Class Remarks and the Tutor Bulletin Board

After a class is completed, the Parent may leave a star rating and a written remark, automatically published to the Tutor's bulletin board and public profile with the Parent's name partially masked. Parents may edit or delete their own remarks at any time. A Tutor who believes a remark is inaccurate or abusive may request it be hidden pending review; LearnSphere administrators have final discretion to resolve such disputes, including removing content that breaches these Terms. A Tutor's overall rating is the average of each Parent's own average rating of that Tutor, rather than a simple average of every remark, so one Parent's volume of remarks cannot disproportionately move the score. Remarks and ratings must reflect your genuine experience and must not be fabricated, traded for payment or favours, or posted to harass another user.

## 7. User Conduct and Content

You agree not to: post or send content that is defamatory, obscene, hateful, harassing, or that our automated filters flag as profane or abusive; misrepresent your identity, qualifications, or relationship to a Student; solicit or make payments outside the Platform to avoid fees; upload falsified identity or academic documents; scrape, reverse engineer, or interfere with the Platform; or use the Platform for any unlawful purpose. Messages, remarks, and dispute submissions may be reviewed by LearnSphere administrators to enforce these Terms, investigate a dispute, or respond to a legal request.

## 8. Direct Messages and Notifications

The Platform provides in-app messaging between Parents and Tutors with an active or past booking, and in-app notifications for booking, payment, verification, and dispute events. You consent to receive these notifications and related service emails (such as payment receipts or verification outcomes) as part of using the Platform.

## 9. Disputes Between Users

LearnSphere provides in-app tools (reschedule approvals, the bulletin board dispute/hide request) to help users resolve disagreements directly. Where that fails, LearnSphere administrators may review the available records (bookings, messages, payment status) and make a decision for purposes of the Platform, such as approving a refund, cancelling a booking, or removing content. This does not limit either party's separate legal rights, including referring an unresolved matter in Singapore to the Singapore Mediation Centre or the Small Claims Tribunal, or in Malaysia to the Tribunal for Consumer Claims, where applicable.

## 10. Intellectual Property

The Platform's software, design, and branding (excluding User-submitted content such as remarks, messages, and profile material) belong to LearnSphere or its licensors. You keep ownership of content you submit, but grant LearnSphere a worldwide, royalty-free licence to host, display, and distribute it as needed to operate the Platform (for example, showing your public tutor profile or a featured remark to visitors who are not logged in).

## 11. Disclaimers and Limitation of Liability

The Platform is provided "as is" and "as available," without warranty that it will be uninterrupted, error-free, or secure. LearnSphere is not responsible for the conduct, acts, or omissions of any Tutor or Parent, in person or online, including during any tuition session, examination results, or any loss or injury occurring at a Tutor's or Parent's premises. To the fullest extent permitted by law, LearnSphere will not be liable for indirect, incidental, special, or consequential damages, and our total aggregate liability to you for any claim relating to these Terms or the Platform is capped at the total platform fees you paid us in the three (3) months preceding the event giving rise to the claim.

## 12. Suspension and Termination

LearnSphere may suspend or terminate your account, or restrict access to a feature, at any time if we reasonably believe you have breached these Terms, provided false information, failed verification, or harmed another user or the Platform. You may close your own account at any time by contacting us; outstanding bookings, payouts, and disputes will be settled under these Terms first.

## 13. Privacy and Data Protection

We collect and use your personal data — including identity and academic documents submitted for Tutor verification, and a Parent's or Student's booking and contact details — as described in our Privacy Policy, and in accordance with Malaysia's Personal Data Protection Act 2010 (Act 709) and/or Singapore's Personal Data Protection Act 2012, whichever applies to you. We share only the minimum information a counterparty needs to carry out a confirmed booking (such as a Tutor's or Parent's name and contact details), and identity documents are held with restricted access and used only for verification. You may request access to, correction of, or deletion of your personal data by contacting us through the Platform.

## 14. Changes to These Terms

We may update these Terms to reflect changes to the Platform, our fees, or applicable law. Material changes will be notified in-app or by email before taking effect. Continued use of the Platform after an update is acceptance of the revised Terms.

## 15. Governing Law and Jurisdiction

If your account's country setting is Malaysia, these Terms are governed by the laws of Malaysia and subject to the non-exclusive jurisdiction of the Malaysian courts. If your account's country setting is Singapore, these Terms are governed by the laws of the Republic of Singapore and subject to the non-exclusive jurisdiction of the Singapore courts. Nothing in this clause prevents either party from relying on the dispute-resolution routes described in Section 9.

## 16. Contact

Questions about these Terms, and requests under Sections 4 and 5, can be sent to jeohvixtech@gmail.com, or via the contact details on our Contact page.
""";
}

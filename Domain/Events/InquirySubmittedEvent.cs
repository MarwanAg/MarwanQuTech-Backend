using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Entities;

namespace MarwanQuTech.Domain.Events;

/// <summary>
/// A visitor sent the contact form. Dispatched after SaveChanges (so the Id exists)
/// → the Application layer sends the notification e-mail via MailKit.
/// </summary>
public sealed record InquirySubmittedEvent(Inquiry Inquiry, DateTime OccurredOnUtc) : DomainEvent(OccurredOnUtc);

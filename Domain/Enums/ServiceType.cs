namespace MarwanQuTech.Domain.Enums;

/// <summary>
/// Service a visitor is asking about in the contact form.
/// Stored as int; explicit values so reordering never corrupts existing rows.
/// Display labels (Arabic / English) live in the frontend translation files.
/// </summary>
public enum ServiceType
{
    WebDevelopment = 1,
    ApiDevelopment = 2,
    SystemArchitecture = 3,
    ProcessAutomation = 4,
    Consulting = 5,
    Maintenance = 6,
    Other = 99
}

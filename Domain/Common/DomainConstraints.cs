namespace MarwanQuTech.Domain.Common;

/// <summary>
/// Single source of truth for field limits.
/// Reused by: Domain guards, FluentValidation validators (Application)
/// and EF Core column configuration (Infrastructure) — so the three never drift apart.
/// </summary>
public static class DomainConstraints
{
    public const int UrlMaxLength = 500;
    public const int SlugMaxLength = 200;
    public const int EmailMaxLength = 254;

    public static class Projects
    {
        public const int TitleMaxLength = 200;
        public const int SummaryMaxLength = 500;
        public const int DescriptionMaxLength = 50_000;
        public const int MaxImages = 20;
    }

    public static class ProjectImages
    {
        public const int AltTextMaxLength = 250;
    }

    public static class Articles
    {
        public const int TitleMaxLength = 200;
        public const int SummaryMaxLength = 500;
        public const int ContentMaxLength = 200_000;
        public const int SeoTitleMaxLength = 70;
        public const int SeoDescriptionMaxLength = 160;
    }

    public static class Inquiries
    {
        public const int ClientNameMinLength = 2;
        public const int ClientNameMaxLength = 100;
        public const int PhoneMaxLength = 20;
        public const int DetailsMinLength = 10;
        public const int DetailsMaxLength = 4_000;
    }

    public static class AdminUsers
    {
        public const int DisplayNameMaxLength = 100;
        public const int PasswordHashMaxLength = 500;
    }

    public static class RefreshTokens
    {
        public const int TokenHashMaxLength = 128;
    }
}

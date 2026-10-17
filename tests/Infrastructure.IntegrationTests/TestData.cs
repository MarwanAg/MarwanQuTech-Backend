using MarwanQuTech.Application.Common.Models;
using MarwanQuTech.Application.Features.Articles;
using MarwanQuTech.Application.Features.Projects;
using SkiaSharp;

namespace MarwanQuTech.Infrastructure.IntegrationTests;

internal static class TestData
{
    public static CreateProjectCommand FullProject(string titleEn = "Booking System", string titleAr = "نظام الحجوزات") => new(
        titleAr, titleEn,
        "ملخص المشروع", "Project summary",
        "## الوصف", "## Description",
        SlugAr: null, SlugEn: null,
        GithubUrl: "https://github.com/MarwanAg/demo", LiveUrl: null,
        IsReal: true, DisplayOrder: 0);

    public static CreateArticleCommand FullArticle(string titleEn = "Clean Architecture in .NET", string titleAr = "مقدمة في Clean Architecture") => new(
        titleAr, titleEn,
        "ملخص المقال", "Article summary",
        "محتوى المقال بالعربية", "Article body in English",
        SlugAr: null, SlugEn: null);

    public static ImageUpload Png(int width, int height, string fileName = "photo.png")
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Teal);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();
        return new ImageUpload(new MemoryStream(bytes), fileName, "image/png", bytes.Length);
    }
}

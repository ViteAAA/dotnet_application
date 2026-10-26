using PrintingSlips.Models;

namespace PrintingSlips.Interfaces;

/// <summary>
/// Интерфейс экспорта печатных форм в PDF документ.
/// Поддерживает генерацию PDF для каждого вида формы (накладная, кухонный чек, шаблон, тест).
/// </summary>
public interface IPdfSlipExporter
{
    /// <summary>
    /// Экспортирует накладную (Invoice) в байтовый массив PDF
    /// </summary>
    byte[] ExportInvoicePdf(Slip slip, float widthMm = 80, string fontFamily = "Courier New");

    /// <summary>
    /// Экспортирует кухонный чек (Kitchen Slip) в байтовый массив PDF
    /// </summary>
    byte[] ExportKitchenSlipPdf(Slip slip, float widthMm = 80, string fontFamily = "Courier New");

    /// <summary>
    /// Экспортирует чек по динамическому шаблону (ReceiptField) в байтовый массив PDF
    /// </summary>
    byte[] ExportTemplatePdf(Slip slip, float widthMm, IReadOnlyList<ReceiptField> fields, string fontFamily = "Courier New");

    /// <summary>
    /// Экспортирует тестовую страницу термопринтера в байтовый массив PDF
    /// </summary>
    byte[] ExportTestPagePdf(float widthMm = 80, string fontFamily = "Courier New");

    /// <summary>
    /// Универсальный метод экспорта любой печатной формы в PDF
    /// </summary>
    byte[] ExportFormPdf(PrintFormType formType, Slip slip, float widthMm = 80, IReadOnlyList<ReceiptField>? fields = null, string fontFamily = "Courier New");

    /// <summary>
    /// Сохраняет сформированный PDF напрямую в файл на диске
    /// </summary>
    string SavePdfToFile(string outputDirectory, PrintFormType formType, Slip slip, float widthMm = 80, IReadOnlyList<ReceiptField>? fields = null);
}
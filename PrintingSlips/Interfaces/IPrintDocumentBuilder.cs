using PrintingSlips.Models;

namespace PrintingSlips.Interfaces;

/// <summary>
/// Интерфейс строителя печатных форм для термопринтеров (ESC/POS команды, разметка текста)
/// </summary>
public interface IPrintDocumentBuilder
{
    /// <summary>
    /// Формирует байтовый буфер ESC/POS для накладной (Invoice)
    /// </summary>
    byte[] BuildInvoiceEscPos(Slip slip, float widthMm, PrinterTextProfile profile);

    /// <summary>
    /// Формирует байтовый буфер ESC/POS для сервисного кухонного чека (Kitchen Slip)
    /// </summary>
    byte[] BuildKitchenSlipEscPos(Slip slip, float widthMm, PrinterTextProfile profile);

    /// <summary>
    /// Формирует байтовый буфер ESC/POS по динамическому набору полей (ReceiptField)
    /// </summary>
    byte[] BuildTemplateEscPos(Slip slip, float widthMm, IReadOnlyList<ReceiptField> fields, PrinterTextProfile profile);

    /// <summary>
    /// Формирует байтовый буфер ESC/POS для тестовой страницы принтера
    /// </summary>
    byte[] BuildTestPageEscPos(float widthMm, PrinterTextProfile profile);

    /// <summary>
    /// Универсальный метод построения печатной формы
    /// </summary>
    PrintFormResult BuildForm(PrintFormType formType, Slip slip, float widthMm, PrinterTextProfile profile, IReadOnlyList<ReceiptField>? templateFields = null);

    /// <summary>
    /// Определяет профиль кодировки и страницы символов по имени принтера
    /// </summary>
    PrinterTextProfile ResolvePrinterProfile(string printerName);

    /// <summary>
    /// Возвращает число символов в строке для заданной ширины ленты (32 для 58мм, 48 для 80мм)
    /// </summary>
    int GetCharsPerLine(float widthMm);
}
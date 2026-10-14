using PrintingSlips.Models; // Или namespace, где лежит ReceiptField

namespace PrintingSlips.Interfaces;

public interface ITemplateProvider
{
    /// <summary>
    /// Загружает список полей шаблона для указанного принтера
    /// </summary>
    List<ReceiptField> GetTemplateForPrinter(string printerName);
}
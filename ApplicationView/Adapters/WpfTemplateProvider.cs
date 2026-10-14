using System.Collections.Generic;
using PrintingSlips.Models;
using ApplicationView.Services; // Где лежит ваш ProcessingData
using PrintingSlips.Interfaces;

namespace ApplicationView.Adapters;

public class WpfTemplateProvider : ITemplateProvider
{
    public List<ReceiptField> GetTemplateForPrinter(string printerName)
    {
        // Вызываем ваш сервисный класс WPF проекта
        return ProcessingData.LoadTemplate(printerName);
    }
}